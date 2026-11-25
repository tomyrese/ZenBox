using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ZenBox.ViewModels;

namespace ZenBox.Views;

public partial class FloatingMenuWindow : Window
{
    private const double CompactWindowSize = 96.0;
    private const double DragThresholdPixels = 8.0;

    private readonly MainViewModel _mainVm;
    private bool _isMouseDownOnBall;
    private bool _isDraggingBall;
    private Point _mouseDownDipScreenPos;
    private double _ballStartLeft;
    private double _ballStartTop;
    private double _savedBallLeft;
    private double _savedBallTop;
    private bool _isMenuExpanded;

    public FloatingMenuWindow(MainViewModel mainVm)
    {
        InitializeComponent();
        _mainVm = mainVm;
        DataContext = mainVm;
        Loaded += OnWindowLoaded;
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        var workArea = SystemParameters.WorkArea;
        var cfg = _mainVm.ConfigService.Current;

        _savedBallLeft = cfg.FloatingButtonLeft ?? (workArea.Right - CompactWindowSize - 24);
        _savedBallTop = cfg.FloatingButtonTop ?? (workArea.Top + (workArea.Height - CompactWindowSize) / 2.0);
        ClampSavedPositionToScreen();

        Left = _savedBallLeft;
        Top = _savedBallTop;
    }

    private Point GetCursorDipScreenPosition(MouseEventArgs e)
    {
        var physicalPoint = PointToScreen(e.GetPosition(this));
        var dpi = VisualTreeHelper.GetDpi(this);
        return new Point(physicalPoint.X / dpi.DpiScaleX, physicalPoint.Y / dpi.DpiScaleY);
    }

    private void OnBallMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isMouseDownOnBall = true;
        _isDraggingBall = false;
        _mouseDownDipScreenPos = GetCursorDipScreenPosition(e);
        _ballStartLeft = Left;
        _ballStartTop = Top;
        AssistiveBall.CaptureMouse();
        e.Handled = true;
    }

    private void OnBallMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isMouseDownOnBall || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var currentDipPos = GetCursorDipScreenPosition(e);
        var dx = currentDipPos.X - _mouseDownDipScreenPos.X;
        var dy = currentDipPos.Y - _mouseDownDipScreenPos.Y;

        if (!_isDraggingBall && Math.Sqrt(dx * dx + dy * dy) > DragThresholdPixels)
        {
            _isDraggingBall = true;
        }

        if (_isDraggingBall)
        {
            var screenW = SystemParameters.PrimaryScreenWidth;
            var screenH = SystemParameters.PrimaryScreenHeight;

            Left = Math.Clamp(_ballStartLeft + dx, 0, Math.Max(0, screenW - CompactWindowSize));
            Top = Math.Clamp(_ballStartTop + dy, 0, Math.Max(0, screenH - CompactWindowSize));
        }
    }

    private void OnBallMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isMouseDownOnBall)
        {
            return;
        }

        _isMouseDownOnBall = false;
        AssistiveBall.ReleaseMouseCapture();
        e.Handled = true;

        if (_isDraggingBall)
        {
            _savedBallLeft = Left;
            _savedBallTop = Top;
            _mainVm.ConfigService.Current.FloatingButtonLeft = _savedBallLeft;
            _mainVm.ConfigService.Current.FloatingButtonTop = _savedBallTop;
            _mainVm.ConfigService.Save();
            _isDraggingBall = false;
        }
        else
        {
            ExpandQuickMenu();
        }
    }

    private void ExpandQuickMenu()
    {
        if (_isMenuExpanded)
        {
            return;
        }

        _savedBallLeft = Left;
        _savedBallTop = Top;
        _isMenuExpanded = true;

        AssistiveBallContainer.Visibility = Visibility.Collapsed;
        ExpandedMenuOverlay.Opacity = 0;
        ExpandedMenuOverlay.Visibility = Visibility.Visible;

        Left = 0;
        Top = 0;
        Width = SystemParameters.PrimaryScreenWidth;
        Height = SystemParameters.PrimaryScreenHeight;

        PlaySheetEntranceAnimation();
    }

    private void PlaySheetEntranceAnimation()
    {
        var ease = new QuinticEase { EasingMode = EasingMode.EaseOut };
        var duration = new Duration(TimeSpan.FromMilliseconds(280));

        ExpandedMenuOverlay.BeginAnimation(OpacityProperty, new DoubleAnimation(0.0, 1.0, new Duration(TimeSpan.FromMilliseconds(200))));
        SheetScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.92, 1.0, duration) { EasingFunction = ease });
        SheetScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.92, 1.0, duration) { EasingFunction = ease });
        SheetTranslate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(20.0, 0.0, duration) { EasingFunction = ease });
    }

    private void CollapseQuickMenu()
    {
        if (!_isMenuExpanded)
        {
            return;
        }

        _isMenuExpanded = false;
        ExpandedMenuOverlay.Visibility = Visibility.Collapsed;
        AssistiveBallContainer.Visibility = Visibility.Visible;

        Width = CompactWindowSize;
        Height = CompactWindowSize;
        ClampSavedPositionToScreen();
        Left = _savedBallLeft;
        Top = _savedBallTop;
    }

    private void ClampSavedPositionToScreen()
    {
        var screenW = SystemParameters.PrimaryScreenWidth;
        var screenH = SystemParameters.PrimaryScreenHeight;
        _savedBallLeft = Math.Clamp(_savedBallLeft, 0, Math.Max(0, screenW - CompactWindowSize));
        _savedBallTop = Math.Clamp(_savedBallTop, 0, Math.Max(0, screenH - CompactWindowSize));
    }

    private void OnBackdropMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        CollapseQuickMenu();
    }

    private void OnMenuCardMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
    }

    private void OnCloseMenuClick(object sender, RoutedEventArgs e)
    {
        CollapseQuickMenu();
    }

    private void OnGoHomeClick(object sender, RoutedEventArgs e)
    {
        CollapseQuickMenu();
        _mainVm.NavigateHome();
    }

    private void OnOpenYouTubeClick(object sender, RoutedEventArgs e)
    {
        CollapseQuickMenu();
        _mainVm.OpenYouTube();
    }

    private void OnOpenFacebookClick(object sender, RoutedEventArgs e)
    {
        CollapseQuickMenu();
        _mainVm.OpenFacebook();
    }

    private void OnOpenTikTokClick(object sender, RoutedEventArgs e)
    {
        CollapseQuickMenu();
        _mainVm.OpenTikTok();
    }

    private void OnOpenWebBrowserClick(object sender, RoutedEventArgs e)
    {
        CollapseQuickMenu();
        _mainVm.OpenWebBrowser();
    }

    private void OnOpenMoviesClick(object sender, RoutedEventArgs e)
    {
        CollapseQuickMenu();
        _mainVm.OpenOfflineMovies();
    }

    private void OnOpenMusicClick(object sender, RoutedEventArgs e)
    {
        CollapseQuickMenu();
        _mainVm.OpenOfflineMusic();
    }

    private void OnVolumeDownClick(object sender, RoutedEventArgs e)
    {
        _mainVm.VolumeDown();
    }

    private void OnVolumeUpClick(object sender, RoutedEventArgs e)
    {
        _mainVm.VolumeUp();
    }

    private void OnExitAppClick(object sender, RoutedEventArgs e)
    {
        CollapseQuickMenu();
        _mainVm.ExitApplication();
    }
}
