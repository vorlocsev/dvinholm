using Microsoft.UI.Windowing;
using Windows.UI;

namespace Dvinholm.App;

public sealed partial class MainWindow
{
    /// <summary>
    /// Match the native Windows 11 caption bar to Aurora's midnight palette
    /// while preserving native dragging, the system menu, caption buttons,
    /// DPI frame metrics and the fixed 1280 x 800 DIP client viewport.
    ///
    /// This is intentionally SIMPLE customization: do not extend XAML into
    /// the title bar, replace drag rectangles or build custom close controls.
    /// On Windows versions where native color customization is unavailable
    /// (or ignored, including Windows 10), keep the system title bar.
    /// </summary>
    private void ConfigureAuroraNativeTitleBar()
    {
        if (!AppWindowTitleBar.IsCustomizationSupported())
            return;

        var titleBar = AppWindow.TitleBar;
        var midnight = Color.FromArgb(255, 9, 29, 50);       // #091D32
        var inactiveNavy = Color.FromArgb(255, 19, 38, 60);  // #13263C
        var label = Color.FromArgb(255, 245, 250, 255);      // #F5FAFF
        var inactiveLabel = Color.FromArgb(255, 164, 188, 207);
        var hoverNavy = Color.FromArgb(255, 35, 69, 105);
        var pressedNavy = Color.FromArgb(255, 50, 87, 118);

        // Native frame, live title, system drag area and window-menu icon.
        titleBar.BackgroundColor = midnight;
        titleBar.ForegroundColor = label;
        titleBar.InactiveBackgroundColor = inactiveNavy;
        titleBar.InactiveForegroundColor = inactiveLabel;

        // Minimize/maximize/close remain system-managed; Windows owns
        // close-button hover red and accessibility semantics.
        titleBar.ButtonBackgroundColor = midnight;
        titleBar.ButtonForegroundColor = label;
        titleBar.ButtonHoverBackgroundColor = hoverNavy;
        titleBar.ButtonHoverForegroundColor = label;
        titleBar.ButtonPressedBackgroundColor = pressedNavy;
        titleBar.ButtonPressedForegroundColor = label;
        titleBar.ButtonInactiveBackgroundColor = inactiveNavy;
        titleBar.ButtonInactiveForegroundColor = inactiveLabel;
    }
}
