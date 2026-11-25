using System.Windows;
using System.Windows.Threading;
using ZenBox.Models;
using ZenBox.Services;
using ZenBox.ViewModels;

namespace ZenBox;

public partial class App : Application
{
    private ErrorHandlingService? _errorService;
    private MainViewModel? _mainViewModel;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _errorService = new ErrorHandlingService();

        try
        {
            var configService = new ConfigService(_errorService);
            var networkService = new NetworkMonitorService();
            var audioService = new SystemAudioService();
            var webBrowserService = new WebBrowserService(_errorService, networkService);
            var mediaLibraryService = new MediaLibraryService(_errorService);

            _mainViewModel = new MainViewModel(
                _errorService,
                configService,
                networkService,
                audioService,
                webBrowserService,
                mediaLibraryService);

            var mainWindow = new MainWindow(_mainViewModel);
            MainWindow = mainWindow;
            mainWindow.Show();

            // Chỉ bật chặn ngoại lệ ngầm sau khi cửa sổ chính đã hiển thị thành công trên màn hình
            RegisterGlobalExceptionHandlers(_errorService);
        }
        catch (Exception ex)
        {
            _errorService.LogError(ErrorCategory.UnexpectedSystemError, "Fatal Startup Failure", ex);
            MessageBox.Show(
                "ZenBox gặp sự cố khi khởi động. Vui lòng mở lại ứng dụng.",
                "ZenBox",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            Shutdown(1);
        }
    }

    private void RegisterGlobalExceptionHandlers(ErrorHandlingService errorService)
    {
        DispatcherUnhandledException += (_, args) =>
        {
            args.Handled = true;
            errorService.ReportError(
                ErrorCategory.UnexpectedSystemError,
                "WPF DispatcherUnhandledException",
                args.Exception);
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            errorService.LogError(
                ErrorCategory.UnexpectedSystemError,
                "AppDomain UnhandledException",
                args.ExceptionObject as Exception);
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            args.SetObserved();
            errorService.LogError(
                ErrorCategory.UnexpectedSystemError,
                "TaskScheduler UnobservedTaskException",
                args.Exception);
        };
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mainViewModel?.Dispose();
        base.OnExit(e);
    }
}
