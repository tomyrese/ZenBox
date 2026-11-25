using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZenBox.Models;
using ZenBox.Services;

namespace ZenBox.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private static readonly SolidColorBrush TransparentOverlay = CreateFrozenBrush(0, 0, 0, 0);
    private static readonly SolidColorBrush WarmSoftOverlay = CreateFrozenBrush(30, 255, 148, 48);
    private static readonly SolidColorBrush DeepNightOverlay = CreateFrozenBrush(56, 255, 126, 28);

    private readonly ErrorHandlingService _errorService;
    private readonly ConfigService _configService;
    private readonly NetworkMonitorService _networkService;
    private readonly SystemAudioService _audioService;
    private readonly WebBrowserService _webBrowserService;
    private readonly MediaLibraryService _mediaLibraryService;
    private readonly RemoteControlServer _remoteServer;
    private readonly DispatcherTimer _clockTimer;

    private DateTime? _sleepTargetUtc;
    private bool _updatingVolumeFromSync;
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

    [ObservableProperty]
    private Brush _eyeCareOverlayBrush = TransparentOverlay;

    [ObservableProperty]
    private bool _isEyeCareActive;

    [ObservableProperty]
    private string _quickAudioPresetLabel = "Chuẩn cân bằng";

    [ObservableProperty]
    private string _quickEyeCareLabel = "Bảo vệ mắt: Tắt";

    [ObservableProperty]
    private string _sleepTimerBadgeText = "Hẹn giờ: Tắt";

    [ObservableProperty]
    private bool _isSleepTimerActive;

    [ObservableProperty]
    private int _quickVolumePercent = 60;

    [ObservableProperty]
    private bool _isSystemMuted;

    public bool IsHomeVisible => ActiveSection == AppSection.Home;
    public bool IsNotHome => ActiveSection != AppSection.Home;

    public bool IsOnlineAppActive => ActiveSection is
        AppSection.YouTube or
        AppSection.Netflix or
        AppSection.Spotify or
        AppSection.TikTok or
        AppSection.Facebook or
        AppSection.FptPlay or
        AppSection.VtvGo or
        AppSection.ZingMp3 or
        AppSection.AppleMusic or
        AppSection.PrimeVideo or
        AppSection.Instagram or
        AppSection.SoundCloud or
        AppSection.Twitch or
        AppSection.WebBrowser;

    public bool IsSystemShellChromeVisible =>
        ActiveSection is AppSection.Home or AppSection.OfflineMovies or AppSection.OfflineMusic or AppSection.SystemConfig;

    public bool IsWebSectionVisible => !IsErrorVisible && IsOnlineAppActive;

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
            OpenSettings,
            NavigateHome);
        MediaPlayerVm = new MediaPlayerViewModel(
            _errorService,
            _configService,
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
        SettingsVm = new SettingsViewModel(
            _configService,
            _audioService,
            _webBrowserService,
            ApplyGlobalAudioPreset,
            ApplyEyeCareMode,
            SetSleepTimerMinutes,
            NavigateHome);

        ApplyGlobalAudioPreset(_configService.Current.SoundPreset);
        ApplyEyeCareMode(_configService.Current.EyeCare);

        _errorService.ErrorReported += OnErrorReported;
        _networkService.NetworkStatusChanged += (_, online) =>
        {
            Application.Current?.Dispatcher?.InvokeAsync(() => ApplyNetworkState(online));
        };

        ApplyNetworkState(_networkService.IsOnline);
        UpdateClockAndTimers();

        _clockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += (_, _) => UpdateClockAndTimers();
        _clockTimer.Start();

        _remoteServer = new RemoteControlServer(
            _configService.Current.RemotePort,
            _errorService,
            HandleRemoteCommand);
    }

    private static SolidColorBrush CreateFrozenBrush(byte a, byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        brush.Freeze();
        return brush;
    }

    public void StartDeferredBackgroundServices()
    {
        _ = Task.Run(() => _remoteServer.Start());
        SyncLiveSystemVolume();
        WebVm.TriggerBackgroundWarmup();
    }

    public void SyncLiveSystemVolume()
    {
        _updatingVolumeFromSync = true;
        try
        {
            QuickVolumePercent = _audioService.GetMasterVolumePercent();
            IsSystemMuted = _audioService.IsMuted();
        }
        finally
        {
            _updatingVolumeFromSync = false;
        }
    }

    partial void OnQuickVolumePercentChanged(int value)
    {
        if (_updatingVolumeFromSync)
        {
            return;
        }

        _audioService.SetMasterVolumePercent(value);
        IsSystemMuted = false;
    }

    partial void OnActiveSectionChanged(AppSection value)
    {
        NotifyVisibilityProperties();
        if (value == AppSection.SystemConfig)
        {
            SettingsVm.RefreshSystemDiagnostics();
        }
    }

    partial void OnIsErrorVisibleChanged(bool value)
    {
        NotifyVisibilityProperties();
    }

    private void NotifyVisibilityProperties()
    {
        OnPropertyChanged(nameof(IsHomeVisible));
        OnPropertyChanged(nameof(IsNotHome));
        OnPropertyChanged(nameof(IsOnlineAppActive));
        OnPropertyChanged(nameof(IsSystemShellChromeVisible));
        OnPropertyChanged(nameof(IsWebSectionVisible));
        OnPropertyChanged(nameof(ActiveNonWebViewModel));
        OnPropertyChanged(nameof(IsNonWebHostVisible));
    }

    private void UpdateClockAndTimers()
    {
        var now = DateTime.Now;
        ClockTimeText = now.ToString("HH:mm");
        VietnameseDateText = FormatVietnameseDate(now);

        if (_sleepTargetUtc.HasValue)
        {
            var remaining = _sleepTargetUtc.Value - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                _sleepTargetUtc = null;
                IsSleepTimerActive = false;
                MediaPlayerVm.StopPlayback();
                _webBrowserService.HideAllTabs();
                ExitApplication();
                return;
            }

            var mins = (int)remaining.TotalMinutes;
            var secs = remaining.Seconds;
            var countdown = $"{mins:00}:{secs:00}";
            SleepTimerBadgeText = $"Hẹn tắt: {countdown}";
            SettingsVm.UpdateSleepStatusDisplay($"Tự động tắt sau {countdown}");
        }
    }

    public void ApplyGlobalAudioPreset(AudioPreset preset)
    {
        QuickAudioPresetLabel = AudioPresetCatalog.GetDisplayName(preset);
        MediaPlayerVm.ApplyEqualizerPreset(preset);
        _webBrowserService.ApplyAudioPresetToAllTabs(preset);
        SettingsVm.SyncSelectedPresetFromExternal(preset);
    }

    public void ApplyEyeCareMode(EyeCareMode mode)
    {
        switch (mode)
        {
            case EyeCareMode.WarmSoft:
                EyeCareOverlayBrush = WarmSoftOverlay;
                IsEyeCareActive = true;
                QuickEyeCareLabel = "Mắt: Ấm dịu";
                break;
            case EyeCareMode.DeepNight:
                EyeCareOverlayBrush = DeepNightOverlay;
                IsEyeCareActive = true;
                QuickEyeCareLabel = "Mắt: Ban đêm";
                break;
            default:
                EyeCareOverlayBrush = TransparentOverlay;
                IsEyeCareActive = false;
                QuickEyeCareLabel = "Mắt: Chuẩn";
                break;
        }

        SettingsVm.SyncEyeCareFromExternal(mode);
    }

    public void SetSleepTimerMinutes(int minutes)
    {
        if (minutes <= 0)
        {
            _sleepTargetUtc = null;
            IsSleepTimerActive = false;
            SleepTimerBadgeText = "Hẹn giờ: Tắt";
            SettingsVm.SyncSleepTimerMinutesFromExternal(0, "Đang tắt");
        }
        else
        {
            _sleepTargetUtc = DateTime.UtcNow.AddMinutes(minutes);
            IsSleepTimerActive = true;
            SettingsVm.SyncSleepTimerMinutesFromExternal(minutes, $"Đang hẹn {minutes} phút");
            UpdateClockAndTimers();
        }
    }

    [RelayCommand]
    public void CycleQuickAudioPreset()
    {
        var values = Enum.GetValues<AudioPreset>();
        var next = values[(Array.IndexOf(values, _configService.Current.SoundPreset) + 1) % values.Length];
        _configService.Current.SoundPreset = next;
        _configService.Save();
        ApplyGlobalAudioPreset(next);
    }

    [RelayCommand]
    public void CycleQuickEyeCare()
    {
        var values = Enum.GetValues<EyeCareMode>();
        var next = values[(Array.IndexOf(values, _configService.Current.EyeCare) + 1) % values.Length];
        _configService.Current.EyeCare = next;
        _configService.Save();
        ApplyEyeCareMode(next);
    }

    [RelayCommand]
    public void CycleQuickSleepTimer()
    {
        int[] steps = [0, 30, 60, 90];
        int currentMinutes = 0;
        if (_sleepTargetUtc.HasValue)
        {
            var rem = (_sleepTargetUtc.Value - DateTime.UtcNow).TotalMinutes;
            currentMinutes = rem <= 35 ? 30 : rem <= 65 ? 60 : 90;
        }

        var nextIdx = (Array.IndexOf(steps, currentMinutes) + 1) % steps.Length;
        SetSleepTimerMinutes(steps[nextIdx]);
    }

    public void SetSleepTimerFromSettings(int minutes)
    {
        SetSleepTimerMinutes(minutes);
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
                OpenSettings();
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
    public void OpenSettings()
    {
        IsErrorVisible = false;
        MediaPlayerVm.StopPlayback();
        _webBrowserService.HideAllTabs();
        ActivateSection(AppSection.SystemConfig);
    }

    [RelayCommand]
    public void OpenYouTube() => OpenOnlineChannel(AppSection.YouTube, "https://www.youtube.com", "YouTube");

    [RelayCommand]
    public void OpenNetflix() => OpenOnlineChannel(AppSection.Netflix, "https://www.netflix.com", "Netflix");

    [RelayCommand]
    public void OpenSpotify() => OpenOnlineChannel(AppSection.Spotify, "https://open.spotify.com", "Spotify");

    [RelayCommand]
    public void OpenTikTok() => OpenOnlineChannel(AppSection.TikTok, "https://www.tiktok.com", "TikTok");

    [RelayCommand]
    public void OpenFacebook() => OpenOnlineChannel(AppSection.Facebook, "https://www.facebook.com", "Facebook");

    [RelayCommand]
    public void OpenWebBrowser() => OpenOnlineChannel(AppSection.WebBrowser, "https://www.google.com", "Tìm kiếm Google");

    [RelayCommand]
    public void AppGoBack()
    {
        if (IsErrorVisible)
        {
            IsErrorVisible = false;
            ActiveError = null;
            return;
        }

        if (ActiveSection == AppSection.MediaPlayer)
        {
            MediaPlayerVm.BackToLibraryCommand.Execute(null);
            return;
        }

        if (IsOnlineAppActive)
        {
            // Nếu trang trong ứng dụng có lịch sử trước đó thì lùi 1 bước; nếu đang ở trang gốc của ứng dụng thì trở về ngay Trang Chủ!
            if (!_webBrowserService.TryNavigateActiveTabBack())
            {
                NavigateHome();
            }
            return;
        }

        if (ActiveSection != AppSection.Home)
        {
            NavigateHome();
        }
    }

    [RelayCommand]
    public void AppReload()
    {
        if (IsOnlineAppActive)
        {
            _webBrowserService.ReloadActiveTab();
        }
    }

    [RelayCommand]
    public void ToggleSystemMute()
    {
        _audioService.ToggleMute();
        IsSystemMuted = _audioService.IsMuted();
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
        SyncLiveSystemVolume();
    }

    [RelayCommand]
    public void VolumeDown()
    {
        _audioService.VolumeDown();
        SyncLiveSystemVolume();
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
            case "NETFLIX":
                OpenNetflix();
                break;
            case "SPOTIFY":
                OpenSpotify();
                break;
            case "FACEBOOK":
                OpenFacebook();
                break;
            case "TIKTOK":
                OpenTikTok();
                break;
            case "FPT":
            case "FPTPLAY":
                OpenOnlineChannel(AppSection.FptPlay, "https://fptplay.vn", "FPT Play");
                break;
            case "VTV":
            case "VTVGO":
                OpenOnlineChannel(AppSection.VtvGo, "https://vtvgo.vn", "VTV Go");
                break;
            case "ZING":
            case "ZINGMP3":
                OpenOnlineChannel(AppSection.ZingMp3, "https://zingmp3.vn", "Zing MP3");
                break;
            case "APPLEMUSIC":
                OpenOnlineChannel(AppSection.AppleMusic, "https://music.apple.com", "Apple Music");
                break;
            case "PRIME":
            case "PRIMEVIDEO":
                OpenOnlineChannel(AppSection.PrimeVideo, "https://www.primevideo.com", "Prime Video");
                break;
            case "INSTAGRAM":
                OpenOnlineChannel(AppSection.Instagram, "https://www.instagram.com", "Instagram");
                break;
            case "SOUNDCLOUD":
                OpenOnlineChannel(AppSection.SoundCloud, "https://soundcloud.com", "SoundCloud");
                break;
            case "TWITCH":
                OpenOnlineChannel(AppSection.Twitch, "https://www.twitch.tv", "Twitch");
                break;
            case "BROWSER":
            case "WEB":
                OpenWebBrowser();
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
                OpenSettings();
                break;
            default:
                if (!string.IsNullOrWhiteSpace(target) &&
                    Uri.TryCreate(target, UriKind.Absolute, out var customUri) &&
                    (customUri.Scheme == Uri.UriSchemeHttp || customUri.Scheme == Uri.UriSchemeHttps))
                {
                    OpenOnlineChannel(AppSection.WebBrowser, customUri.ToString(), "Tìm kiếm Google");
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
