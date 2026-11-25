using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using ZenBox.Models;
using ZenBox.Services;

namespace ZenBox.ViewModels;

public sealed partial class SoundPresetOption : ObservableObject
{
    public required AudioPreset Preset { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required Geometry IconGeometry { get; init; }
    public required Brush IconBrush { get; init; }
    public required Brush IconBgBrush { get; init; }

    [ObservableProperty]
    private bool _isSelected;
}

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ConfigService _configService;
    private readonly SystemAudioService _audioService;
    private readonly WebBrowserService _webBrowserService;
    private readonly Action<AudioPreset> _onAudioPresetChanged;
    private readonly Action<EyeCareMode> _onEyeCareChanged;
    private readonly Action<int> _onSleepTimerSelected;
    private readonly Action _goHomeCallback;

    [ObservableProperty]
    private int _activeCategoryIndex;

    [ObservableProperty]
    private int _masterVolumePercent;

    [ObservableProperty]
    private bool _isMuted;

    [ObservableProperty]
    private string _selectedAspectRatio;

    [ObservableProperty]
    private double _selectedPlaybackSpeed;

    [ObservableProperty]
    private bool _autoResumeVideos;

    [ObservableProperty]
    private EyeCareMode _selectedEyeCare;

    [ObservableProperty]
    private bool _smartMemorySaver;

    [ObservableProperty]
    private int _maxCachedWebApps;

    [ObservableProperty]
    private int _selectedSleepMinutes;

    [ObservableProperty]
    private string _moviesFolderPath;

    [ObservableProperty]
    private string _musicFolderPath;

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private string _ramUsageText = "Đang đo...";

    [ObservableProperty]
    private string _activeBackgroundAppsText = "0 ứng dụng đang giữ nền";

    [ObservableProperty]
    private string _remoteEndpointDisplay = string.Empty;

    [ObservableProperty]
    private string _connectedPhonesText = "Sẵn sàng nhận kết nối từ nhiều điện thoại";

    [ObservableProperty]
    private string _firewallStatusText = string.Empty;

    [ObservableProperty]
    private bool _isFirewallBlocked;

    [ObservableProperty]
    private string _sleepTimerStatusText = "Đang tắt";

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasStatusMessage;

    public ObservableCollection<SoundPresetOption> SoundPresets { get; } = [];

    // Trạng thái chọn chuyên mục bên trái
    public bool IsAudioCategory => ActiveCategoryIndex == 0;
    public bool IsCinemaCategory => ActiveCategoryIndex == 1;
    public bool IsPerformanceCategory => ActiveCategoryIndex == 2;
    public bool IsSystemCategory => ActiveCategoryIndex == 3;

    // Trạng thái nút đang chọn của Chế độ Bảo vệ mắt
    public bool IsEyeCareOff => SelectedEyeCare == EyeCareMode.Off;
    public bool IsEyeCareWarmSoft => SelectedEyeCare == EyeCareMode.WarmSoft;
    public bool IsEyeCareDeepNight => SelectedEyeCare == EyeCareMode.DeepNight;

    // Trạng thái nút đang chọn của Tỷ lệ khung hình
    public bool IsAspectAuto => string.Equals(SelectedAspectRatio, "AUTO", StringComparison.OrdinalIgnoreCase);
    public bool IsAspect169 => string.Equals(SelectedAspectRatio, "16:9", StringComparison.OrdinalIgnoreCase);
    public bool IsAspect219 => string.Equals(SelectedAspectRatio, "21:9", StringComparison.OrdinalIgnoreCase);

    // Trạng thái nút đang chọn của Tốc độ phát
    public bool IsSpeed075 => Math.Abs(SelectedPlaybackSpeed - 0.75) < 0.05;
    public bool IsSpeed100 => Math.Abs(SelectedPlaybackSpeed - 1.0) < 0.05;
    public bool IsSpeed125 => Math.Abs(SelectedPlaybackSpeed - 1.25) < 0.05;
    public bool IsSpeed150 => Math.Abs(SelectedPlaybackSpeed - 1.5) < 0.05;

    // Trạng thái nút đang chọn của Giới hạn ứng dụng giữ nền
    public bool IsMaxApps2 => MaxCachedWebApps == 2;
    public bool IsMaxApps4 => MaxCachedWebApps == 4;
    public bool IsMaxApps6 => MaxCachedWebApps == 6;
    public bool IsMaxApps8 => MaxCachedWebApps == 8;

