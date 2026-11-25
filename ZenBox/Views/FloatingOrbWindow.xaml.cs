using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ZenBox.ViewModels;

namespace ZenBox.Views;

public partial class FloatingOrbWindow : Window
{
    private const double DragThresholdPixels = 8.0;

    private readonly MainViewModel _mainVm;
    private readonly Action _openPopupCallback;
    private UIElement? _capturedElement;
    private bool _isMouseDown;
    private bool _isDragging;
    private Point _mouseDownDipScreenPos;
    private double _windowStartLeft;
    private double _windowStartTop;

    public FloatingOrbWindow(MainViewModel mainVm, Action openPopupCallback)
    {
        InitializeComponent();
        _mainVm = mainVm;
        _openPopupCallback = openPopupCallback;
        DataContext = mainVm;
        Loaded += OnWindowLoaded;
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        var workArea = SystemParameters.WorkArea;
        var cfg = _mainVm.ConfigService.Current;

        var left = cfg.FloatingButtonLeft ?? (workArea.Right - Width - 20);
        var top = cfg.FloatingButtonTop ?? (workArea.Top + (workArea.Height - Height) / 2.0);

        Left = Math.Clamp(left, 0, Math.Max(0, SystemParameters.PrimaryScreenWidth - Width));
        Top = Math.Clamp(top, 0, Math.Max(0, SystemParameters.PrimaryScreenHeight - Height));
    }

    private Point GetCursorDipScreenPosition(MouseEventArgs e)
    {
        var physicalPoint = PointToScreen(e.GetPosition(this));
        var dpi = VisualTreeHelper.GetDpi(this);
        return new Point(physicalPoint.X / dpi.DpiScaleX, physicalPoint.Y / dpi.DpiScaleY);
    }

    private void BeginPotentialDrag(UIElement element, MouseButtonEventArgs e)
    {
        _isMouseDown = true;
        _isDragging = false;
        _capturedElement = element;
        _mouseDownDipScreenPos = GetCursorDipScreenPosition(e);
        _windowStartLeft = Left;
        _windowStartTop = Top;
        element.CaptureMouse();
        e.Handled = true;
    }

    private void OnBackOrbMouseDown(object sender, MouseButtonEventArgs e)
    {
        BeginPotentialDrag(BackOrb, e);
    }

    private void OnMenuOrbMouseDown(object sender, MouseButtonEventArgs e)
    {
        BeginPotentialDrag(MenuOrb, e);
    }

    private void OnOrbMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isMouseDown || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var currentDipPos = GetCursorDipScreenPosition(e);
        var dx = currentDipPos.X - _mouseDownDipScreenPos.X;
        var dy = currentDipPos.Y - _mouseDownDipScreenPos.Y;

        if (!_isDragging && Math.Sqrt(dx * dx + dy * dy) > DragThresholdPixels)
        {
            _isDragging = true;
        }

        if (_isDragging)
        {
            var screenW = SystemParameters.PrimaryScreenWidth;
            var screenH = SystemParameters.PrimaryScreenHeight;
            Left = Math.Clamp(_windowStartLeft + dx, 0, Math.Max(0, screenW - Width));
            Top = Math.Clamp(_windowStartTop + dy, 0, Math.Max(0, screenH - Height));
        }
    }

    private bool EndDragOrClick(MouseButtonEventArgs e)
    {
        if (!_isMouseDown)
        {
            return false;
        }

        _isMouseDown = false;
        _capturedElement?.ReleaseMouseCapture();
        _capturedElement = null;
        e.Handled = true;

        if (_isDragging)
        {
            _isDragging = false;
            _mainVm.ConfigService.Current.FloatingButtonLeft = Left;
            _mainVm.ConfigService.Current.FloatingButtonTop = Top;
            _mainVm.ConfigService.Save();
            return false;
        }

        return true;
    }

    private void OnBackOrbMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (EndDragOrClick(e))
        {
            _mainVm.AppGoBack();
        }
    }

    private void OnMenuOrbMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (EndDragOrClick(e))
        {
            _openPopupCallback();
        }
    }
}
