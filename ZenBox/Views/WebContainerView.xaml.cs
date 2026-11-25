using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ZenBox.Models;
using ZenBox.ViewModels;

namespace ZenBox.Views;

public partial class WebContainerView : UserControl
{
    private WebContainerViewModel? _boundVm;

    public WebContainerView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_boundVm is not null)
        {
            _boundVm.TabActivationRequested -= OnTabActivationRequested;
            _boundVm.WarmupRequested -= OnWarmupRequested;
        }

        _boundVm = e.NewValue as WebContainerViewModel;
        if (_boundVm is not null)
        {
            _boundVm.TabActivationRequested += OnTabActivationRequested;
            _boundVm.WarmupRequested += OnWarmupRequested;
        }
    }

    private async void OnWarmupRequested(object? sender, EventArgs e)
    {
        if (_boundVm is null)
        {
            return;
        }

        await _boundVm.BrowserService.PrewarmEnvironmentAndExtensionAsync(BrowserHostGrid);
    }

    private async void OnTabActivationRequested(object? sender, (AppSection Section, string Url) req)
    {
        if (_boundVm is null)
        {
            return;
        }

        await _boundVm.BrowserService.GetOrCreateTabAsync(req.Section, req.Url, BrowserHostGrid);
    }

    private void OnAddressBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _boundVm is not null)
        {
            _boundVm.NavigateToAddressCommand.Execute(null);
        }
    }
}
