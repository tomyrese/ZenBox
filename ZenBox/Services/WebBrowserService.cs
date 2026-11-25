using System.IO;
using System.Runtime.InteropServices;
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
    private readonly ConfigService _configService;
    private readonly Dictionary<AppSection, WebView2> _browserPool = new();
    private readonly List<AppSection> _lruOrder = new();
    private readonly HashSet<AppSection> _initializedTabs = new();
    private readonly SemaphoreSlim _envLock = new(1, 1);

    private Panel? _lastHostPanel;
    private CoreWebView2Environment? _sharedEnvironment;
    private bool _uBlockLoadedInProfile;
    private bool _disposed;

    public int ActivePoolCount => _browserPool.Count;

    public event EventHandler<string>? ActiveTitleChanged;
    public event EventHandler<bool>? LoadingStateChanged;
    public event EventHandler<bool>? AppSplashVisibilityChanged;
    public event EventHandler<bool>? FullscreenStateChanged;
    public event EventHandler? PoolCountChanged;

    private string _lastAutoFullscreenYoutubeId = string.Empty;
    private CancellationTokenSource? _ytFullscreenCts;

    public WebBrowserService(
        ErrorHandlingService errorService,
        NetworkMonitorService networkService,
        ConfigService configService)
    {
        _errorService = errorService;
        _networkService = networkService;
        _configService = configService;
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
                additionalBrowserArguments: "--autoplay-policy=no-user-gesture-required --enable-features=VaapiVideoDecoder,CanvasOopRasterization")
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

    public async Task PrewarmEnvironmentAndExtensionAsync(Panel hostPanel)
    {
        _lastHostPanel = hostPanel;
        try
        {
            var env = await GetOrCreateEnvironmentAsync();
            if (!_uBlockLoadedInProfile)
            {
                var warmupView = new WebView2
                {
                    Width = 1,
                    Height = 1,
                    Visibility = Visibility.Collapsed
                };
                hostPanel.Children.Add(warmupView);
                try
                {
                    await warmupView.EnsureCoreWebView2Async(env);
                    var uBlockDir = GetUBlockExtensionDirectory();
                    if (File.Exists(Path.Combine(uBlockDir, "manifest.json")))
                    {
                        try
                        {
                            await warmupView.CoreWebView2.Profile.AddBrowserExtensionAsync(uBlockDir);
                        }
                        catch
                        {
                        }
                        _uBlockLoadedInProfile = true;
                    }
                }
                finally
                {
                    hostPanel.Children.Remove(warmupView);
                    warmupView.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            _errorService.LogError(ErrorCategory.WebLoadFailed, "WebView2 background prewarm", ex);
        }
    }

    public async Task<WebView2> GetOrCreateTabAsync(AppSection section, string targetUrl, Panel hostPanel)
    {
        _lastHostPanel = hostPanel;
        TouchLru(section);
        PauseAndTrimBackgroundTabs(exceptSection: section);

        if (_browserPool.TryGetValue(section, out var existingWebView))
        {
            ShowOnlySelectedWebView(existingWebView);
            if (existingWebView.CoreWebView2 is not null)
            {
                existingWebView.CoreWebView2.MemoryUsageTargetLevel = CoreWebView2MemoryUsageTargetLevel.Normal;
                ApplyWebAudioEqualizerToCore(existingWebView.CoreWebView2, _configService.Current.SoundPreset);
            }

            LoadingStateChanged?.Invoke(this, false);
            AppSplashVisibilityChanged?.Invoke(this, false);
            if (!string.IsNullOrWhiteSpace(existingWebView.CoreWebView2?.DocumentTitle))
            {
                ActiveTitleChanged?.Invoke(this, existingWebView.CoreWebView2.DocumentTitle);
            }
            return existingWebView;
        }

        // Tự động thu hồi tab cũ nhất nếu vượt quá giới hạn tiết kiệm RAM thông minh
        EnforceSmartMemoryLimit(hostPanel, keepSection: section);

        var webView = new WebView2
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, 6, 9, 19),
            Visibility = Visibility.Visible
        };

        hostPanel.Children.Add(webView);
        _browserPool[section] = webView;
        PoolCountChanged?.Invoke(this, EventArgs.Empty);
        ShowOnlySelectedWebView(webView);

        try
        {
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

    private void TouchLru(AppSection section)
    {
        _lruOrder.Remove(section);
        _lruOrder.Add(section);
    }

    private void EnforceSmartMemoryLimit(Panel hostPanel, AppSection keepSection)
    {
        if (!_configService.Current.SmartMemorySaver)
        {
            return;
        }

        var maxTabs = Math.Clamp(_configService.Current.MaxCachedWebApps, 2, 10);
        while (_browserPool.Count >= maxTabs && _lruOrder.Count > 0)
        {
            var oldest = _lruOrder.FirstOrDefault(s => s != keepSection);
            if (oldest == default)
            {
                break;
            }

            _lruOrder.Remove(oldest);
            _initializedTabs.Remove(oldest);
            if (_browserPool.Remove(oldest, out var victim))
            {
                hostPanel.Children.Remove(victim);
                victim.Dispose();
                PoolCountChanged?.Invoke(this, EventArgs.Empty);
            }
        }
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

        await core.AddScriptToExecuteOnDocumentCreatedAsync(NativeAppModeInjectionScript);

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

        core.PermissionRequested += (_, args) =>
        {
            var kindName = args.PermissionKind.ToString();
            if (args.PermissionKind is CoreWebView2PermissionKind.Autoplay or CoreWebView2PermissionKind.ClipboardRead ||
                string.Equals(kindName, "ProtectedMediaIdentifier", StringComparison.OrdinalIgnoreCase))
            {
                args.State = CoreWebView2PermissionState.Allow;
            }
        };

        core.ContainsFullScreenElementChanged += (_, _) =>
        {
            if (webView.Visibility == Visibility.Visible)
            {
                FullscreenStateChanged?.Invoke(this, core.ContainsFullScreenElement);
            }
        };

        // Tự động phóng toàn màn hình bằng giao thức phần cứng Chromium DevTools (CDP Trusted Click) mỗi khi bấm mở video YouTube!
        core.SourceChanged += (_, _) =>
        {
            if (section == AppSection.YouTube && webView.Visibility == Visibility.Visible)
            {
                TriggerYouTubeAutoFullscreen(core);
            }
        };

        core.DOMContentLoaded += (_, _) =>
        {
            ApplyWebAudioEqualizerToCore(core, _configService.Current.SoundPreset);
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
            ApplyWebAudioEqualizerToCore(core, _configService.Current.SoundPreset);

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

    public void ApplyAudioPresetToAllTabs(AudioPreset preset)
    {
        foreach (var view in _browserPool.Values)
        {
            if (view.CoreWebView2 is not null)
            {
                ApplyWebAudioEqualizerToCore(view.CoreWebView2, preset);
            }
        }
    }

    private static void ApplyWebAudioEqualizerToCore(CoreWebView2 core, AudioPreset preset)
    {
        var (bass, mid, treble) = AudioPresetCatalog.GetWebThreeBandGains(preset);
        var js = $$"""
            (() => {
                window.__zenEqPreset = { bass: {{bass}}, mid: {{mid}}, treble: {{treble}} };
                const hookMedia = (el) => {
                    try {
                        if (!el.__zenEqNodes) {
                            const Ctx = window.AudioContext || window.webkitAudioContext;
                            if (!Ctx) return;
                            if (!window.__zenAudioCtx) window.__zenAudioCtx = new Ctx();
                            const ctx = window.__zenAudioCtx;
                            const src = ctx.createMediaElementSource(el);
                            const low = ctx.createBiquadFilter(); low.type = 'lowshelf'; low.frequency.value = 110;
                            const midF = ctx.createBiquadFilter(); midF.type = 'peaking'; midF.frequency.value = 1800; midF.Q.value = 1.0;
                            const high = ctx.createBiquadFilter(); high.type = 'highshelf'; high.frequency.value = 8500;
                            src.connect(low); low.connect(midF); midF.connect(high); high.connect(ctx.destination);
                            el.__zenEqNodes = { low, midF, high };
                        }
                        el.__zenEqNodes.low.gain.value = window.__zenEqPreset.bass;
                        el.__zenEqNodes.midF.gain.value = window.__zenEqPreset.mid;
                        el.__zenEqNodes.high.gain.value = window.__zenEqPreset.treble;
                    } catch (e) {}
                };
                document.querySelectorAll('video, audio').forEach(hookMedia);
                if (!window.__zenEqObserver) {
                    window.__zenEqObserver = new MutationObserver(() => {
                        document.querySelectorAll('video, audio').forEach(hookMedia);
                    });
                    if (document.body) {
                        window.__zenEqObserver.observe(document.body, { childList: true, subtree: true });
                    }
                }
            })();
            """;

        try
        {
            _ = core.ExecuteScriptAsync(js);
        }
        catch
        {
        }
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

    private void PauseAndTrimBackgroundTabs(AppSection? exceptSection = null)
    {
        foreach (var (sec, view) in _browserPool)
        {
            if (exceptSection.HasValue && sec == exceptSection.Value)
            {
                continue;
            }

            try
            {
                if (view.CoreWebView2 is { } core)
                {
                    _ = core.ExecuteScriptAsync(
                        "document.querySelectorAll('video,audio').forEach(el => { try { el.pause(); } catch(e){} });");

                    if (_configService.Current.SmartMemorySaver)
                    {
                        // Thu hồi tới 70% RAM GPU/V8 của các ứng dụng nằm dưới nền mà vẫn giữ nguyên trạng thái trang!
                        core.MemoryUsageTargetLevel = CoreWebView2MemoryUsageTargetLevel.Low;
                    }
                }
            }
            catch
            {
            }
        }
    }

    public void ReleaseAllBackgroundTabs()
    {
        foreach (var sec in _browserPool.Keys.ToList())
        {
            if (_browserPool.Remove(sec, out var view))
            {
                _lastHostPanel?.Children.Remove(view);
                view.Dispose();
            }
        }

        _lruOrder.Clear();
        _initializedTabs.Clear();
        PoolCountChanged?.Invoke(this, EventArgs.Empty);

        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    public async Task ClearTemporaryWebDataAsync()
    {
        try
        {
            var sample = _browserPool.Values.FirstOrDefault(v => v.CoreWebView2 is not null);
            if (sample?.CoreWebView2?.Profile is { } profile)
            {
                await profile.ClearBrowsingDataAsync(
                    CoreWebView2BrowsingDataKinds.DiskCache | CoreWebView2BrowsingDataKinds.DownloadHistory);
            }
        }
        catch (Exception ex)
        {
            _errorService.LogError(ErrorCategory.UnexpectedSystemError, "ClearBrowsingDataAsync", ex);
        }
    }

    private void ShowOnlySelectedWebView(WebView2 active)
    {
        foreach (var item in _browserPool.Values)
        {
            item.Visibility = ReferenceEquals(item, active) ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void TriggerYouTubeAutoFullscreen(CoreWebView2 core)
    {
        var currentUrl = core.Source;
        if (string.IsNullOrWhiteSpace(currentUrl) || !Uri.TryCreate(currentUrl, UriKind.Absolute, out var uri))
        {
            return;
        }

        if (!uri.Host.EndsWith("youtube.com", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(uri.AbsolutePath, "/watch", StringComparison.OrdinalIgnoreCase))
        {
            _lastAutoFullscreenYoutubeId = string.Empty;
            _ytFullscreenCts?.Cancel();
            return;
        }

        var videoId = ExtractQueryParam(uri.Query, "v");
        if (string.IsNullOrWhiteSpace(videoId) ||
            string.Equals(videoId, _lastAutoFullscreenYoutubeId, StringComparison.Ordinal))
        {
            return;
        }

        _ytFullscreenCts?.Cancel();
        _ytFullscreenCts = new CancellationTokenSource();
        var token = _ytFullscreenCts.Token;

        _ = AutoClickYouTubeFullscreenViaCdpAsync(core, videoId, token);
    }

    private async Task AutoClickYouTubeFullscreenViaCdpAsync(CoreWebView2 core, string videoId, CancellationToken token)
    {
        const string locateFsButtonJs = """
            (() => {
                if (document.fullscreenElement || document.querySelector('.ytp-fullscreen')) return 'ALREADY_FS';
                const tag = document.activeElement ? document.activeElement.tagName : '';
                if (tag === 'INPUT' || tag === 'TEXTAREA' || document.activeElement?.isContentEditable) return 'TYPING';
                const btn = document.querySelector('.ytp-fullscreen-button');
                if (!btn) return 'WAIT';
                const r = btn.getBoundingClientRect();
                if (r.width < 4 || r.height < 4) return 'WAIT';
                return Math.round(r.left + r.width / 2) + ',' + Math.round(r.top + r.height / 2);
            })()
            """;

        try
        {
            for (int attempt = 0; attempt < 35; attempt++)
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }

                if (core.ContainsFullScreenElement)
                {
                    _lastAutoFullscreenYoutubeId = videoId;
                    return;
                }

                var rawResult = await core.ExecuteScriptAsync(locateFsButtonJs);
                var clean = rawResult?.Trim('"');

                if (clean == "ALREADY_FS")
                {
                    _lastAutoFullscreenYoutubeId = videoId;
                    return;
                }

                if (!string.IsNullOrWhiteSpace(clean) && clean.Contains(','))
                {
                    var parts = clean.Split(',');
                    if (parts.Length == 2 &&
                        int.TryParse(parts[0], out int x) &&
                        int.TryParse(parts[1], out int y))
                    {
                        // Gửi thao tác click chuột cấp độ trình điều khiển Chromium (isTrusted = true, có UserActivation thật 100%) mà không làm lệch con trỏ chuột ngoài đời!
                        await core.CallDevToolsProtocolMethodAsync(
                            "Input.dispatchMouseEvent",
                            $$"""{"type":"mouseMoved","x":{{x}},"y":{{y}}}""");
                        await core.CallDevToolsProtocolMethodAsync(
                            "Input.dispatchMouseEvent",
                            $$"""{"type":"mousePressed","x":{{x}},"y":{{y}},"button":"left","clickCount":1}""");
                        await core.CallDevToolsProtocolMethodAsync(
                            "Input.dispatchMouseEvent",
                            $$"""{"type":"mouseReleased","x":{{x}},"y":{{y}},"button":"left","clickCount":1}""");

                        await Task.Delay(280, token);
                        if (core.ContainsFullScreenElement)
                        {
                            _lastAutoFullscreenYoutubeId = videoId;
                            return;
                        }

                        // Dự phòng tầng 2: Nếu có lớp thông báo che nút góc dưới phải, kích hoạt phím tắt 'f' chuẩn của trình phát YouTube qua giao thức CDP Trusted Key!
                        _ = await core.ExecuteScriptAsync("document.getElementById('movie_player')?.focus();");
                        await core.CallDevToolsProtocolMethodAsync(
                            "Input.dispatchKeyEvent",
                            """{"type":"rawKeyDown","windowsVirtualKeyCode":70,"nativeVirtualKeyCode":70,"key":"f","code":"KeyF"}""");
                        await core.CallDevToolsProtocolMethodAsync(
                            "Input.dispatchKeyEvent",
                            """{"type":"char","text":"f","unmodifiedText":"f","key":"f","code":"KeyF"}""");
                        await core.CallDevToolsProtocolMethodAsync(
                            "Input.dispatchKeyEvent",
                            """{"type":"keyUp","windowsVirtualKeyCode":70,"nativeVirtualKeyCode":70,"key":"f","code":"KeyF"}""");

                        await Task.Delay(280, token);
                        if (core.ContainsFullScreenElement)
                        {
                            _lastAutoFullscreenYoutubeId = videoId;
                            return;
                        }
                    }
                }

                await Task.Delay(220, token);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _errorService.LogError(ErrorCategory.WebLoadFailed, "AutoClickYouTubeFullscreenViaCdpAsync", ex);
        }
    }

    private static string? ExtractQueryParam(string query, string key)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        var trimmed = query.TrimStart('?');
        foreach (var pair in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = pair.Split('=', 2);
            if (kv.Length == 2 && string.Equals(kv[0], key, StringComparison.OrdinalIgnoreCase))
            {
                return Uri.UnescapeDataString(kv[1]);
            }
        }

        return null;
    }

    public void HideAllTabs()
    {
        FullscreenStateChanged?.Invoke(this, false);
        AppSplashVisibilityChanged?.Invoke(this, false);
        PauseAndTrimBackgroundTabs();
        foreach (var item in _browserPool.Values)
        {
            item.Visibility = Visibility.Collapsed;
        }
    }

    public bool TryNavigateActiveTabBack()
    {
        var active = _browserPool.Values.FirstOrDefault(v => v.Visibility == Visibility.Visible);
        if (active?.CoreWebView2 is not { } core)
        {
            return false;
        }

        // Nếu đang toàn màn hình video (VD: YouTube), thoát toàn màn hình đồng thời quay về danh sách video chỉ với 1 lần bấm!
        if (core.ContainsFullScreenElement)
        {
            _ = core.ExecuteScriptAsync("if (document.fullscreenElement) { document.exitFullscreen().catch(()=>{}); }");
            if (core.CanGoBack)
            {
                core.GoBack();
            }
            return true;
        }

        if (core.CanGoBack)
        {
            core.GoBack();
            return true;
        }

        return false;
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
