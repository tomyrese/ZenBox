using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using ZenBox.Models;

namespace ZenBox.Services;

public sealed class WebBrowserService : IDisposable
{
    private static readonly string[] BlockedAdDomains =
    [
        "doubleclick.net",
        "googlesyndication.com",
        "googleadservices.com",
        "adservice.google.com",
        "pagead2.googlesyndication.com",
        "ads.tiktok.com",
        "analytics.tiktok.com",
        "an.facebook.com",
        "adnxs.com",
        "taboola.com",
        "outbrain.com",
        "criteo.com",
        "rubiconproject.com",
        "pubmatic.com",
        "moatads.com",
        "scorecardresearch.com"
    ];

    private const string CleanEdgeUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36 Edg/131.0.0.0";

    // CSS & JS biến trang web thành trải nghiệm Native App thực thụ: ẩn thanh cuộn thô, tắt kéo thả ảnh/link, nền tối chống chớp trắng, ẩn banner giục cài app
    private const string NativeAppModeInjectionScript = """
        (() => {
            const injectAppStyle = () => {
                if (document.getElementById('zenbox-native-app-style')) return;
                const style = document.createElement('style');
                style.id = 'zenbox-native-app-style';
                style.textContent = `
                    html, body {
                        overscroll-behavior: none !important;
                        -webkit-tap-highlight-color: transparent !important;
                    }
                    ::-webkit-scrollbar {
                        width: 0px !important;
                        height: 0px !important;
                        display: none !important;
                    }
                    img, a {
                        -webkit-user-drag: none !important;
                    }
                    /* Ẩn các banner gạ tải ứng dụng di động/máy tính trên web */
                    [data-e2e="download-app-container"],
                    [class*="DivPromotionContainer"],
                    [id*="smartbanner"],
                    .smartbanner {
                        display: none !important;
                    }
                `;
                (document.head || document.documentElement).appendChild(style);
            };
            injectAppStyle();
            document.addEventListener('DOMContentLoaded', injectAppStyle);
        })();
        """;

    private readonly ErrorHandlingService _errorService;
    private readonly NetworkMonitorService _networkService;
    private readonly Dictionary<AppSection, WebView2> _browserPool = new();
    private readonly HashSet<AppSection> _initializedTabs = new();
    private readonly SemaphoreSlim _envLock = new(1, 1);

    private CoreWebView2Environment? _sharedEnvironment;
    private bool _uBlockLoadedInProfile;
    private bool _disposed;

    public IReadOnlyDictionary<AppSection, WebView2> BrowserPool => _browserPool;

    public event EventHandler<string>? ActiveTitleChanged;
    public event EventHandler<bool>? LoadingStateChanged;
    public event EventHandler<bool>? AppSplashVisibilityChanged;

    public WebBrowserService(ErrorHandlingService errorService, NetworkMonitorService networkService)
    {
        _errorService = errorService;
        _networkService = networkService;
    }

    public static string GetUBlockExtensionDirectory()
    {
        return Path.Combine(AppContext.BaseDirectory, "Assets", "Extensions", "uBlock");
    }

    public async Task<CoreWebView2Environment> GetOrCreateEnvironmentAsync()
    {
        if (_sharedEnvironment is not null)
        {
            return _sharedEnvironment;
        }

        await _envLock.WaitAsync();
        try
        {
            if (_sharedEnvironment is not null)
            {
                return _sharedEnvironment;
            }

            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ZenBox",
                "WebView2Profile");

            Directory.CreateDirectory(userDataFolder);

            var options = new CoreWebView2EnvironmentOptions(
                additionalBrowserArguments: "--autoplay-policy=no-user-gesture-required")
            {
                AreBrowserExtensionsEnabled = true
            };

            _sharedEnvironment = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: userDataFolder,
                options: options);

