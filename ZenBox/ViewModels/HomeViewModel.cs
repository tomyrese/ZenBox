using System.Collections.ObjectModel;
using System.Windows;
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
                Subtitle = "Xem video, thời sự và ca nhạc",
                IconGeometry = Geometry.Parse("M21.58,7.19 C21.35,6.33 20.67,5.65 19.81,5.42 C18.25,5 12,5 12,5 C12,5 5.75,5 4.19,5.42 C3.33,5.65 2.65,6.33 2.42,7.19 C2,8.75 2,12 2,12 C2,12 2,15.25 2.42,16.81 C2.65,17.67 3.33,18.35 4.19,18.58 C5.75,19 12,19 12,19 C12,19 18.25,19 19.81,18.58 C20.67,18.35 21.35,17.67 21.58,16.81 C22,15.25 22,12 22,12 C22,12 22,8.75 21.58,7.19 Z M10,15 L10,9 L15.2,12 Z"),
                BadgeBrush = CreateGradient("#FF3B5C", "#DC2626"),
                GlowBrush = new SolidColorBrush(Color.FromArgb(64, 239, 68, 68)),
                Section = AppSection.YouTube,
                TargetUrl = "https://www.youtube.com",
                RequiresInternet = true
            },
            new AppTileItem
            {
                Title = "Facebook",
                Subtitle = "Kết nối bạn bè và gia đình",
                IconGeometry = Geometry.Parse("M14,13.5 L16.5,13.5 L17.5,9.5 L14,9.5 L14,7.5 C14,6.47 14,5.5 16,5.5 L17.5,5.5 L17.5,2.14 C17.17,2.1 15.94,2 14.64,2 C11.93,2 10,3.66 10,6.7 L10,9.5 L7,9.5 L7,13.5 L10,13.5 L10,22 L14,22 Z"),
                BadgeBrush = CreateGradient("#3B82F6", "#1D4ED8"),
                GlowBrush = new SolidColorBrush(Color.FromArgb(64, 59, 130, 246)),
                Section = AppSection.Facebook,
                TargetUrl = "https://www.facebook.com",
                RequiresInternet = true
            },
            new AppTileItem
            {
                Title = "TikTok",
                Subtitle = "Video giải trí ngắn đặc sắc",
                IconGeometry = Geometry.Parse("M16.6,5.82 C15.9,5.03 15.5,4.02 15.5,2.95 L15.5,2 L12.05,2 L12.05,15.67 C12.05,17.23 10.78,18.5 9.22,18.5 C7.66,18.5 6.39,17.23 6.39,15.67 C6.39,14.11 7.66,12.84 9.22,12.84 C9.53,12.84 9.83,12.89 10.11,12.98 L10.11,9.43 C9.82,9.39 9.52,9.37 9.22,9.37 C5.74,9.37 2.92,12.19 2.92,15.67 C2.92,19.15 5.74,21.97 9.22,21.97 C12.7,21.97 15.52,19.15 15.52,15.67 L15.52,8.98 C16.86,9.94 18.5,10.5 20.27,10.5 L20.27,7.05 C18.86,7.05 17.55,6.59 16.6,5.82 Z"),
                BadgeBrush = CreateGradient("#06B6D4", "#EC4899"),
                GlowBrush = new SolidColorBrush(Color.FromArgb(64, 236, 72, 153)),
                Section = AppSection.TikTok,
                TargetUrl = "https://www.tiktok.com",
                RequiresInternet = true
            },
            new AppTileItem
            {
                Title = "Trình duyệt Web",
                Subtitle = "Đọc báo, tra cứu tin tức mới",
                IconGeometry = Geometry.Parse("M12,2 C6.48,2 2,6.48 2,12 C2,17.52 6.48,22 12,22 C17.52,22 22,17.52 22,12 C22,6.48 17.52,2 12,2 Z M11,19.93 C7.05,19.44 4,16.08 4,12 C4,11.38 4.08,10.79 4.21,10.21 L9,15 L9,16 C9,17.1 9.9,18 11,18 Z M17.9,17.39 C17.64,16.58 16.9,16 16,16 L15,16 L15,13 C15,12.45 14.55,12 14,12 L8,12 L8,10 L10,10 C10.55,10 11,9.55 11,9 L11,7 L13,7 C14.1,7 15,6.1 15,5 L15,4.59 C17.93,5.78 20,8.65 20,12 C20,14.08 19.2,15.97 17.9,17.39 Z"),
                BadgeBrush = CreateGradient("#38BDF8", "#6366F1"),
                GlowBrush = new SolidColorBrush(Color.FromArgb(64, 56, 189, 248)),
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
                Subtitle = "Xem kho phim lưu sẵn trong máy",
                IconGeometry = Geometry.Parse("M18,4 L20,8 L17,8 L15,4 L13,4 L15,8 L12,8 L10,4 L8,4 L10,8 L7,8 L5,4 L4,4 C2.9,4 2,4.9 2,6 L2,18 C2,19.1 2.9,20 4,20 L20,20 C21.1,20 22,19.1 22,18 L22,4 Z M10,17.5 L10,10.5 L16,14 Z"),
                BadgeBrush = CreateGradient("#F59E0B", "#EA580C"),
                GlowBrush = new SolidColorBrush(Color.FromArgb(64, 245, 158, 11)),
                Section = AppSection.OfflineMovies,
                RequiresInternet = false
            },
            new AppTileItem
            {
                Title = "Nhạc của tôi",
                Subtitle = "Thưởng thức album nhạc yêu thích",
                IconGeometry = Geometry.Parse("M12,3 L12,13.55 C11.41,13.21 10.73,13 10,13 C7.79,13 6,14.79 6,17 C6,19.21 7.79,21 10,21 C12.21,21 14,19.21 14,17 L14,7 L18,7 L18,3 Z"),
                BadgeBrush = CreateGradient("#A855F7", "#EC4899"),
                GlowBrush = new SolidColorBrush(Color.FromArgb(64, 168, 85, 247)),
                Section = AppSection.OfflineMusic,
                RequiresInternet = false
            },
            new AppTileItem
            {
                Title = "Cài đặt",
                Subtitle = "Thư mục lưu trữ và khởi động",
                IconGeometry = Geometry.Parse("M3,17 L3,19 L9,19 L9,17 Z M3,5 L3,7 L13,7 L13,5 Z M13,21 L13,19 L21,19 L21,17 L13,17 L13,15 L11,15 L11,21 Z M7,9 L7,11 L3,11 L3,13 L7,13 L7,15 L9,15 L9,9 Z M21,13 L21,11 L11,11 L11,13 Z M15,9 L17,9 L17,7 L21,7 L21,5 L17,5 L17,3 L15,3 Z"),
                BadgeBrush = CreateGradient("#64748B", "#334155"),
                GlowBrush = new SolidColorBrush(Color.FromArgb(64, 100, 116, 139)),
                Section = AppSection.SystemConfig,
                RequiresInternet = false
            }
        ];
    }

    private static LinearGradientBrush CreateGradient(string startHex, string endHex)
    {
        var start = (Color)ColorConverter.ConvertFromString(startHex);
        var end = (Color)ColorConverter.ConvertFromString(endHex);
        var brush = new LinearGradientBrush(start, end, new Point(0, 0), new Point(1, 1));
        brush.Freeze();
        return brush;
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
