using System.Windows;
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
        // Sau khi khung hình đầu tiên đã hiện lên màn hình, mới âm thầm làm nóng WebView2 + uBlock và mở cổng điều khiển từ xa
        _viewModel.StartDeferredBackgroundServices();
    }

    private void OnMainWindowClosed(object? sender, EventArgs e)
    {
        _floatingMenuWindow?.Close();
        _viewModel.Dispose();
    }
}