    // Trạng thái nút đang chọn của Hẹn giờ tự nghỉ
    public bool IsSleep0 => SelectedSleepMinutes == 0;
    public bool IsSleep15 => SelectedSleepMinutes == 15;
    public bool IsSleep30 => SelectedSleepMinutes == 30;
    public bool IsSleep60 => SelectedSleepMinutes == 60;
    public bool IsSleep90 => SelectedSleepMinutes == 90;

    public string CurrentEyeCareLabel => SelectedEyeCare switch
    {
        EyeCareMode.WarmSoft => "Dịu nhẹ ấm áp",
        EyeCareMode.DeepNight => "Ấm sâu ban đêm",
        _ => "Đang tắt (Màu chuẩn)"
    };

    public string CurrentAspectDisplay => SelectedAspectRatio switch
    {
        "16:9" => "Tràn màn hình 16:9",
        "21:9" => "Điện ảnh rộng 21:9",
        _ => "Tự động theo phim gốc"
    };

    public string CurrentSpeedDisplay => $"{SelectedPlaybackSpeed:0.##}x";

    public SettingsViewModel(
        ConfigService configService,
        SystemAudioService audioService,
        WebBrowserService webBrowserService,
        Action<AudioPreset> onAudioPresetChanged,
        Action<EyeCareMode> onEyeCareChanged,
        Action<int> onSleepTimerSelected,
        Action goHomeCallback)
    {
        _configService = configService;
        _audioService = audioService;
        _webBrowserService = webBrowserService;
        _onAudioPresetChanged = onAudioPresetChanged;
        _onEyeCareChanged = onEyeCareChanged;
        _onSleepTimerSelected = onSleepTimerSelected;
        _goHomeCallback = goHomeCallback;

        var current = _configService.Current;
        _moviesFolderPath = current.MoviesFolderPath;
        _musicFolderPath = current.MusicFolderPath;
        _startWithWindows = current.StartWithWindows;
        _selectedAspectRatio = string.IsNullOrWhiteSpace(current.DefaultAspectRatio) ? "AUTO" : current.DefaultAspectRatio;
        _selectedPlaybackSpeed = current.DefaultPlaybackSpeed <= 0 ? 1.0 : current.DefaultPlaybackSpeed;
        _autoResumeVideos = current.AutoResumeVideos;
        _selectedEyeCare = current.EyeCare;
        _smartMemorySaver = current.SmartMemorySaver;
        _maxCachedWebApps = Math.Clamp(current.MaxCachedWebApps, 2, 8);
        _masterVolumePercent = _audioService.GetMasterVolumePercent();
        _isMuted = _audioService.IsMuted();

        foreach (var preset in Enum.GetValues<AudioPreset>())
        {
            var (geo, iconHex, bgHex) = GetPresetVisuals(preset);
            SoundPresets.Add(new SoundPresetOption
            {
                Preset = preset,
                Title = AudioPresetCatalog.GetDisplayName(preset),
                Description = AudioPresetCatalog.GetDescription(preset),
                IconGeometry = geo,
                IconBrush = CreateBrush(iconHex),
                IconBgBrush = CreateBrush(bgHex),
                IsSelected = preset == current.SoundPreset
            });
        }

        _webBrowserService.PoolCountChanged += (_, _) => RefreshSystemDiagnostics();
        RefreshSystemDiagnostics();
    }

    private static SolidColorBrush CreateBrush(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }

