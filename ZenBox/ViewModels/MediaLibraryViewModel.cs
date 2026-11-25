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

    private List<MediaItem> _allScannedItems = [];
    private bool _isVideoMode = true;
    private CancellationTokenSource? _scanCts;

    [ObservableProperty]
    private string _headerTitle = "Phim của tôi";

    [ObservableProperty]
    private string _currentFolderDisplay = string.Empty;

    [ObservableProperty]
    private string _emptyStateMessage = "Chưa tìm thấy tệp nào trong thư mục đã chọn.";

    [ObservableProperty]
    private string _searchKeyword = string.Empty;

    [ObservableProperty]
    private string _itemCountText = string.Empty;

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

    partial void OnSearchKeywordChanged(string value)
    {
        ApplySearchFilter();
    }

    public async Task LoadLibraryAsync(bool videoMode)
    {
        _isVideoMode = videoMode;
        HeaderTitle = videoMode ? "Phim của tôi" : "Nhạc của tôi";

        var folder = videoMode
            ? _configService.Current.MoviesFolderPath
            : _configService.Current.MusicFolderPath;

        CurrentFolderDisplay = $"Thư mục: {folder}";
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

            // Gắn thêm thông tin vị trí đang xem dở nếu có
            var enriched = new List<MediaItem>(items.Count);
            foreach (var item in items)
            {
                if (item.IsVideo &&
                    _configService.Current.AutoResumeVideos &&
                    _configService.Current.ResumePositions.TryGetValue(item.FilePath, out var savedMs) &&
                    savedMs > 5_000)
                {
                    var span = TimeSpan.FromMilliseconds(savedMs);
                    var posLabel = span.TotalHours >= 1
                        ? $"{(int)span.TotalHours:00}:{span.Minutes:00}:{span.Seconds:00}"
                        : $"{span.Minutes:00}:{span.Seconds:00}";

                    enriched.Add(new MediaItem
                    {
                        Title = item.Title,
                        FilePath = item.FilePath,
                        FileSizeText = item.FileSizeText,
                        SubInfoText = $"Đang xem dở ({posLabel}) • {item.FileSizeText}",
                        IsVideo = item.IsVideo,
                        CoverImage = item.CoverImage
                    });
                }
                else
                {
                    enriched.Add(item);
                }
            }

            _allScannedItems = enriched;
            ApplySearchFilter();
        }
        catch (OperationCanceledException)
        {
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

    private void ApplySearchFilter()
    {
        MediaItems.Clear();
        var query = SearchKeyword?.Trim();
        var filtered = string.IsNullOrWhiteSpace(query)
            ? _allScannedItems
            : _allScannedItems.Where(x => x.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase)).ToList();

        foreach (var item in filtered)
        {
            MediaItems.Add(item);
        }

        IsEmpty = _allScannedItems.Count == 0;
        var unit = _isVideoMode ? "bộ phim" : "bài hát";
        ItemCountText = $"{MediaItems.Count} {unit}";
    }

    [RelayCommand]
    private void ClearSearch()
    {
        SearchKeyword = string.Empty;
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
