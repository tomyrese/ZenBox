using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZenBox.Models;
using ZenBox.Services;

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
            CreateOnline(AppSection.YouTube, "YouTube", "https://www.youtube.com", "#FF0033"),
            CreateOnline(AppSection.Netflix, "Netflix", "https://www.netflix.com", "#E50914"),
            CreateOnline(AppSection.Spotify, "Spotify", "https://open.spotify.com", "#1ED760"),
            CreateOnline(AppSection.TikTok, "TikTok", "https://www.tiktok.com", "#FE2C55"),
            CreateOnline(AppSection.Facebook, "Facebook", "https://www.facebook.com", "#0866FF"),
            CreateOnline(AppSection.Instagram, "Instagram", "https://www.instagram.com", "#D62976"),
            CreateOnline(AppSection.SoundCloud, "SoundCloud", "https://soundcloud.com", "#FF5500"),
            CreateOnline(AppSection.Twitch, "Twitch", "https://www.twitch.tv", "#9146FF"),
            CreateOnline(AppSection.VtvGo, "VTV Go", "https://vtvgo.vn", "#10B981"),
            CreateOnline(AppSection.WebBrowser, "Tìm kiếm Google", "https://www.google.com", "#4285F4")
        ];

        OfflineAndSystemTiles =
        [
            new AppTileItem
            {
                Title = "Phim của tôi",
                LogoImage = BrandLogoFactory.GetLogo(AppSection.OfflineMovies),
                GlowBrush = CreateGlow("#F59E0B"),
                Section = AppSection.OfflineMovies,
                RequiresInternet = false
            },
            new AppTileItem
            {
                Title = "Nhạc của tôi",
                LogoImage = BrandLogoFactory.GetLogo(AppSection.OfflineMusic),
                GlowBrush = CreateGlow("#C084FC"),
                Section = AppSection.OfflineMusic,
                RequiresInternet = false
            },
            new AppTileItem
            {
                Title = "Cài đặt",
                LogoImage = BrandLogoFactory.GetLogo(AppSection.SystemConfig),
                GlowBrush = CreateGlow("#38BDF8"),
                Section = AppSection.SystemConfig,
                RequiresInternet = false
            }
        ];
    }

    private static AppTileItem CreateOnline(AppSection section, string title, string url, string glowHex)
    {
        return new AppTileItem
        {
            Title = title,
            LogoImage = BrandLogoFactory.GetLogo(section),
            GlowBrush = CreateGlow(glowHex),
            Section = section,
            TargetUrl = url,
            RequiresInternet = true
        };
    }

    private static RadialGradientBrush CreateGlow(string hex)
    {
        var baseColor = (Color)ColorConverter.ConvertFromString(hex);
        var brush = new RadialGradientBrush();
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(115, baseColor.R, baseColor.G, baseColor.B), 0.0));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(40, baseColor.R, baseColor.G, baseColor.B), 0.52));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, baseColor.R, baseColor.G, baseColor.B), 1.0));
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
