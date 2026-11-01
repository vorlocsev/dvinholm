using System.Xml.Linq;

namespace Dvinholm.App.Tests;

public sealed class AuroraSectionNavigationTests
{
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static XDocument Document()
    {
        using var stream = typeof(AuroraSectionNavigationTests).Assembly.GetManifestResourceStream("Dvinholm.App.Tests.MainWindow.xaml");
        return XDocument.Load(stream!);
    }
    private static XElement Named(XDocument doc, string name) => doc.Descendants().Single(e => (string?)e.Attribute(X + "Name") == name);
    private static void Owns(string page, params string[] names)
    {
        var doc = Document();
        foreach (var name in names) Assert.Contains(Named(doc, page), Named(doc, name).Ancestors());
    }

    [Fact]
    public void EveryDestinationHasItsOwnInitiallyHiddenPage()
    {
        var doc = Document();
        var sections = Enum.GetValues<AuroraDestination>().Select(AuroraWorkspaceNavigation.Describe).ToArray();
        Assert.Equal(7, sections.Select(s => s.Panel).Distinct().Count());
        Assert.All(sections, section =>
        {
            Assert.Equal("Collapsed", (string?)Named(doc, section.Panel).Attribute("Visibility"));
            Assert.False(string.IsNullOrWhiteSpace(section.Title));
            Assert.False(string.IsNullOrWhiteSpace(section.Description));
        });
        Assert.Equal("Collapsed", (string?)Named(doc, "AuroraWorkspaceHost").Attribute("Visibility"));
        Assert.Null(Named(doc, "AuroraLanding").Attribute("Visibility"));
    }

    [Fact]
    public void PersistentSidebarReachesEveryPageAndRemainsOutsideWorkspaceVisibility()
    {
        var doc = Document();
        var sidebar = Named(doc, "SidebarNavPanel");
        var tags = sidebar.Elements().Where(e => e.Attribute("Tag") is not null).Select(e => (string)e.Attribute("Tag")!).ToArray();
        Assert.Equal(Enum.GetValues<AuroraDestination>().Length, tags.Length);
        Assert.All(tags, tag => Assert.True(AuroraWorkspaceNavigation.TryParse(tag, out _)));
        Assert.DoesNotContain(Named(doc, "AuroraWorkspaceHost"), sidebar.Ancestors());
        Assert.All(sidebar.Elements().Where(e => e.Attribute("Tag") is not null),
            e => Assert.Equal("WorkspaceNavigate_Click", (string?)e.Attribute("Click")));
    }

    [Fact]
    public void CemPinHasItsOwnIdentityRangeVaultAndReviewControls() =>
        Owns("CemPinPage", "CemPartNumberReadButton", "CemPinLivePanel", "CemPinSavedButton", "P2PinReviewButton");

    [Fact]
    public void ConfigurationCatalogIsASeparateDestinationWithExplicitOfflineAction()
    {
        Owns("CemConfigPage", "P2ConfigCatalogButton");
        var doc = Document();
        Assert.DoesNotContain(Named(doc, "ProfilesPage"), Named(doc, "P2ConfigCatalogButton").Ancestors());
        Assert.Contains("офлайн", Named(doc, "CemConfigPage").ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EcuDiscoveryAndProfilesAreSeparated()
    {
        Owns("EcuPage", "DiscoverButton", "EcuSelector", "EcusView", "DiscoveryStatus");
        Owns("ProfilesPage", "ProfileSearch", "ProfileSelector", "ProfileActionsPanel", "EcuInfoText");
    }

    [Fact]
    public void DiagnosticsOwnsAddressingAndReadsWhileConnectionOwnsAdapterSetup()
    {
        Owns("DiagnosticsPage", "UdsRequestId", "UdsResponseId", "CanIdFormat", "ReadVinButton", "ReadDidButton", "ReadDtcButton");
        Owns("ConnectionPage", "DllPath", "J2534Devices", "BaudRate", "CanConnectionMode", "ConnectButton", "AdapterMetadataText");
        Assert.Equal("Disabled", (string?)Named(Document(), "WorkspaceScroll").Attribute("HorizontalScrollBarVisibility"));
    }

    [Fact]
    public void CanMonitorOwnsItsCaptureAndAnalysisControls()
    {
        Owns("CanMonitorPage", "StartButton", "DeveloperMonitorHost", "IdFilter", "HardwareFilterButton", "FramesView", "FrameRateText");
        Assert.Equal("Dark", (string?)Named(Document(), "RootLayout").Attribute("RequestedTheme"));
        Assert.Equal("{Binding Text, ElementName=StatusText}", (string?)Named(Document(), "ShellStatusText").Attribute("Text"));
    }

    [Fact]
    public void InvalidRoutesCannotSelectAServiceAndAllControlNamesRemainUnique()
    {
        foreach (var tag in new string?[] { null, "", "999", "Connect_Click", "ReadCemPin_Click" })
            Assert.False(AuroraWorkspaceNavigation.TryParse(tag, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => AuroraWorkspaceNavigation.Describe((AuroraDestination)999));
        var names = Document().Descendants().Select(e => (string?)e.Attribute(X + "Name")).Where(n => n is not null).ToArray();
        Assert.Equal(names.Length, names.Distinct().Count());
    }
}
