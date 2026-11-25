using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZenBox.Models;

namespace ZenBox.ViewModels;

public sealed partial class HomeViewModel : ObservableObject
{
    private readonly Action<AppTileItem> _onTileSelected;

    public ObservableCollection<AppTileItem> OnlineTiles { get; }
    public ObservableCollection<AppTileItem> OfflineAndSystemTiles { get; }

    public HomeViewModel(Action<AppTileItem> onTileSelected)
    {
        _onTileSelected = onTileSelected;

        OnlineTiles =
        [
            new AppTileItem
            {
                Title = "YouTube",
                Subtitle = "Xem video, thời sự, ca nhạc",
                IconSymbol = "▶",
                AccentBrush = new SolidColorBrush(Color.FromRgb(248, 113, 113)),
                Section = AppSection.YouTube,
                TargetUrl = "https://www.youtube.com",
                RequiresInternet = true
            },
            new AppTileItem
            {
                Title = "Facebook",
                Subtitle = "Kết nối bạn bè và gia đình",
                IconSymbol = "f",
                AccentBrush = new SolidColorBrush(Color.FromRgb(96, 165, 250)),
                Section = AppSection.Facebook,
                TargetUrl = "https://www.facebook.com",
                RequiresInternet = true
            },
            new AppTileItem
            {
                Title = "TikTok",
                Subtitle = "Video giải trí ngắn đặc sắc",
                IconSymbol = "♪",
                AccentBrush = new SolidColorBrush(Color.FromRgb(45, 212, 191)),
                Section = AppSection.TikTok,
                TargetUrl = "https://www.tiktok.com",
                RequiresInternet = true
            },
            new AppTileItem
            {
                Title = "Trình duyệt Web",
                Subtitle = "Đọc báo, tra cứu thông tin",
                IconSymbol = "🌐",
                AccentBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                Section = AppSection.WebBrowser,
                TargetUrl = "https://www.google.com",
                RequiresInternet = true
            }
        ];

        OfflineAndSystemTiles =
        [
            new AppTileItem
            {
                Title = "Phim của tôi",
                Subtitle = "Xem phim lưu sẵn không cần mạng",
                IconSymbol = "🎬",
                AccentBrush = new SolidColorBrush(Color.FromRgb(251, 191, 36)),
                Section = AppSection.OfflineMovies,
                RequiresInternet = false
            },
            new AppTileItem
            {
                Title = "Nhạc của tôi",
                Subtitle = "Nghe album nhạc yêu thích",
                IconSymbol = "🎵",
                AccentBrush = new SolidColorBrush(Color.FromRgb(167, 139, 250)),
                Section = AppSection.OfflineMusic,
                RequiresInternet = false
            },
            new AppTileItem
            {
                Title = "Cài đặt",
                Subtitle = "Chọn thư mục phim nhạc và khởi động",
                IconSymbol = "⚙",
                AccentBrush = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                Section = AppSection.SystemConfig,
                RequiresInternet = false
            }
        ];
    }

    [RelayCommand]
    private void SelectTile(AppTileItem? item)
    {
        if (item is not null)
        {
            _onTileSelected(item);
        }
    }
}