    private static (Geometry Geo, string IconHex, string BgHex) GetPresetVisuals(AudioPreset preset) => preset switch
    {
        AudioPreset.Balanced => (
            Geometry.Parse("M3,17 L3,19 L9,19 L9,17 Z M3,5 L3,7 L13,7 L13,5 Z M13,21 L13,19 L21,19 L21,17 L13,17 L13,15 L11,15 L11,21 Z M7,9 L7,11 L3,11 L3,13 L7,13 L7,15 L9,15 L9,9 Z M21,13 L21,11 L11,11 L11,13 Z M15,9 L17,9 L17,7 L21,7 L21,5 L17,5 L17,3 L15,3 Z"),
            "#38BDF8",
            "#2438BDF8"),
        AudioPreset.CinemaSurround => (
            Geometry.Parse("M18,4 L20,8 L17,8 L15,4 L13,4 L15,8 L12,8 L10,4 L8,4 L10,8 L7,8 L5,4 L4,4 C2.9,4 2,4.9 2,6 L2,18 C2,19.1 2.9,20 4,20 L20,20 C21.1,20 22,19.1 22,18 L22,4 Z M10,17.5 L10,10.5 L16,14 Z"),
            "#FBBF24",
            "#24FBBF24"),
        AudioPreset.ClearVoice => (
            Geometry.Parse("M12,14 C13.66,14 15,12.66 15,11 L15,5 C15,3.34 13.66,2 12,2 C10.34,2 9,3.34 9,5 L9,11 C9,12.66 10.34,14 12,14 Z M17.91,11 C17.42,11 17.01,11.36 16.93,11.85 C16.52,14.2 14.47,16 12,16 C9.53,16 7.48,14.2 7.07,11.85 C6.99,11.36 6.58,11 6.09,11 C5.48,11 5.01,11.54 5.1,12.14 C5.59,15.14 7.97,17.51 11,17.92 L11,21 L13,21 L13,17.92 C16.03,17.51 18.41,15.14 18.9,12.14 C18.99,11.54 18.52,11 17.91,11 Z"),
            "#34D399",
            "#2434D399"),
        AudioPreset.WarmBolero => (
            Geometry.Parse("M12,3 L12,13.55 C11.41,13.21 10.73,13 10,13 C7.79,13 6,14.79 6,17 C6,19.21 7.79,21 10,21 C12.21,21 14,19.21 14,17 L14,7 L18,7 L18,3 Z"),
            "#F472B6",
            "#24F472B6"),
        AudioPreset.BassBoost => (
            Geometry.Parse("M17,2 L7,2 C5.9,2 5,2.9 5,4 L5,20 C5,21.1 5.9,22 7,22 L17,22 C18.1,22 19,21.1 19,20 L19,4 C19,2.9 18.1,2 17,2 Z M12,5 C13.1,5 14,5.9 14,7 C14,8.1 13.1,9 12,9 C10.9,9 10,8.1 10,7 C10,5.9 10.9,5 12,5 Z M12,19 C9.79,19 8,17.21 8,15 C8,12.79 9.79,11 12,11 C14.21,11 16,12.79 16,15 C16,17.21 14.21,19 12,19 Z M12,13 C10.9,13 10,13.9 10,15 C10,16.1 10.9,17 12,17 C13.1,17 14,16.1 14,15 C14,13.9 13.1,13 12,13 Z"),
            "#A855F7",
            "#26A855F7"),
        _ => (
            Geometry.Parse("M12.3,4.9 C12.7,4.2 13,3.4 13,2.5 C13,2.3 13,2.2 13,2 C8.1,2.5 4.2,6.8 4.2,12 C4.2,17.5 8.7,22 14.2,22 C18.4,22 21.9,19.4 23.4,15.8 C22.8,16 22.1,16.1 21.4,16.1 C15.9,16.1 11.4,11.6 11.4,6.1 C11.4,5.7 11.8,5.3 12.3,4.9 Z"),
            "#FCD34D",
            "#24FCD34D")
    };

    public void RefreshSystemDiagnostics()
    {
        MasterVolumePercent = _audioService.GetMasterVolumePercent();
        IsMuted = _audioService.IsMuted();

        using var proc = Process.GetCurrentProcess();
        var ramMb = proc.WorkingSet64 / (1024.0 * 1024.0);
        RamUsageText = $"{ramMb:0} MB RAM";
        ActiveBackgroundAppsText = $"{_webBrowserService.ActivePoolCount} ứng dụng trực tuyến đang giữ sẵn";

        var allIps = NetworkAddressHelper.GetRealLanIpAddresses();
        var port = _configService.Current.RemotePort;
        RemoteEndpointDisplay = string.Join("  •  ", allIps.Select(ip => $"{ip}:{port}"));

        var allowed = FirewallHelperService.IsPortAllowedInFirewall(port);
        IsFirewallBlocked = !allowed;
        FirewallStatusText = allowed
            ? "Tường lửa Windows: Đã mở cổng kết nối cho mọi điện thoại cùng Wi-Fi"
            : "Tường lửa Windows đang chặn kết nối từ điện thoại — hãy bấm nút Mở cổng Wi-Fi bên cạnh";
    }

    public void UpdateConnectedPhonesCount(int count)
    {
        ConnectedPhonesText = count > 0
            ? $"Đang có {count} điện thoại cùng kết nối điều khiển trực tiếp"
            : "Sẵn sàng cho nhiều điện thoại cùng kết nối điều khiển";
    }

    [RelayCommand]
    private void OpenFirewallPort()
    {
        var ok = FirewallHelperService.RequestOpenFirewallPort(_configService.Current.RemotePort);
        RefreshSystemDiagnostics();
        ShowConfirmation(ok
            ? "Đã mở cổng Wi-Fi thành công! Giờ mọi điện thoại trong nhà đều có thể tự động dò tìm và điều khiển."
            : "Chưa cấp quyền Tường lửa Windows. Vui lòng bấm lại và chọn Yes trên hộp thoại xác nhận.");
    }

