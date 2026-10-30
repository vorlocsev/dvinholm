using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Dvinholm.App;

/// <summary>
/// Presentation-only home navigation. No CAN request, PIN search, configuration
/// write, installer call or network request originates from this control.
/// </summary>
public enum AuroraDestination
{
    CemPinReadiness,
    CemConfigurationCatalog,
    EcuWorkspace,
    DiagnosticsWorkspace,
    CanMonitor,
    EcuProfiles,
    J2534Connection
}

public sealed partial class AuroraHome : UserControl
{
    public event EventHandler<AuroraDestination>? NavigationRequested;

    public AuroraHome() => InitializeComponent();

    public void UpdateConnection(bool connected, string? adapter, string? baud)
    {
        ConnectionSummary.Text = connected ? "J2534 подключён" : "Не подключено";
        ConnectionSummary.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
            connected
                ? Windows.UI.Color.FromArgb(255, 111, 232, 181)
                : Windows.UI.Color.FromArgb(255, 245, 197, 143));
        AdapterSummary.Text = string.IsNullOrWhiteSpace(adapter)
            ? "Адаптер не выбран"
            : adapter;
        BaudSummary.Text = string.IsNullOrWhiteSpace(baud) ? "CAN: неизвестно" : "CAN: " + baud;
    }

    public void UpdateProfile(string? profile) =>
        ProfileSummary.Text = string.IsNullOrWhiteSpace(profile) ? "Профиль не выбран" : profile;

    public void UpdateActivity(string activity) =>
        ActivitySummary.Text = string.IsNullOrWhiteSpace(activity)
            ? "Готово. Работа с автомобилем не начата."
            : activity;

    // The Home item is visible only on the landing view. Make it useful:
    // return to the banner after the user has scrolled through the cards.
    // This does not change connection state or start any diagnostic request.
    private void Home_Click(object sender, RoutedEventArgs e) =>
        DashboardScroll.ChangeView(null, 0.0, null);

    private void Navigate(AuroraDestination destination) =>
        NavigationRequested?.Invoke(this, destination);

    private void CardPin_Click(object sender, RoutedEventArgs e) =>
        Navigate(AuroraDestination.CemPinReadiness);

    private void CardConfig_Click(object sender, RoutedEventArgs e) =>
        Navigate(AuroraDestination.CemConfigurationCatalog);

    private void CardEcu_Click(object sender, RoutedEventArgs e) =>
        Navigate(AuroraDestination.EcuWorkspace);

    private void CardDiagnostics_Click(object sender, RoutedEventArgs e) =>
        Navigate(AuroraDestination.DiagnosticsWorkspace);

    private void Monitor_Click(object sender, RoutedEventArgs e) =>
        Navigate(AuroraDestination.CanMonitor);

    private void Profiles_Click(object sender, RoutedEventArgs e) =>
        Navigate(AuroraDestination.EcuProfiles);

    private void Connection_Click(object sender, RoutedEventArgs e) =>
        Navigate(AuroraDestination.J2534Connection);
}
