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
    DtcFaults,
    EcuWorkspace,
    DiagnosticsWorkspace,
    CanMonitor,
    EcuProfiles,
    J2534Connection
}

public sealed partial class AuroraHome : UserControl
{
    public event EventHandler<AuroraDestination>? NavigationRequested;

    public AuroraHome()
    {
        InitializeComponent();
        var bannerPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Dvinholm-Banner.png");
        if (File.Exists(bannerPath))
            HomeBannerImage.ImageSource = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(bannerPath));
#if DVINHOLM_DEVELOPMENT_FEATURES
        var russian = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru";
        CemPinSubtitle.Text = russian ? "Ограниченный поиск через J2534" : "Bounded search through J2534";
        CemPinAvailability.Text = russian ? "Эксперимент · нужен сервисный контекст CEM" : "Experimental · requires CEM service context";
        CemPinOpenLabel.Text = russian ? "Открыть CEM  ›" : "Open CEM  ›";
        ToolTipService.SetToolTip(CemPinCard, russian
            ? "Чтение номера CEM и ограниченный поиск PIN. Физическая совместимость не проверена."
            : "Read CEM part number and check a bounded PIN range. Physical compatibility is unverified.");
#endif
    }

    internal void UseSharedNavigation()
    {
        HomeSidebar.Visibility = Visibility.Collapsed;
        HomeSidebarColumn.Width = new GridLength(0);
    }

    internal void ReturnToTop() => DashboardScroll.ChangeView(null, 0.0, null);

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
