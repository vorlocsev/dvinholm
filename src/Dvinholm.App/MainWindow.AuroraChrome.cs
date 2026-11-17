using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.UI;

namespace Dvinholm.App;

public sealed partial class MainWindow
{
    /// <summary>
    /// Reuses the approved cyan-to-violet Aurora selection treatment.
    /// This is paint only: the active destination, handler, connection state,
    /// update channel and diagnostic context are not modified.
    /// </summary>
    private static void ApplyAuroraNavigationSurface(Button button, bool selected)
    {
        ArgumentNullException.ThrowIfNull(button);
        if (!selected)
        {
            button.Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
            button.BorderBrush = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
            button.BorderThickness = new Thickness(0);
            return;
        }

        var background = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 0)
        };
        background.GradientStops.Add(new GradientStop
        {
            Color = Color.FromArgb(255, 10, 93, 179), Offset = 0
        });
        background.GradientStops.Add(new GradientStop
        {
            Color = Color.FromArgb(255, 43, 64, 143), Offset = 0.65
        });
        background.GradientStops.Add(new GradientStop
        {
            Color = Color.FromArgb(255, 75, 47, 128), Offset = 1
        });
        button.Background = background;
        button.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 86, 172, 235));
        button.BorderThickness = new Thickness(1);
    }
}
