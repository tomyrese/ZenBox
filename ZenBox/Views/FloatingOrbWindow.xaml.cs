using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ZenBox.Models;
using ZenBox.ViewModels;

namespace ZenBox.Views;

public partial class FloatingOrbWindow : Window
{
    private const double DragThresholdPixels = 8.0;
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;

    private readonly MainViewModel _mainVm;
    private readonly Action _openPopupCallback;
    private readonly DispatcherTimer _autoHideWatchTimer;

    private IntPtr _hwnd;
    private UIElement? _capturedElement;
    private bool _isMouseDown;
    private bool _isDragging;
    private bool _isSuppressedByPopup;
    private bool _isClickThroughEnabled;
    private double _targetOpacityState = 1.0;
    private Point _mouseDownDipScreenPos;
    private double _windowStartLeft;
    private double _windowStartTop;
    private NativePoint _lastCursorPos;
    private DateTime _lastMouseMoveUtc = DateTime.UtcNow;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint lpPoint);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    public FloatingOrbWindow(MainViewModel mainVm, Action openPopupCallback)
    {
        InitializeComponent();
        _mainVm = mainVm;
        _openPopupCallback = openPopupCallback;
        DataContext = mainVm;

        GetCursorPos(out _lastCursorPos);

        _autoHideWatchTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(180)
        };
        _autoHideWatchTimer.Tick += OnAutoHideWatchTick;

        SourceInitialized += OnWindowSourceInitialized;
        Loaded += OnWindowLoaded;
        Closed += (_, _) => _autoHideWatchTimer.Stop();
    }

    private void OnWindowSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        var workArea = SystemParameters.WorkArea;
        var cfg = _mainVm.ConfigService.Current;

        // Neo sát mép phải màn hình (cách 10px) để nằm hoàn toàn trong vùng lề trống, không đè lên bất kỳ nội dung nào
        var left = cfg.FloatingButtonLeft ?? (workArea.Right - Width - 10);
        var top = cfg.FloatingButtonTop ?? (workArea.Top + (workArea.Height - Height) / 2.0);

        Left = Math.Clamp(left, 0, Math.Max(0, SystemParameters.PrimaryScreenWidth - Width));
        Top = Math.Clamp(top, 0, Math.Max(0, SystemParameters.PrimaryScreenHeight - Height));

        _autoHideWatchTimer.Start();
    }

    public void SetSuppressedByPopup(bool isPopupOpen)
    {
        _isSuppressedByPopup = isPopupOpen;
        if (isPopupOpen)
        {
            SetClickThrough(true);
            BeginAnimation(OpacityProperty, null);
            Opacity = 0.0;
            _targetOpacityState = 0.0;
        }
        else
        {
            _lastMouseMoveUtc = DateTime.UtcNow;
            TransitionToOpacity(1.0, clickThrough: false);
        }
    }

    private void OnAutoHideWatchTick(object? sender, EventArgs e)
    {
        if (_isSuppressedByPopup || _isMouseDown)
        {
            return;
        }

        if (GetCursorPos(out var cur))
        {
            if (Math.Abs(cur.X - _lastCursorPos.X) > 4 || Math.Abs(cur.Y - _lastCursorPos.Y) > 4)
            {
                _lastCursorPos = cur;
                _lastMouseMoveUtc = DateTime.UtcNow;
                TransitionToOpacity(1.0, clickThrough: false);
                return;
            }
        }

        if (IsMouseOver)
        {
            _lastMouseMoveUtc = DateTime.UtcNow;
            TransitionToOpacity(1.0, clickThrough: false);
            return;
        }

        var idleSeconds = (DateTime.UtcNow - _lastMouseMoveUtc).TotalSeconds;
        if (idleSeconds >= 2.2)
        {
            // Khi đang xem phim / YouTube / ứng dụng toàn màn hình: ẩn 100% + xuyên chuột để không che nội dung đang phát!
            bool shouldHideCompletely =
                _mainVm.IsWebVideoFullscreen ||
                _mainVm.ActiveSection == AppSection.MediaPlayer ||
                _mainVm.IsOnlineAppActive;

            if (shouldHideCompletely)
            {
                TransitionToOpacity(0.0, clickThrough: true);
            }
            else
            {
                // Ở màn hình chính / Cài đặt: làm mờ dịu xuống 42% để cực kỳ tinh tế, không gây vướng mắt
                TransitionToOpacity(0.42, clickThrough: false);
            }
        }
    }

    private void TransitionToOpacity(double targetOpacity, bool clickThrough)
    {
        SetClickThrough(clickThrough);
        if (Math.Abs(_targetOpacityState - targetOpacity) < 0.01)
        {
            return;
        }

        _targetOpacityState = targetOpacity;
        var durationMs = targetOpacity > Opacity ? 140 : 280;
        BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(targetOpacity, new Duration(TimeSpan.FromMilliseconds(durationMs))));
    }

    private void SetClickThrough(bool enable)
    {
        if (_hwnd == IntPtr.Zero || _isClickThroughEnabled == enable)
        {
            return;
        }

        _isClickThroughEnabled = enable;
        int style = GetWindowLong(_hwnd, GWL_EXSTYLE);
        if (enable)
        {
            SetWindowLong(_hwnd, GWL_EXSTYLE, style | WS_EX_TRANSPARENT);
        }
        else
        {
            SetWindowLong(_hwnd, GWL_EXSTYLE, style & ~WS_EX_TRANSPARENT);
        }
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
