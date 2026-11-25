using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ZenBox.Models;
using ZenBox.ViewModels;
using ZenBox.Views;

namespace ZenBox;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private FloatingOrbWindow? _floatingOrbWindow;
    private FloatingMenuWindow? _floatingMenuWindow;
    private bool _backgroundStarted;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        Loaded += OnMainWindowLoaded;
        ContentRendered += OnMainWindowContentRendered;
        Closed += OnMainWindowClosed;
    }

    private void OnMainWindowLoaded(object sender, RoutedEventArgs e)
    {
        // Khởi tạo sẵn bề mặt Popup toàn màn hình ở chế độ xuyên chuột (Opacity = 0, WS_EX_TRANSPARENT) để mở 0ms không bao giờ nháy
        _floatingMenuWindow = new FloatingMenuWindow(
            _viewModel,
            isPopupOpen =>
            {
                if (_floatingOrbWindow is not null)
                {
                    _floatingOrbWindow.Opacity = isPopupOpen ? 0.0 : 1.0;
                    _floatingOrbWindow.IsHitTestVisible = !isPopupOpen;
                }
            })
        {
            Owner = this
        };
        _floatingMenuWindow.Show();

        // Cụm viên ngọc nổi độc lập (gồm nút Trả Về khi ở trong App + nút mở Trung tâm điều khiển)
        _floatingOrbWindow = new FloatingOrbWindow(_viewModel, () => _floatingMenuWindow.OpenSmoothly())
        {
            Owner = this
        };
        _floatingOrbWindow.Show();
    }

    private void OnMainWindowContentRendered(object? sender, EventArgs e)
    {
        if (_backgroundStarted)
        {
            return;
        }

        _backgroundStarted = true;
        _viewModel.StartDeferredBackgroundServices();
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.FocusedElement is TextBox)
        {
            return;
        }

        if (e.Key == Key.BrowserHome || (e.Key == Key.Home && Keyboard.Modifiers == ModifierKeys.Alt))
        {
            _viewModel.NavigateHome();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.BrowserBack)
        {
            _viewModel.AppGoBack();
            e.Handled = true;
            return;
        }

        if (_viewModel.ActiveSection == AppSection.MediaPlayer)
        {
            switch (e.Key)
            {
                case Key.Space:
                case Key.MediaPlayPause:
                    _viewModel.MediaPlayerVm.TogglePlayPause();
                    e.Handled = true;
                    break;
                case Key.Left:
                    _viewModel.MediaPlayerVm.Rewind15Seconds();
                    e.Handled = true;
                    break;
                case Key.Right:
                    _viewModel.MediaPlayerVm.Forward15Seconds();
                    e.Handled = true;
                    break;
                case Key.Escape:
                    _viewModel.MediaPlayerVm.BackToLibraryCommand.Execute(null);
                    e.Handled = true;
                    break;
            }
        }
        else if (e.Key == Key.Escape && _viewModel.IsNotHome)
        {
            _viewModel.AppGoBack();
            e.Handled = true;
        }
    }

    private void OnMainWindowClosed(object? sender, EventArgs e)
    {
        _floatingOrbWindow?.Close();
        _floatingMenuWindow?.Close();
        _viewModel.Dispose();
    }
}