            return _sharedEnvironment;
        }
        finally
        {
            _envLock.Release();
        }
    }

    public async Task<WebView2> GetOrCreateTabAsync(AppSection section, string targetUrl, Panel hostPanel)
    {
        PauseBackgroundTabsMedia(exceptSection: section);

        // Nếu ứng dụng đã mở sẵn trong Pool: chuyển tức thì như Native App đang chạy ngầm
        if (_browserPool.TryGetValue(section, out var existingWebView))
        {
            ShowOnlySelectedWebView(existingWebView);
            LoadingStateChanged?.Invoke(this, false);
            AppSplashVisibilityChanged?.Invoke(this, false);
            if (!string.IsNullOrWhiteSpace(existingWebView.CoreWebView2?.DocumentTitle))
            {
                ActiveTitleChanged?.Invoke(this, existingWebView.CoreWebView2.DocumentTitle);
            }
            return existingWebView;
        }

        var webView = new WebView2
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, 6, 9, 19),
            Visibility = Visibility.Visible
        };

        hostPanel.Children.Add(webView);
        _browserPool[section] = webView;
        ShowOnlySelectedWebView(webView);

        try
        {
            // Hiện màn hình khởi động App (Splash Logo) mượt mà ở lần khởi chạy đầu tiên để che hoàn toàn quá trình nạp trang
            AppSplashVisibilityChanged?.Invoke(this, true);
            LoadingStateChanged?.Invoke(this, true);

            var env = await GetOrCreateEnvironmentAsync();
            await webView.EnsureCoreWebView2Async(env);

            await ConfigureAdBlockingAndAppExperienceAsync(webView, section);
            webView.CoreWebView2.Navigate(targetUrl);
        }
        catch (Exception ex)
        {
            AppSplashVisibilityChanged?.Invoke(this, false);
            LoadingStateChanged?.Invoke(this, false);
            _errorService.ReportError(
                ErrorCategory.WebLoadFailed,
                $"Initialize WebView2 for {section} ({targetUrl})",
                ex,
                () => _ = SafeNavigateAsync(webView, targetUrl));
        }

        return webView;
    }

    private async Task ConfigureAdBlockingAndAppExperienceAsync(WebView2 webView, AppSection section)
    {
        var core = webView.CoreWebView2;
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.IsBuiltInErrorPageEnabled = false;
        core.Settings.IsZoomControlEnabled = false;
        core.Settings.IsPinchZoomEnabled = false;
        core.Settings.IsSwipeNavigationEnabled = true;
        core.Settings.IsPasswordAutosaveEnabled = true;
        core.Settings.IsGeneralAutofillEnabled = true;
        core.Settings.UserAgent = CleanEdgeUserAgent;

        // Tiêm CSS/JS chế độ Native App trước khi trang vẽ khung hình đầu tiên
        await core.AddScriptToExecuteOnDocumentCreatedAsync(NativeAppModeInjectionScript);

        // Lớp chặn 1: uBlock Origin
        if (!_uBlockLoadedInProfile)
        {
            var uBlockDir = GetUBlockExtensionDirectory();
            var manifestPath = Path.Combine(uBlockDir, "manifest.json");
            if (File.Exists(manifestPath))
            {
                try
                {
                    await core.Profile.AddBrowserExtensionAsync(uBlockDir);
                    _uBlockLoadedInProfile = true;
                }
                catch (Exception ex)
                {
                    _uBlockLoadedInProfile = true;
                    _errorService.LogError(ErrorCategory.WebLoadFailed, "uBlock AddBrowserExtensionAsync notice", ex);
                }
            }
        }

        // Lớp chặn 2: Chặn tên miền quảng cáo
        foreach (var domain in BlockedAdDomains)
        {
            core.AddWebResourceRequestedFilter($"*://{domain}/*", CoreWebView2WebResourceContext.All);
            core.AddWebResourceRequestedFilter($"*://*.{domain}/*", CoreWebView2WebResourceContext.All);
        }

        core.WebResourceRequested += (_, args) =>
        {
            if (IsBlockedAdUri(args.Request.Uri))
            {
                args.Response = core.Environment.CreateWebResourceResponse(
                    Stream.Null,
                    403,
                    "Blocked",
                    "Content-Type: text/plain");
            }
        };

        // Giữ mọi luồng đăng nhập OAuth / liên kết trong cùng cửa sổ ứng dụng
        core.NewWindowRequested += (_, args) =>
        {
            args.Handled = true;
            if (!string.IsNullOrWhiteSpace(args.Uri) &&
                Uri.TryCreate(args.Uri, UriKind.Absolute, out var popupUri) &&
                (popupUri.Scheme == Uri.UriSchemeHttp || popupUri.Scheme == Uri.UriSchemeHttps))
            {
                core.Navigate(popupUri.ToString());
            }
        };

        core.NavigationStarting += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Uri) &&
                Uri.TryCreate(args.Uri, UriKind.Absolute, out var navUri))
            {
                if (navUri.Scheme is not ("http" or "https" or "about" or "blob" or "data" or "edge"))
                {
                    args.Cancel = true;
                    return;
                }
            }

            if (webView.Visibility == Visibility.Visible)
            {
                LoadingStateChanged?.Invoke(this, true);
            }
        };

        // Cấp quyền phát tự động & nội dung bản quyền DRM (Widevine/PlayReady cho Netflix & Spotify)
        core.PermissionRequested += (_, args) =>
        {
            var kindName = args.PermissionKind.ToString();
            if (args.PermissionKind is CoreWebView2PermissionKind.Autoplay or CoreWebView2PermissionKind.ClipboardRead ||
                string.Equals(kindName, "ProtectedMediaIdentifier", StringComparison.OrdinalIgnoreCase))
            {
                args.State = CoreWebView2PermissionState.Allow;
            }
        };

        core.DOMContentLoaded += (_, _) =>
        {
            if (!_initializedTabs.Contains(section))
            {
                _initializedTabs.Add(section);
                if (webView.Visibility == Visibility.Visible)
                {
                    AppSplashVisibilityChanged?.Invoke(this, false);
                }
            }
        };

        core.DocumentTitleChanged += (_, _) =>
        {
            if (webView.Visibility == Visibility.Visible)
            {
                ActiveTitleChanged?.Invoke(this, core.DocumentTitle);
            }
        };

        core.NavigationCompleted += (_, args) =>
        {
            _initializedTabs.Add(section);
            if (webView.Visibility == Visibility.Visible)
            {
                LoadingStateChanged?.Invoke(this, false);
                AppSplashVisibilityChanged?.Invoke(this, false);
            }

            if (!args.IsSuccess && IsGenuineFatalWebError(args.WebErrorStatus))
            {
                var category = !_networkService.IsOnline
                    ? ErrorCategory.NetworkOffline
                    : ErrorCategory.WebLoadFailed;

                _errorService.ReportError(
                    category,
                    $"WebView2 fatal navigation failure ({section}): WebErrorStatus={args.WebErrorStatus}",
                    null,
                    () =>
                    {
                        try
                        {
                            core.Reload();
                        }
                        catch (Exception reloadEx)
                        {
                            _errorService.LogError(ErrorCategory.WebLoadFailed, "Reload WebView2", reloadEx);
                        }
                    });
            }
        };
    }

    private static bool IsGenuineFatalWebError(CoreWebView2WebErrorStatus status)
    {
        return status is
            CoreWebView2WebErrorStatus.HostNameNotResolved or
            CoreWebView2WebErrorStatus.CannotConnect or
            CoreWebView2WebErrorStatus.ConnectionReset or
            CoreWebView2WebErrorStatus.Disconnected or
            CoreWebView2WebErrorStatus.Timeout or
            CoreWebView2WebErrorStatus.ServerUnreachable or
            CoreWebView2WebErrorStatus.ErrorHttpInvalidServerResponse;
    }

    public static bool IsBlockedAdUri(string? rawUri)
    {
        if (string.IsNullOrWhiteSpace(rawUri) || !Uri.TryCreate(rawUri, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var host = uri.Host;
        foreach (var domain in BlockedAdDomains)
        {
            if (host.Equals(domain, StringComparison.OrdinalIgnoreCase) ||
                host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private async Task SafeNavigateAsync(WebView2 webView, string url)
    {
        try
        {
            var env = await GetOrCreateEnvironmentAsync();
            await webView.EnsureCoreWebView2Async(env);
            webView.CoreWebView2.Navigate(url);
        }
        catch (Exception ex)
        {
            _errorService.ReportError(ErrorCategory.WebLoadFailed, $"SafeNavigateAsync ({url})", ex);
        }
    }

    private void PauseBackgroundTabsMedia(AppSection? exceptSection = null)
    {
        foreach (var (sec, view) in _browserPool)
        {
            if (exceptSection.HasValue && sec == exceptSection.Value)
            {
                continue;
            }

            try
            {
                if (view.CoreWebView2 is not null)
                {
                    _ = view.CoreWebView2.ExecuteScriptAsync(
                        "document.querySelectorAll('video,audio').forEach(el => { try { el.pause(); } catch(e){} });");
                }
            }
            catch
            {
                // Bỏ qua nếu trang chưa sẵn sàng
            }
        }
    }

    private void ShowOnlySelectedWebView(WebView2 active)
    {
        foreach (var item in _browserPool.Values)
        {
            item.Visibility = ReferenceEquals(item, active) ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    public void HideAllTabs()
    {
        AppSplashVisibilityChanged?.Invoke(this, false);
        PauseBackgroundTabsMedia();
        foreach (var item in _browserPool.Values)
        {
            item.Visibility = Visibility.Collapsed;
        }
    }

    public void NavigateActiveTabBack()
    {
        var active = _browserPool.Values.FirstOrDefault(v => v.Visibility == Visibility.Visible);
        if (active?.CoreWebView2?.CanGoBack == true)
        {
            active.CoreWebView2.GoBack();
        }
    }

    public void ReloadActiveTab()
    {
        var active = _browserPool.Values.FirstOrDefault(v => v.Visibility == Visibility.Visible);
        active?.CoreWebView2?.Reload();
    }

    public void NavigateActiveTabToUrl(string rawInput)
    {
        var active = _browserPool.Values.FirstOrDefault(v => v.Visibility == Visibility.Visible);
        if (active?.CoreWebView2 is null || string.IsNullOrWhiteSpace(rawInput))
        {
            return;
        }

        var trimmed = rawInput.Trim();
        string finalUrl;
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var parsed) &&
            (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps))
        {
            finalUrl = parsed.ToString();
        }
        else if (trimmed.Contains('.') && !trimmed.Contains(' '))
        {
            finalUrl = "https://" + trimmed;
        }
        else
        {
            finalUrl = "https://www.google.com/search?q=" + Uri.EscapeDataString(trimmed);
        }

        active.CoreWebView2.Navigate(finalUrl);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var webView in _browserPool.Values)
        {
            webView.Dispose();
        }
        _browserPool.Clear();
        _envLock.Dispose();
    }
}