    partial void OnActiveCategoryIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsAudioCategory));
        OnPropertyChanged(nameof(IsCinemaCategory));
        OnPropertyChanged(nameof(IsPerformanceCategory));
        OnPropertyChanged(nameof(IsSystemCategory));
        RefreshSystemDiagnostics();
    }

    partial void OnSelectedEyeCareChanged(EyeCareMode value)
    {
        OnPropertyChanged(nameof(IsEyeCareOff));
        OnPropertyChanged(nameof(IsEyeCareWarmSoft));
        OnPropertyChanged(nameof(IsEyeCareDeepNight));
        OnPropertyChanged(nameof(CurrentEyeCareLabel));
    }

    partial void OnSelectedAspectRatioChanged(string value)
    {
        OnPropertyChanged(nameof(IsAspectAuto));
        OnPropertyChanged(nameof(IsAspect169));
        OnPropertyChanged(nameof(IsAspect219));
        OnPropertyChanged(nameof(CurrentAspectDisplay));
    }

    partial void OnSelectedPlaybackSpeedChanged(double value)
    {
        OnPropertyChanged(nameof(IsSpeed075));
        OnPropertyChanged(nameof(IsSpeed100));
        OnPropertyChanged(nameof(IsSpeed125));
        OnPropertyChanged(nameof(IsSpeed150));
        OnPropertyChanged(nameof(CurrentSpeedDisplay));
    }

    partial void OnMaxCachedWebAppsChanged(int value)
    {
        OnPropertyChanged(nameof(IsMaxApps2));
        OnPropertyChanged(nameof(IsMaxApps4));
        OnPropertyChanged(nameof(IsMaxApps6));
        OnPropertyChanged(nameof(IsMaxApps8));
    }

    partial void OnSelectedSleepMinutesChanged(int value)
    {
        OnPropertyChanged(nameof(IsSleep0));
        OnPropertyChanged(nameof(IsSleep15));
        OnPropertyChanged(nameof(IsSleep30));
        OnPropertyChanged(nameof(IsSleep60));
        OnPropertyChanged(nameof(IsSleep90));
    }

    partial void OnMasterVolumePercentChanged(int value)
    {
        _audioService.SetMasterVolumePercent(value);
        IsMuted = false;
    }

    partial void OnAutoResumeVideosChanged(bool value)
    {
        _configService.Current.AutoResumeVideos = value;
        _configService.Save();
    }

    partial void OnSmartMemorySaverChanged(bool value)
    {
        _configService.Current.SmartMemorySaver = value;
        _configService.Save();
        ShowConfirmation(value
            ? "Đã bật Tiết kiệm bộ nhớ thông minh cho ứng dụng chạy ngầm."
            : "Đã tắt giới hạn bộ nhớ ứng dụng ngầm.");
    }

    partial void OnStartWithWindowsChanged(bool value)
    {
        _configService.SetStartWithWindows(value);
        ShowConfirmation(value
            ? "Đã bật tự động mở ZenBox ngay khi khởi động Windows 11."
            : "Đã tắt khởi động cùng Windows 11.");
    }

    [RelayCommand]
    private void SelectCategory(string? indexText)
    {
        if (int.TryParse(indexText, out var idx))
        {
            ActiveCategoryIndex = idx;
            HasStatusMessage = false;
        }
    }

    [RelayCommand]
    private void SelectSoundPreset(SoundPresetOption? option)
    {
        if (option is null)
        {
            return;
        }

        foreach (var item in SoundPresets)
        {
            item.IsSelected = ReferenceEquals(item, option);
        }

        _configService.Current.SoundPreset = option.Preset;
        _configService.Save();
        _onAudioPresetChanged(option.Preset);
        ShowConfirmation($"Đã chuyển chế độ âm thanh sang: {option.Title}");
    }

    public void SyncSelectedPresetFromExternal(AudioPreset preset)
    {
        foreach (var item in SoundPresets)
        {
            item.IsSelected = item.Preset == preset;
        }
    }

    public void SyncEyeCareFromExternal(EyeCareMode mode)
    {
        SelectedEyeCare = mode;
    }

    public void SyncSleepTimerMinutesFromExternal(int minutes, string status)
    {
        SelectedSleepMinutes = minutes;
        SleepTimerStatusText = status;
    }

    public void UpdateSleepStatusDisplay(string status)
    {
        SleepTimerStatusText = status;
    }

    [RelayCommand]
    private void ToggleMute()
    {
        _audioService.ToggleMute();
        IsMuted = _audioService.IsMuted();
    }

    [RelayCommand]
    private void SetAspectRatio(string? ratio)
    {
        if (string.IsNullOrWhiteSpace(ratio))
        {
            return;
        }

        SelectedAspectRatio = ratio;
        _configService.Current.DefaultAspectRatio = ratio;
        _configService.Save();
        ShowConfirmation($"Đã đặt tỷ lệ khung hình mặc định: {CurrentAspectDisplay}");
    }

    [RelayCommand]
    private void SetPlaybackSpeed(string? speedText)
    {
        if (double.TryParse(speedText, System.Globalization.CultureInfo.InvariantCulture, out var speed))
        {
            SelectedPlaybackSpeed = speed;
            _configService.Current.DefaultPlaybackSpeed = speed;
            _configService.Save();
            ShowConfirmation($"Đã đặt tốc độ phát mặc định: {CurrentSpeedDisplay}");
        }
    }

    [RelayCommand]
    private void SetEyeCareMode(string? modeName)
    {
        if (Enum.TryParse<EyeCareMode>(modeName, ignoreCase: true, out var mode))
        {
            SelectedEyeCare = mode;
            _configService.Current.EyeCare = mode;
            _configService.Save();
            _onEyeCareChanged(mode);
            ShowConfirmation($"Chế độ bảo vệ mắt ban đêm: {CurrentEyeCareLabel}");
        }
    }

    [RelayCommand]
    private void ClearResumeHistory()
    {
        _configService.Current.ResumePositions.Clear();
        _configService.Save();
        ShowConfirmation("Đã xóa toàn bộ lịch sử vị trí xem phim dở dang.");
    }

    [RelayCommand]
    private void SetMaxCachedApps(string? countText)
    {
        if (int.TryParse(countText, out var count))
        {
            MaxCachedWebApps = Math.Clamp(count, 2, 8);
            _configService.Current.MaxCachedWebApps = MaxCachedWebApps;
            _configService.Save();
            ShowConfirmation($"Giữ tối đa {MaxCachedWebApps} ứng dụng gần nhất trong bộ nhớ.");
        }
    }

    [RelayCommand]
    private void FreeMemoryNow()
    {
        _webBrowserService.ReleaseAllBackgroundTabs();
        RefreshSystemDiagnostics();
        ShowConfirmation("Đã giải phóng bộ nhớ RAM và đóng các ứng dụng chạy ngầm!");
    }

    [RelayCommand]
    private async Task CleanWebTempDataAsync()
    {
        await _webBrowserService.ClearTemporaryWebDataAsync();
        RefreshSystemDiagnostics();
        ShowConfirmation("Đã dọn dẹp dữ liệu duyệt web tạm thời thành công!");
    }

    [RelayCommand]
    private void SetSleepTimerMinutes(string? minutesText)
    {
        if (int.TryParse(minutesText, out var minutes))
        {
            SelectedSleepMinutes = minutes;
            _onSleepTimerSelected(minutes);
            ShowConfirmation(minutes <= 0
                ? "Đã hủy hẹn giờ tự tắt ZenBox."
                : $"Đã hẹn giờ tự động tắt ZenBox sau {minutes} phút.");
        }
    }

    [RelayCommand]
    private void ChooseMoviesFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Chọn thư mục lưu Phim của bạn",
            Multiselect = false
        };

        if (Directory.Exists(MoviesFolderPath))
        {
            dialog.InitialDirectory = MoviesFolderPath;
        }

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
        {
            MoviesFolderPath = dialog.FolderName;
            _configService.Current.MoviesFolderPath = dialog.FolderName;
            _configService.Save();
            ShowConfirmation("Đã cập nhật thư mục Phim thành công!");
        }
    }

    [RelayCommand]
    private void ChooseMusicFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Chọn thư mục lưu Nhạc của bạn",
            Multiselect = false
        };

        if (Directory.Exists(MusicFolderPath))
        {
            dialog.InitialDirectory = MusicFolderPath;
        }

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
        {
            MusicFolderPath = dialog.FolderName;
            _configService.Current.MusicFolderPath = dialog.FolderName;
            _configService.Save();
            ShowConfirmation("Đã cập nhật thư mục Nhạc thành công!");
        }
    }

    [RelayCommand]
    private void GoHome()
    {
        _goHomeCallback();
    }

    private void ShowConfirmation(string message)
    {
        StatusMessage = message;
        HasStatusMessage = true;
    }
}
