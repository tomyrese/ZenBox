using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZenBox.Models;
using ZenBox.Services;

namespace ZenBox.ViewModels;

public sealed partial class MediaLibraryViewModel : ObservableObject
{
    private readonly MediaLibraryService _mediaService;
    private readonly ConfigService _configService;
    private readonly ErrorHandlingService _errorService;
    private readonly Action<MediaItem> _playMediaCallback;
    private readonly Action _openConfigCallback;
    private readonly Action _goHomeCallback;

    private bool _isVideoMode = true;
    private CancellationTokenSource? _scanCts;

    [ObservableProperty]
    private string _headerTitle = "Phim của tôi";

    [ObservableProperty]
    private string _currentFolderDisplay = string.Empty;

    [ObservableProperty]
    private string _emptyStateMessage = "Chưa tìm thấy tệp nào trong thư mục đã chọn.";

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private bool _isEmpty;

    public ObservableCollection<MediaItem> MediaItems { get; } = [];

    public MediaLibraryViewModel(
        MediaLibraryService mediaService,
        ConfigService configService,
        ErrorHandlingService errorService,
        Action<MediaItem> playMediaCallback,
        Action openConfigCallback,
        Action goHomeCallback)
    {
        _mediaService = mediaService;
        _configService = configService;
        _errorService = errorService;
        _playMediaCallback = playMediaCallback;
        _openConfigCallback = openConfigCallback;
        _goHomeCallback = goHomeCallback;
    }

    public async Task LoadLibraryAsync(bool videoMode)
    {
        _isVideoMode = videoMode;
        HeaderTitle = videoMode ? "Phim của tôi" : "Nhạc của tôi";

        var folder = videoMode
            ? _configService.Current.MoviesFolderPath
            : _configService.Current.MusicFolderPath;

        CurrentFolderDisplay = $"Thư mục đang mở: {folder}";
        EmptyStateMessage = videoMode
            ? "Chưa có bộ phim nào trong thư mục này. Hãy chép phim vào máy hoặc bấm Chọn Thư Mục Khác."
            : "Chưa có bài hát nào trong thư mục này. Hãy chép nhạc vào máy hoặc bấm Chọn Thư Mục Khác.";

        _scanCts?.Cancel();
        _scanCts = new CancellationTokenSource();
        var token = _scanCts.Token;

        IsScanning = true;
        IsEmpty = false;
        MediaItems.Clear();

        try
        {
            var items = videoMode
                ? await _mediaService.ScanMoviesAsync(folder, token)
                : await _mediaService.ScanMusicAsync(folder, token);

            if (token.IsCancellationRequested)
            {
                return;
            }

            foreach (var item in items)
            {
                MediaItems.Add(item);
            }

            IsEmpty = MediaItems.Count == 0;
        }
        catch (OperationCanceledException)
        {
            // Đã hủy quét khi chuyển màn hình
        }
        catch (Exception ex)
        {
            _errorService.ReportError(ErrorCategory.MediaFileCorrupted, $"Scan library ({folder})", ex);
        }
        finally
        {
            if (!token.IsCancellationRequested)
            {
                IsScanning = false;
            }
        }
    }

    [RelayCommand]
    private void PlayItem(MediaItem? item)
    {
        if (item is null)
        {
            return;
        }

        if (!File.Exists(item.FilePath))
        {
            _errorService.ReportError(
                ErrorCategory.MediaFileCorrupted,
                $"Missing media file: {item.FilePath}",
                null,
                () => _ = LoadLibraryAsync(_isVideoMode));
            return;
        }

        _playMediaCallback(item);
    }

    [RelayCommand]
    private Task RefreshLibraryAsync()
    {
        return LoadLibraryAsync(_isVideoMode);
    }

    [RelayCommand]
    private void OpenConfig()
    {
        _openConfigCallback();
    }

    [RelayCommand]
    private void GoHome()
    {
        _goHomeCallback();
    }
}
