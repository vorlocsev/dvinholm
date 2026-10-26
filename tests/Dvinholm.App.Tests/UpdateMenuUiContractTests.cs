using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline contracts for the dedicated updater flyout. These validate
/// the actual XAML/handler wiring; interactive WinUI acceptance is separate.
/// </summary>
public sealed class UpdateMenuUiContractTests
{
    private static string Read(string path)
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var file = Path.Combine(dir.FullName, path.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(file))
                return File.ReadAllText(file);
        }

        throw new FileNotFoundException("Update flyout contract source not found.", path);
    }

    private static XDocument Xaml => XDocument.Parse(Read("src/Dvinholm.App/MainWindow.xaml"));
    private static string Code => Read("src/Dvinholm.App/MainWindow.xaml.cs");

    private static XElement NamedElement(XDocument xaml, string localName, string name) =>
        Assert.Single(xaml.Descendants(), element =>
            element.Name.LocalName == localName &&
            element.Attributes().Any(attr => attr.Name.LocalName == "Name" && attr.Value == name));

    [Fact]
    public void MainWindow_HasSingleCheckUpdateEntryThatOpensFlyout()
    {
        var xaml = Xaml;
        var entry = NamedElement(xaml, "Button", "UpdateButton");
        Assert.Equal("Check update", (string?)entry.Attribute("Content"));
        Assert.Null(entry.Attribute("Click"));
        var flyout = Assert.Single(entry.Descendants(), node => node.Name.LocalName == "Flyout");
        Assert.Equal("UpdateFlyout_Opened", (string?)flyout.Attribute("Opened"));
        Assert.Contains("await CheckLatestBuildAsync();", Code);
        Assert.Contains("if (_closing || _updaterBusy || _latestBuild is not null ||", Code);
    }

    [Fact]
    public void Menu_ContainsExplicitChannelSelectorAndBothChannels()
    {
        var xaml = Xaml;
        var menu = NamedElement(xaml, "Flyout", "UpdateFlyout");
        var selector = NamedElement(xaml, "ComboBox", "UpdateSource");
        Assert.Contains(menu, selector.Ancestors());
        Assert.Equal("UpdateSource_SelectionChanged", (string?)selector.Attribute("SelectionChanged"));
        Assert.Equal(new[] { "Development", "Stable" },
            selector.Elements().Where(node => node.Name.LocalName == "ComboBoxItem")
                .Select(node => (string?)node.Attribute("Content")));
    }

    [Fact]
    public void CurrentAndAvailableStatus_IsInsideMenu_NotInlineOnMainWindow()
    {
        var xaml = Xaml;
        var menu = NamedElement(xaml, "Flyout", "UpdateFlyout");
        var status = NamedElement(xaml, "TextBlock", "UpdateStatusText");
        Assert.Contains(menu, status.Ancestors());
        Assert.Equal("Wrap", (string?)status.Attribute("TextWrapping"));
    }

    [Fact]
    public void MenuActionIsSeparateButtonWiredToExistingVerifiedUpdater()
    {
        var xaml = Xaml;
        var menu = NamedElement(xaml, "Flyout", "UpdateFlyout");
        var action = NamedElement(xaml, "Button", "RunUpdateButton");
        Assert.Contains(menu, action.Ancestors());
        Assert.Equal("UpdateButton_Click", (string?)action.Attribute("Click"));
        Assert.Equal("Check for updates", (string?)action.Attribute("Content"));
    }

    [Fact]
    public void FlyoutUsesSavedChannelAfterXamlInitialization()
    {
        var cs = Code;
        var ctor = cs.IndexOf("public MainWindow()", StringComparison.Ordinal);
        var initialize = cs.IndexOf("InitializeComponent();", ctor, StringComparison.Ordinal);
        var load = cs.IndexOf("LoadSavedUpdateSource();", initialize, StringComparison.Ordinal);
        var enable = cs.IndexOf("_loadingUpdateSource = false;", load, StringComparison.Ordinal);
        Assert.True(ctor >= 0 && initialize > ctor && load > initialize && enable > load);
        Assert.Contains("UpdateSourcePreferenceStore(UpdateSourceSettingsFile).Load(fallback)", cs);
    }

    [Fact]
    public void ChangingChannelInvalidatesCachedOfferAndPersistsPreference()
    {
        var cs = Code;
        var start = cs.IndexOf("private void UpdateSource_SelectionChanged", StringComparison.Ordinal);
        var end = cs.IndexOf("private async void UpdateButton_Click", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var handler = cs[start..end];
        Assert.Contains("_loadingUpdateSource", handler);
        Assert.Contains(".Save(", handler);
        Assert.Contains("_latestBuild = null;", handler);
        Assert.Contains("_updateAvailable = false;", handler);
        Assert.Contains("RunUpdateButton.Content", handler);
    }

    [Fact]
    public void AllUpdaterBusyAndRetryStatesTargetMenuAction()
    {
        var cs = Code;
        var start = cs.IndexOf("private void UpdateSource_SelectionChanged", StringComparison.Ordinal);
        var end = cs.IndexOf("private static string ProfileCatalogPath", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var handlers = cs[start..end];
        Assert.DoesNotContain("UpdateButton.Content", handlers.Replace("RunUpdateButton.Content", ""));
        Assert.DoesNotContain("UpdateButton.IsEnabled", handlers.Replace("RunUpdateButton.IsEnabled", ""));
        Assert.Contains("RunUpdateButton.Content = \"Retry update check\"", handlers);
        Assert.Contains("RunUpdateButton.Content = _updateAvailable ? \"Replace files and restart\" : \"Check update\"", handlers);
    }

    [Fact]
    public void AcceptanceMenuRemainsInspectableButInstallerAndMsixStayBlocked()
    {
        var cs = Code;
        var acceptance = cs.IndexOf("if (DvinholmUserDataDirectory.IsAcceptanceLaunch)", StringComparison.Ordinal);
        var nextHandler = cs.IndexOf("_session.ReadError +=", acceptance, StringComparison.Ordinal);
        Assert.True(acceptance >= 0 && nextHandler > acceptance);
        var acceptanceSetup = cs[acceptance..nextHandler];
        Assert.Contains("RunUpdateButton.IsEnabled = false;", acceptanceSetup.Split('\n').Select(line => line.Trim()));
        Assert.DoesNotContain("UpdateButton.IsEnabled = false;", acceptanceSetup.Split('\n').Select(line => line.Trim()));
        Assert.DoesNotContain("UpdateSource.IsEnabled = false;", acceptanceSetup.Split('\n').Select(line => line.Trim()));
        Assert.Contains("ConnectButton.IsEnabled = false;", acceptanceSetup);

        // MSIX is still managed by its package manager; no portable updater.
        var msixStart = cs.IndexOf("#if DVINHOLM_MSIX", StringComparison.Ordinal);
        Assert.True(msixStart >= 0 && msixStart < acceptance);
        var msixSetup = cs[msixStart..acceptance];
        Assert.Contains("UpdateButton.IsEnabled = false;", msixSetup);
        Assert.Contains("UpdateSource.IsEnabled = false;", msixSetup);
    }
}
