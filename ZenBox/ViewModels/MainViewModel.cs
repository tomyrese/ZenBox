using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZenBox.Models;
using ZenBox.Services;

namespace ZenBox.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly ErrorHandlingService _errorService;
    private readonly ConfigService _configService;
    private readonly NetworkMonitorService _networkService;
    private readonly SystemAudioService _audioService;
    private readonly WebBrowserService _webBrowserService;
    private readonly MediaLibraryService _mediaLibraryService;
    private readonly RemoteControlServer _remoteServer;
    private readonly DispatcherTimer _clockTimer;

    private bool _disposed;

    public HomeViewModel HomeVm { get; }
    public WebContainerViewModel WebVm { get; }
    public MediaLibraryViewModel MediaLibraryVm { get; }
    public MediaPlayerViewModel MediaPlayerVm { get; }
    public SettingsViewModel SettingsVm { get; }
    public ConfigService ConfigService => _configService;

    [ObservableProperty]
    private AppSection _activeSection = AppSection.Home;

    [ObservableProperty]
    private string _clockTimeText = string.Empty;

    [ObservableProperty]
    private string _vietnameseDateText = string.Empty;

    [ObservableProperty]
    private bool _isNetworkConnected;

    [ObservableProperty]
    private string _networkStatusLabel = "Đã kết nối mạng";

    [ObservableProperty]
    private VietnameseErrorInfo? _activeError;

    [ObservableProperty]
    private bool _isErrorVisible;

    public bool IsHomeVisible => ActiveSection == AppSection.Home;
    public bool IsNotHome => ActiveSection != AppSection.Home;

    // Khi hộp thoại báo lỗi xuất hiện, tạm ẩn các vùng HWND (WebView2 / VideoView) để hộp thoại Glass luôn hiển thị rõ nhất
    public bool IsWebSectionVisible =>
        !IsErrorVisible &&
        ActiveSection is AppSection.YouTube or AppSection.Facebook or AppSection.TikTok or AppSection.WebBrowser;

    public bool IsMediaLibraryVisible =>
        ActiveSection is AppSection.OfflineMovies or AppSection.OfflineMusic;

    public bool IsMediaPlayerVisible =>
        !IsErrorVisible && ActiveSection == AppSection.MediaPlayer;

    public object? ActiveNonWebViewModel => ActiveSection switch
    {
        AppSection.Home => HomeVm,
        AppSection.OfflineMovies or AppSection.OfflineMusic => MediaLibraryVm,
        AppSection.MediaPlayer => IsErrorVisible ? null : MediaPlayerVm,
        AppSection.SystemConfig => SettingsVm,
        _ => null
    };

    public bool IsNonWebHostVisible => ActiveNonWebViewModel is not null;

    public MainViewModel(
        ErrorHandlingService errorService,
        ConfigService configService,
        NetworkMonitorService networkService,
        SystemAudioService audioService,
        WebBrowserService webBrowserService,
        MediaLibraryService mediaLibraryService)
    {
        _errorService = errorService;
        _configService = configService;
        _networkService = networkService;
        _audioService = audioService;
        _webBrowserService = webBrowserService;
        _mediaLibraryService = mediaLibraryService;

        HomeVm = new HomeViewModel(OnTileSelected);
        WebVm = new WebContainerViewModel(_webBrowserService, NavigateHome);
        MediaLibraryVm = new MediaLibraryViewModel(
            _mediaLibraryService,
            _configService,
            _errorService,
            PlayMediaItem,
            () => ActivateSection(AppSection.SystemConfig),
            NavigateHome);
        MediaPlayerVm = new MediaPlayerViewModel(
            _errorService,
            wasVideo =>
            {
                if (wasVideo)
                {
                    OpenOfflineMovies();
                }
                else
                {
                    OpenOfflineMusic();
                }
            });
        SettingsVm = new SettingsViewModel(_configService, NavigateHome);

        _errorService.ErrorReported += OnErrorReported;
        _networkService.NetworkStatusChanged += (_, online) =>
        {
            Application.Current?.Dispatcher?.InvokeAsync(() => ApplyNetworkState(online));
        };

        ApplyNetworkState(_networkService.IsOnline);
        UpdateClockAndDate();

        _clockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += (_, _) =>
        {
            UpdateClockAndDate();
            _networkService.RefreshStatus();
        };
        _clockTimer.Start();

        _remoteServer = new RemoteControlServer(
            _configService.Current.RemotePort,
            _errorService,
            HandleRemoteCommand);
        _remoteServer.Start();
    }

    partial void OnActiveSectionChanged(AppSection value)
    {
        NotifyVisibilityProperties();
    }

    partial void OnIsErrorVisibleChanged(bool value)
    {
        NotifyVisibilityProperties();
    }

    private void NotifyVisibilityProperties()
    {
        OnPropertyChanged(nameof(IsHomeVisible));
        OnPropertyChanged(nameof(IsNotHome));
        OnPropertyChanged(nameof(IsWebSectionVisible));
        OnPropertyChanged(nameof(IsMediaLibraryVisible));
        OnPropertyChanged(nameof(IsMediaPlayerVisible));
        OnPropertyChanged(nameof(ActiveNonWebViewModel));
        OnPropertyChanged(nameof(IsNonWebHostVisible));
    }

    private void UpdateClockAndDate()
    {
        var now = DateTime.Now;
        ClockTimeText = now.ToString("HH:mm");
        VietnameseDateText = FormatVietnameseDate(now);
    }

    public static string FormatVietnameseDate(DateTime dateTime)
    {
        var dayName = dateTime.DayOfWeek switch
        {
            DayOfWeek.Monday => "Thứ Hai",
            DayOfWeek.Tuesday => "Thứ Ba",
            DayOfWeek.Wednesday => "Thứ Tư",
            DayOfWeek.Thursday => "Thứ Năm",
            DayOfWeek.Friday => "Thứ Sáu",
            DayOfWeek.Saturday => "Thứ Bảy",
            _ => "Chủ Nhật"
        };

        return $"{dayName}, {dateTime:dd/MM/yyyy}";
    }

    private void ApplyNetworkState(bool online)
    {
        IsNetworkConnected = online;
        NetworkStatusLabel = online ? "Đã kết nối mạng" : "Mất kết nối mạng";
    }

    private void OnErrorReported(object? sender, VietnameseErrorInfo info)
    {
        if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            _ = dispatcher.InvokeAsync(() => ShowErrorDialog(info));
            return;
        }

        ShowErrorDialog(info);
    }

    private void ShowErrorDialog(VietnameseErrorInfo info)
    {
        ActiveError = info;
        IsErrorVisible = true;
    }

    private void OnTileSelected(AppTileItem tile)
    {
        if (tile.RequiresInternet && !string.IsNullOrWhiteSpace(tile.TargetUrl))
        {
            OpenOnlineChannel(tile.Section, tile.TargetUrl, tile.Title);
            return;
        }

        switch (tile.Section)
        {
            case AppSection.OfflineMovies:
                OpenOfflineMovies();
                break;
            case AppSection.OfflineMusic:
                OpenOfflineMusic();
                break;
            case AppSection.SystemConfig:
                ActivateSection(AppSection.SystemConfig);
                break;
            default:
                NavigateHome();
                break;
        }
    }

    public void OpenOnlineChannel(AppSection section, string url, string title)
    {
        IsErrorVisible = false;
        _networkService.RefreshStatus();

        if (!_networkService.IsOnline)
        {
            _errorService.ReportError(
                ErrorCategory.NetworkOffline,
                $"Attempted to open {title} ({url}) while offline",
                null,
                () => OpenOnlineChannel(section, url, title));
            return;
        }

        MediaPlayerVm.StopPlayback();
        ActivateSection(section);
        WebVm.OpenChannel(section, url, title);
    }

    [RelayCommand]
    public void NavigateHome()
    {
        IsErrorVisible = false;
        MediaPlayerVm.StopPlayback();
        _webBrowserService.HideAllTabs();
        ActivateSection(AppSection.Home);
    }

    [RelayCommand]
    public void OpenYouTube()
    {
        OpenOnlineChannel(AppSection.YouTube, "https://www.youtube.com", "YouTube");
    }

    [RelayCommand]
    public void OpenFacebook()
    {
        OpenOnlineChannel(AppSection.Facebook, "https://www.facebook.com", "Facebook");
    }

    [RelayCommand]
    public void OpenTikTok()
    {
        OpenOnlineChannel(AppSection.TikTok, "https://www.tiktok.com", "TikTok");
    }

    [RelayCommand]
    public void OpenWebBrowser()
    {
        OpenOnlineChannel(AppSection.WebBrowser, "https://www.google.com", "Trình duyệt Web");
    }

    [RelayCommand]
    public void OpenOfflineMovies()
    {
        IsErrorVisible = false;
        MediaPlayerVm.StopPlayback();
        _webBrowserService.HideAllTabs();
        ActivateSection(AppSection.OfflineMovies);
        _ = MediaLibraryVm.LoadLibraryAsync(videoMode: true);
    }

    [RelayCommand]
    public void OpenOfflineMusic()
    {
        IsErrorVisible = false;
        MediaPlayerVm.StopPlayback();
        _webBrowserService.HideAllTabs();
        ActivateSection(AppSection.OfflineMusic);
        _ = MediaLibraryVm.LoadLibraryAsync(videoMode: false);
    }

    private void PlayMediaItem(MediaItem item)
    {
        IsErrorVisible = false;
        _webBrowserService.HideAllTabs();
        ActivateSection(AppSection.MediaPlayer);
        MediaPlayerVm.PlayMedia(item);
    }

    private void ActivateSection(AppSection section)
    {
        ActiveSection = section;
    }

    [RelayCommand]
    public void VolumeUp()
    {
        _audioService.VolumeUp();
    }

    [RelayCommand]
    public void VolumeDown()
    {
        _audioService.VolumeDown();
    }

    [RelayCommand]
    public void RetryErrorAction()
    {
        var retry = ActiveError?.RetryAction;
        IsErrorVisible = false;
        ActiveError = null;
        retry?.Invoke();
    }

    [RelayCommand]
    public void DismissErrorToHome()
    {
        IsErrorVisible = false;
        ActiveError = null;
        NavigateHome();
    }

    [RelayCommand]
    public void DismissErrorToOfflineMovies()
    {
        IsErrorVisible = false;
        ActiveError = null;
        OpenOfflineMovies();
    }

    [RelayCommand]
    public void ExitApplication()
    {
        Application.Current?.Shutdown();
    }

    public void HandleRemoteCommand(RemoteCommandPayload payload)
    {
        var action = payload.Action?.Trim().ToUpperInvariant();
        switch (action)
        {
            case "HOME":
                NavigateHome();
                break;
            case "VOL_UP":
                VolumeUp();
                break;
            case "VOL_DOWN":
                VolumeDown();
                break;
            case "PLAY_PAUSE":
                if (ActiveSection == AppSection.MediaPlayer)
                {
                    MediaPlayerVm.TogglePlayPause();
                }
                break;
            case "OPEN_APP":
                HandleRemoteOpenApp(payload.Target);
                break;
        }
    }

    private void HandleRemoteOpenApp(string? target)
    {
        var normalized = target?.Trim().ToUpperInvariant();
        switch (normalized)
        {
            case "YOUTUBE":
                OpenYouTube();
                break;
            case "FACEBOOK":
                OpenFacebook();
                break;
            case "TIKTOK":
                OpenTikTok();
                break;
            case "BROWSER":
            case "WEB":
                OpenOnlineChannel(AppSection.WebBrowser, "https://www.google.com", "Trình duyệt Web");
                break;
            case "MOVIES":
            case "PHIM":
                OpenOfflineMovies();
                break;
            case "MUSIC":
            case "NHAC":
                OpenOfflineMusic();
                break;
            case "CONFIG":
            case "CAI_DAT":
                ActivateSection(AppSection.SystemConfig);
                break;
            default:
                if (!string.IsNullOrWhiteSpace(target) &&
                    Uri.TryCreate(target, UriKind.Absolute, out var customUri) &&
                    (customUri.Scheme == Uri.UriSchemeHttp || customUri.Scheme == Uri.UriSchemeHttps))
                {
                    OpenOnlineChannel(AppSection.WebBrowser, customUri.ToString(), "Trình duyệt Web");
                }
                break;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _clockTimer.Stop();
        _remoteServer.Dispose();
        MediaPlayerVm.Dispose();
        _webBrowserService.Dispose();
        _networkService.Dispose();
    }
}
