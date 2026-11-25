using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using ZenBox.ViewModels;

namespace ZenBox.Views;

public partial class MediaPlayerView : UserControl
{
    private bool _isDraggingThumb;

    public MediaPlayerView()
    {
        InitializeComponent();
    }

    private MediaPlayerViewModel? ViewModel => DataContext as MediaPlayerViewModel;

    private void OnMediaPlayerLoaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } vm)
        {
            return;
        }

        // Gắn MediaPlayer vào VideoView sau khi HWND của VideoView đã sẵn sàng rồi mới bắt đầu phát
        var player = vm.EnsurePlayerReady();
        VlcVideoSurface.MediaPlayer = player;

        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            vm.StartPendingPlayback();
        });
    }

    private void OnMediaPlayerUnloaded(object sender, RoutedEventArgs e)
    {
        VlcVideoSurface.MediaPlayer = null;
    }

    private void OnSliderDragStarted(object sender, DragStartedEventArgs e)
    {
        _isDraggingThumb = true;
        ViewModel?.BeginSliderDrag();
    }

    private void OnSliderDragCompleted(object sender, DragCompletedEventArgs e)
    {
        _isDraggingThumb = false;
        ViewModel?.CompleteSliderDrag(TimelineSlider.Value);
    }

    private void OnSliderMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDraggingThumb)
        {
            ViewModel?.CompleteSliderDrag(TimelineSlider.Value);
        }
    }
}
