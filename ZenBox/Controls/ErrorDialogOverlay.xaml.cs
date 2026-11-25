using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ZenBox.Controls;

public partial class ErrorDialogOverlay : UserControl
{
    public ErrorDialogOverlay()
    {
        InitializeComponent();
    }

    private void OnOverlayIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            var ease = new QuinticEase { EasingMode = EasingMode.EaseOut };
            var duration = new Duration(TimeSpan.FromMilliseconds(280));

            OverlayBackdrop.BeginAnimation(OpacityProperty, new DoubleAnimation(0.0, 1.0, new Duration(TimeSpan.FromMilliseconds(200))));
            DialogScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.92, 1.0, duration) { EasingFunction = ease });
            DialogScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.92, 1.0, duration) { EasingFunction = ease });
            DialogTranslate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(20.0, 0.0, duration) { EasingFunction = ease });
        }
    }
}
