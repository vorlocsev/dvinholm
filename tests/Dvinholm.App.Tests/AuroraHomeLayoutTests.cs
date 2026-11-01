using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline Aurora home contracts. These do not claim actual PIN lookup,
/// configuration writes, interactive UI acceptance or physical J2534 access.
/// </summary>
public sealed class AuroraHomeLayoutTests
{
    private static readonly XNamespace XamlName =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XDocument ReadXaml(string logicalName)
    {
        using var stream = typeof(AuroraHomeLayoutTests).Assembly
            .GetManifestResourceStream(logicalName)
            ?? throw new InvalidOperationException("Missing embedded UI resource: " + logicalName);
        return XDocument.Load(stream);
    }

    private static XElement Named(XDocument doc, string name) =>
        doc.Descendants().Single(x => (string?)x.Attribute(XamlName + "Name") == name);

    private static string Source(string relative)
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory);
             directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName,
                relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path)) return File.ReadAllText(path);
        }

        throw new FileNotFoundException(relative);
    }

    private static XDocument Home => ReadXaml("Dvinholm.App.Tests.AuroraHome.xaml");
    private static XDocument Main => ReadXaml("Dvinholm.App.Tests.MainWindow.xaml");

    [Fact]
    public void FourCardsFollowApprovedFunctionalPriority()
    {
        var cards = Home.Descendants()
            .Where(x => x.Name.LocalName == "Button")
            .Where(x => (string?)x.Attribute(XamlName + "Name") is
                "CemPinCard" or "CemConfigCard" or "EcuCard" or "DiagnosticsCard")
            .Select(x => (string?)x.Attribute(XamlName + "Name")).ToArray();
        Assert.Equal(new[] { "CemPinCard", "CemConfigCard", "EcuCard", "DiagnosticsCard" },
            cards);
    }

    [Fact]
    public void CEMPinAndConfigurationCannotPretendToBeLiveFeatures()
    {
        var pin = Named(Home, "CemPinCard");
        var config = Named(Home, "CemConfigCard");
        Assert.Equal("CardPin_Click", (string?)pin.Attribute("Click"));
        Assert.Equal("CardConfig_Click", (string?)config.Attribute("Click"));
        var pinLabels = string.Join(" ", pin.Descendants()
            .Where(x => x.Name.LocalName == "TextBlock")
            .Select(x => (string?)x.Attribute("Text") ?? string.Empty));
        var configLabels = string.Join(" ", config.Descendants()
            .Where(x => x.Name.LocalName == "TextBlock")
            .Select(x => (string?)x.Attribute("Text") ?? string.Empty));
        Assert.Contains("недоступен", pinLabels, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("офлайн", configLabels, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("без записи", (string?)config.Attribute("ToolTipService.ToolTip"));
    }

    [Fact]
    public void ECUAndDiagnosticsNavigateToExistingWorkspaces()
    {
        Assert.Equal("CardEcu_Click", (string?)Named(Home, "EcuCard").Attribute("Click"));
        Assert.Equal("CardDiagnostics_Click",
            (string?)Named(Home, "DiagnosticsCard").Attribute("Click"));
        var code = Source("src/Dvinholm.App/MainWindow.xaml.cs");
        Assert.Contains("EcuSelector.Focus(FocusState.Programmatic)", code);
        Assert.Contains("UdsRequestId.Focus(FocusState.Programmatic)", code);
    }

    [Fact]
    public void DashboardUsesRealConnectionProfileAndActivitySummary()
    {
        foreach (var name in new[]
                 { "ConnectionSummary", "AdapterSummary", "BaudSummary",
                   "ProfileSummary", "ActivitySummary" })
            Assert.NotNull(Named(Home, name));
        var code = Source("src/Dvinholm.App/AuroraHome.xaml.cs");
        Assert.Contains("UpdateConnection(bool connected", code);
        Assert.Contains("UpdateProfile(string? profile)", code);
        Assert.Contains("UpdateActivity(string activity)", code);
    }

    [Fact]
    public void OriginalDiagnosticWindowIsPreservedAndInitiallyHidden()
    {
        Assert.Equal("Collapsed", (string?)Named(Main, "AuroraWorkspaceHost")
            .Attribute("Visibility"));
        Assert.NotNull(Named(Main, "AuroraLanding"));
        foreach (var oldName in new[]
                 { "ConnectButton", "StartButton", "FramesView", "EcusView",
                   "ProfileSelector", "P2PinReviewButton", "P2ConfigCatalogButton" })
            Assert.NotNull(Named(Main, oldName));
    }

    [Fact]
    public void CEMCardsNavigateWithoutStartingServicesOrOpeningResearchDialogs()
    {
        var main = Source("src/Dvinholm.App/MainWindow.xaml.cs");
        var start = main.IndexOf("private void DashboardNavigate(", StringComparison.Ordinal);
        var end = main.IndexOf("private void ShowDashboardWorkspace()", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var region = main[start..end];
        Assert.Contains("AuroraWorkspaceNavigation.Describe(destination)", region);
        Assert.DoesNotContain("P2PinReview_Click(this, null!)", region);
        Assert.DoesNotContain("P2ConfigCatalog_Click(this, null!)", region);
        Assert.DoesNotContain("_session.", region);
        Assert.DoesNotContain("Connect_Click(", region);
        Assert.DoesNotContain("RunUdsOperationAsync", region);
    }

    [Fact]
    public void ExistingWorkspaceHasExplicitWayBackToHome()
    {
        var main = Source("src/Dvinholm.App/MainWindow.xaml.cs");
        Assert.Contains("DashboardReturn_Click", main);
        Assert.Contains("AuroraWorkspaceHost.Visibility = Visibility.Collapsed;", main);
        Assert.Contains("AuroraLanding.Visibility = Visibility.Visible;", main);
        Assert.Equal("DashboardReturn_Click", (string?)Main.Descendants()
            .Single(x => x.Name.LocalName == "Button" &&
                         (string?)x.Attribute("Click") == "DashboardReturn_Click")
            .Attribute("Click"));
    }

    [Fact]
    public void HeroUsesAlreadyOwnedBannerAndNoWebView()
    {
        var home = Home.ToString();
        Assert.Contains("Dvinholm-Banner.png", home);
        Assert.DoesNotContain("WebView2", home);
        var project = Source("src/Dvinholm.App/Dvinholm.App.csproj");
        Assert.Contains("dvinholm-banner.webp", project);
        Assert.Contains("CopyToPublishDirectory", project);
    }
}
