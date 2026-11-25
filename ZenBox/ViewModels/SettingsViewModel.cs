using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
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
    private string _sleepTimerStatusText = "Đang tắt";

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasStatusMessage;

    public ObservableCollection<SoundPresetOption> SoundPresets { get; } = [];

    public bool IsAudioCategory => ActiveCategoryIndex == 0;
    public bool IsCinemaCategory => ActiveCategoryIndex == 1;
    public bool IsPerformanceCategory => ActiveCategoryIndex == 2;
    public bool IsSystemCategory => ActiveCategoryIndex == 3;

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
        _selectedAspectRatio = current.DefaultAspectRatio;
        _selectedPlaybackSpeed = current.DefaultPlaybackSpeed <= 0 ? 1.0 : current.DefaultPlaybackSpeed;
        _autoResumeVideos = current.AutoResumeVideos;
        _selectedEyeCare = current.EyeCare;
        _smartMemorySaver = current.SmartMemorySaver;
        _maxCachedWebApps = Math.Clamp(current.MaxCachedWebApps, 2, 8);
        _masterVolumePercent = _audioService.GetMasterVolumePercent();
        _isMuted = _audioService.IsMuted();

        foreach (var preset in Enum.GetValues<AudioPreset>())
        {
            SoundPresets.Add(new SoundPresetOption
            {
                Preset = preset,
                Title = AudioPresetCatalog.GetDisplayName(preset),
                Description = AudioPresetCatalog.GetDescription(preset),
                IsSelected = preset == current.SoundPreset
            });
        }

        _webBrowserService.PoolCountChanged += (_, _) => RefreshSystemDiagnostics();
        RefreshSystemDiagnostics();
    }

    public void RefreshSystemDiagnostics()
    {
        MasterVolumePercent = _audioService.GetMasterVolumePercent();
        IsMuted = _audioService.IsMuted();

        using var proc = Process.GetCurrentProcess();
        var ramMb = proc.WorkingSet64 / (1024.0 * 1024.0);
        RamUsageText = $"{ramMb:0} MB RAM";
        ActiveBackgroundAppsText = $"{_webBrowserService.ActivePoolCount} ứng dụng trực tuyến đang giữ sẵn";
        RemoteEndpointDisplay = $"ws://{ResolveLocalIpAddress()}:{_configService.Current.RemotePort}";
    }

    private static string ResolveLocalIpAddress()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            var ip = host.AddressList.FirstOrDefault(a =>
                a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a));
            return ip?.ToString() ?? "127.0.0.1";
        }
        catch
        {
            return "127.0.0.1";
        }
    }

    partial void OnActiveCategoryIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsAudioCategory));
        OnPropertyChanged(nameof(IsCinemaCategory));
        OnPropertyChanged(nameof(IsPerformanceCategory));
        OnPropertyChanged(nameof(IsSystemCategory));
        RefreshSystemDiagnostics();
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
        OnPropertyChanged(nameof(CurrentEyeCareLabel));
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
        OnPropertyChanged(nameof(CurrentAspectDisplay));
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
            OnPropertyChanged(nameof(CurrentSpeedDisplay));
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
            OnPropertyChanged(nameof(CurrentEyeCareLabel));
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
