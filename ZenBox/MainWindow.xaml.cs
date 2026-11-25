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
        _floatingMenuWindow = new FloatingMenuWindow(_viewModel)
        {
            Owner = this
        };
        _floatingMenuWindow.Show();
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
        // Không can thiệp khi người dùng đang gõ chữ trong ô tìm kiếm
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
    }

    private void OnMainWindowClosed(object? sender, EventArgs e)
    {
        _floatingMenuWindow?.Close();
        _viewModel.Dispose();
    }
}
