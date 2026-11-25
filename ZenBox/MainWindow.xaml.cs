using System.Windows;
using ZenBox.ViewModels;
using ZenBox.Views;

namespace ZenBox;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private FloatingMenuWindow? _floatingMenuWindow;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        Loaded += OnMainWindowLoaded;
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

    private void OnMainWindowClosed(object? sender, EventArgs e)
    {
        _floatingMenuWindow?.Close();
        _viewModel.Dispose();
    }
}
