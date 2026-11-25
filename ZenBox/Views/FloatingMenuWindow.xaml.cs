using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ZenBox.Models;
using ZenBox.Services;
using ZenBox.ViewModels;

namespace ZenBox.Views;

public partial class FloatingMenuWindow : Window
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    private readonly MainViewModel _mainVm;
    private readonly Action<bool> _onPopupStateChanged;
    private IntPtr _hwnd;
    private bool _isOpen;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

    public FloatingMenuWindow(MainViewModel mainVm, Action<bool> onPopupStateChanged)
    {
        InitializeComponent();
        _mainVm = mainVm;
        _onPopupStateChanged = onPopupStateChanged;
        DataContext = mainVm;

        Left = 0;
        Top = 0;
        Width = SystemParameters.PrimaryScreenWidth;
        Height = SystemParameters.PrimaryScreenHeight;
        Opacity = 0.0;
        IsHitTestVisible = false;

        DockYouTubeImg.Source = BrandLogoFactory.GetLogo(AppSection.YouTube);
        DockNetflixImg.Source = BrandLogoFactory.GetLogo(AppSection.Netflix);
        DockSpotifyImg.Source = BrandLogoFactory.GetLogo(AppSection.Spotify);
        DockTikTokImg.Source = BrandLogoFactory.GetLogo(AppSection.TikTok);
        DockFacebookImg.Source = BrandLogoFactory.GetLogo(AppSection.Facebook);
        DockGoogleImg.Source = BrandLogoFactory.GetLogo(AppSection.WebBrowser);
        DockMoviesImg.Source = BrandLogoFactory.GetLogo(AppSection.OfflineMovies);
        DockMusicImg.Source = BrandLogoFactory.GetLogo(AppSection.OfflineMusic);

        SourceInitialized += OnWindowSourceInitialized;
    }

    private void OnWindowSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        SetClickThrough(enableClickThrough: true);
    }

    private void SetClickThrough(bool enableClickThrough)
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        int style = GetWindowLong(_hwnd, GWL_EXSTYLE);
        if (enableClickThrough)
        {
            SetWindowLong(_hwnd, GWL_EXSTYLE, style | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE);
        }
        else
        {
            SetWindowLong(_hwnd, GWL_EXSTYLE, style & ~(WS_EX_TRANSPARENT | WS_EX_NOACTIVATE));
        }
    }

    public void OpenSmoothly()
    {
        if (_isOpen)
        {
            return;
        }

        _isOpen = true;
        _mainVm.SyncLiveSystemVolume();
        _onPopupStateChanged(true);

        // Không bao giờ đổi kích thước cửa sổ -> Triệt tiêu 100% hiện tượng nháy khung hình DWM
        IsHitTestVisible = true;
        SetClickThrough(enableClickThrough: false);

        var ease = new QuinticEase { EasingMode = EasingMode.EaseOut };
        var sheetDuration = new Duration(TimeSpan.FromMilliseconds(240));

        BeginAnimation(OpacityProperty, new DoubleAnimation(1.0, new Duration(TimeSpan.FromMilliseconds(160))) { EasingFunction = ease });
        SheetScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.94, 1.0, sheetDuration) { EasingFunction = ease });
        SheetScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.94, 1.0, sheetDuration) { EasingFunction = ease });
        SheetTranslate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(14.0, 0.0, sheetDuration) { EasingFunction = ease });
    }

    public void CloseSmoothly()
    {
        if (!_isOpen)
        {
            return;
        }

        _isOpen = false;
        IsHitTestVisible = false;
        SetClickThrough(enableClickThrough: true);
        _onPopupStateChanged(false);

        var ease = new QuarticEase { EasingMode = EasingMode.EaseOut };
        var fadeDuration = new Duration(TimeSpan.FromMilliseconds(150));

        BeginAnimation(OpacityProperty, new DoubleAnimation(0.0, fadeDuration) { EasingFunction = ease });
        SheetScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.96, fadeDuration) { EasingFunction = ease });
        SheetScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.96, fadeDuration) { EasingFunction = ease });
    }

    private void OnBackdropMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        CloseSmoothly();
    }

    private void OnMenuCardMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
    }

    private void OnCloseMenuClick(object sender, RoutedEventArgs e)
    {
        CloseSmoothly();
    }

    private void OnAppBackClick(object sender, RoutedEventArgs e)
    {
        CloseSmoothly();
        _mainVm.AppGoBack();
    }

    private void OnAppReloadClick(object sender, RoutedEventArgs e)
    {
        CloseSmoothly();
        _mainVm.AppReload();
    }

    private void OnGoHomeClick(object sender, RoutedEventArgs e)
    {
        CloseSmoothly();
        _mainVm.NavigateHome();
    }

    private void OnOpenSettingsClick(object sender, RoutedEventArgs e)
    {
        CloseSmoothly();
        _mainVm.OpenSettings();
    }

    private void OnOpenYouTubeClick(object sender, RoutedEventArgs e)
    {
        CloseSmoothly();
        _mainVm.OpenYouTube();
    }

    private void OnOpenNetflixClick(object sender, RoutedEventArgs e)
    {
        CloseSmoothly();
        _mainVm.OpenNetflix();
    }

    private void OnOpenSpotifyClick(object sender, RoutedEventArgs e)
    {
        CloseSmoothly();
        _mainVm.OpenSpotify();
    }

    private void OnOpenFacebookClick(object sender, RoutedEventArgs e)
    {
        CloseSmoothly();
        _mainVm.OpenFacebook();
    }

    private void OnOpenTikTokClick(object sender, RoutedEventArgs e)
    {
        CloseSmoothly();
        _mainVm.OpenTikTok();
    }

    private void OnOpenWebBrowserClick(object sender, RoutedEventArgs e)
    {
        CloseSmoothly();
        _mainVm.OpenWebBrowser();
    }

    private void OnOpenMoviesClick(object sender, RoutedEventArgs e)
    {
        CloseSmoothly();
        _mainVm.OpenOfflineMovies();
    }

    private void OnOpenMusicClick(object sender, RoutedEventArgs e)
    {
        CloseSmoothly();
        _mainVm.OpenOfflineMusic();
    }

    private void OnExitAppClick(object sender, RoutedEventArgs e)
    {
        CloseSmoothly();
        _mainVm.ExitApplication();
    }
}
