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

            var options = new CoreWebView2EnvironmentOptions
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
        if (_browserPool.TryGetValue(section, out var existingWebView))
        {
            ShowOnlySelectedWebView(existingWebView);
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
            DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, 11, 15, 25),
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

        // Lớp chặn 1: Nạp trực tiếp tiện ích uBlock Origin vào Profile WebView2
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
                    // Trường hợp extension đã được cài đặt từ phiên trước, ghi nhật ký nhẹ và tiếp tục
                    _uBlockLoadedInProfile = true;
                    _errorService.LogError(ErrorCategory.WebLoadFailed, "uBlock AddBrowserExtensionAsync notice", ex);
                }
            }
        }

        // Lớp chặn 2: Dự phòng tầng mạng chặn trực tiếp các tên miền quảng cáo
        core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
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

        // Giữ mọi liên kết mở mới nằm ngay trong khung toàn màn hình ZenBox
        core.NewWindowRequested += (_, args) =>
        {
            args.Handled = true;
            if (!string.IsNullOrWhiteSpace(args.Uri))
            {
                core.Navigate(args.Uri);
            }
        };

        core.DocumentTitleChanged += (_, _) =>
        {
            if (webView.Visibility == Visibility.Visible)
            {
                ActiveTitleChanged?.Invoke(this, core.DocumentTitle);
            }
        };

        core.NavigationStarting += (_, _) =>
        {
            if (webView.Visibility == Visibility.Visible)
            {
                LoadingStateChanged?.Invoke(this, true);
            }
        };

        core.NavigationCompleted += (_, args) =>
        {
            if (webView.Visibility == Visibility.Visible)
            {
                LoadingStateChanged?.Invoke(this, false);
            }

            if (!args.IsSuccess &&
                args.WebErrorStatus is not CoreWebView2WebErrorStatus.OperationCanceled
                    and not CoreWebView2WebErrorStatus.ConnectionAborted)
            {
                var category = !_networkService.IsOnline
                    ? ErrorCategory.NetworkOffline
                    : ErrorCategory.WebLoadFailed;

                _errorService.ReportError(
                    category,
                    $"WebView2 NavigationCompleted failed ({section}): WebErrorStatus={args.WebErrorStatus}",
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

    private void ShowOnlySelectedWebView(WebView2 active)
    {
        foreach (var item in _browserPool.Values)
        {
            item.Visibility = ReferenceEquals(item, active) ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    public void HideAllTabs()
    {
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
