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
    private string _channelTitle = "Giải trí trực tuyến";

    [ObservableProperty]
    private string _addressText = "https://www.google.com";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isAddressBarVisible;

    [ObservableProperty]
    private bool _isToolbarVisible = true;

    public WebBrowserService BrowserService => _browserService;

    public event EventHandler<(AppSection Section, string Url)>? TabActivationRequested;

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

        _browserService.FullscreenStateChanged += (_, isFullscreen) =>
        {
            IsToolbarVisible = !isFullscreen;
        };
    }

    public void OpenChannel(AppSection section, string url, string displayName)
    {
        ChannelTitle = displayName;
        IsToolbarVisible = true;
        IsAddressBarVisible = section == AppSection.WebBrowser;
        if (IsAddressBarVisible)
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
