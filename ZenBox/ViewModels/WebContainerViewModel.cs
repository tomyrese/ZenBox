using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZenBox.Models;
using ZenBox.Services;

namespace ZenBox.ViewModels;

public sealed partial class WebContainerViewModel : ObservableObject
{
    private readonly WebBrowserService _browserService;
    private readonly Action _goHomeCallback;

    [ObservableProperty]
    private string _channelTitle = string.Empty;

    [ObservableProperty]
    private string _addressText = "https://www.google.com";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isSearchBarVisible;

    [ObservableProperty]
    private bool _isAppSplashVisible;

    [ObservableProperty]
    private ImageSource? _activeAppLogo;

    public WebBrowserService BrowserService => _browserService;

    public event EventHandler<(AppSection Section, string Url)>? TabActivationRequested;
    public event EventHandler? WarmupRequested;

    public void TriggerBackgroundWarmup()
    {
        WarmupRequested?.Invoke(this, EventArgs.Empty);
    }

    public WebContainerViewModel(WebBrowserService browserService, Action goHomeCallback)
    {
        _browserService = browserService;
        _goHomeCallback = goHomeCallback;

        _browserService.ActiveTitleChanged += (_, title) =>
        {
            if (!string.IsNullOrWhiteSpace(title))
            {
                ChannelTitle = title;
            }
        };

        _browserService.LoadingStateChanged += (_, loading) =>
        {
            IsLoading = loading;
        };

        _browserService.AppSplashVisibilityChanged += (_, visible) =>
        {
            IsAppSplashVisible = visible;
        };
    }

    public void OpenChannel(AppSection section, string url, string displayName)
    {
        ChannelTitle = displayName;
        ActiveAppLogo = BrandLogoFactory.GetLogo(section);

        // Chỉ hiện thanh tìm kiếm gọn khi dùng Google Web; tất cả ứng dụng (YouTube, Netflix, Spotify, TikTok...) đều tràn viền 100% như Native App!
        IsSearchBarVisible = section == AppSection.WebBrowser;
        if (IsSearchBarVisible)
        {
            AddressText = url;
        }

        TabActivationRequested?.Invoke(this, (section, url));
    }

    [RelayCommand]
    private void GoBack()
    {
        _browserService.NavigateActiveTabBack();
    }

    [RelayCommand]
    private void ReloadPage()
    {
        _browserService.ReloadActiveTab();
    }

    [RelayCommand]
    private void NavigateToAddress()
    {
        _browserService.NavigateActiveTabToUrl(AddressText);
    }

    [RelayCommand]
    private void GoHome()
    {
        _goHomeCallback();
    }
}
