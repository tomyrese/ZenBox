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

    private readonly ErrorHandlingService _errorService;
    private readonly NetworkMonitorService _networkService;
    private readonly Dictionary<AppSection, WebView2> _browserPool = new();
    private readonly SemaphoreSlim _envLock = new(1, 1);

    private CoreWebView2Environment? _sharedEnvironment;
    private bool _uBlockLoadedInProfile;
    private bool _disposed;

    public IReadOnlyDictionary<AppSection, WebView2> BrowserPool => _browserPool;

    public event EventHandler<string>? ActiveTitleChanged;
    public event EventHandler<bool>? LoadingStateChanged;
    public event EventHandler<bool>? FullscreenStateChanged;

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
                additionalBrowserArguments: "--autoplay-policy=no-user-gesture-required --disable-features=msSmartScreenProtection")
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
        // Tạm dừng âm thanh ở các tab nền khác trước khi chuyển sang tab mới
        PauseBackgroundTabsMedia(exceptSection: section);

        if (_browserPool.TryGetValue(section, out var existingWebView))
        {
            ShowOnlySelectedWebView(existingWebView);
            LoadingStateChanged?.Invoke(this, false);
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
            DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, 8, 12, 22),
            Visibility = Visibility.Visible
        };

        hostPanel.Children.Add(webView);
        _browserPool[section] = webView;
        ShowOnlySelectedWebView(webView);

        try
        {
            LoadingStateChanged?.Invoke(this, true);
            var env = await GetOrCreateEnvironmentAsync();
            await webView.EnsureCoreWebView2Async(env);

            await ConfigureAdBlockingAndPoliciesAsync(webView, section);
            webView.CoreWebView2.Navigate(targetUrl);
        }
        catch (Exception ex)
        {
            LoadingStateChanged?.Invoke(this, false);
            _errorService.ReportError(
                ErrorCategory.WebLoadFailed,
                $"Initialize WebView2 for {section} ({targetUrl})",
                ex,
                () => _ = SafeNavigateAsync(webView, targetUrl));
        }

        return webView;
    }

    private async Task ConfigureAdBlockingAndPoliciesAsync(WebView2 webView, AppSection section)
    {
        var core = webView.CoreWebView2;
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.IsBuiltInErrorPageEnabled = false;
        core.Settings.UserAgent = CleanEdgeUserAgent;

        // Lớp chặn 1: Nạp tiện ích uBlock Origin vào Profile WebView2
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

        // Lớp chặn 2: Chỉ đăng ký bộ lọc chính xác cho các domain quảng cáo để không làm chậm luồng video 4K của YouTube/TikTok
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

        // Chặn mở ứng dụng ngoài hoặc cửa sổ rời; giữ các liên kết HTTP/HTTPS ngay trong khung toàn màn hình
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

        // Ngăn các giao thức lạ (như tiktok://, fb://, intent://) gây lỗi điều hướng
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

        // Tự động cấp quyền phát tự động / toàn màn hình video
        core.PermissionRequested += (_, args) =>
        {
            if (args.PermissionKind is CoreWebView2PermissionKind.Autoplay or CoreWebView2PermissionKind.ClipboardRead)
            {
                args.State = CoreWebView2PermissionState.Allow;
            }
        };

        // Tự động ẩn thanh điều hướng trên cùng khi người dùng bấm phóng to toàn màn hình video YouTube/TikTok
        core.ContainsFullScreenElementChanged += (_, _) =>
        {
            if (webView.Visibility == Visibility.Visible)
            {
                FullscreenStateChanged?.Invoke(this, core.ContainsFullScreenElement);
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
            if (webView.Visibility == Visibility.Visible)
            {
                LoadingStateChanged?.Invoke(this, false);
            }

            // Chỉ báo lỗi khi thực sự đứt mạng hoặc không thể kết nối tới máy chủ (tránh báo lỗi giả khi TikTok/Facebook dùng SPA redirect hoặc HTTP 204/4xx phụ)
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
        FullscreenStateChanged?.Invoke(this, false);
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
