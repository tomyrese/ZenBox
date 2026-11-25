using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
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
