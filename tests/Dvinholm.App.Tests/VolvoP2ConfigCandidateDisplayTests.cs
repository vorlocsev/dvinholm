using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight deterministic, offline UI/presentation checks. No WinUI window,
/// J2534 adapter, PIN operation, imported snapshot or CEM is used.
/// </summary>
public sealed class VolvoP2ConfigCandidateDisplayTests
{
    private static string Overview() => VolvoP2ConfigCandidateDisplay.BuildOverview();

    [Fact]
    public void OverviewContainsEveryCanonicalCandidateExactlyOnce()
    {
        var overview = Overview();
        foreach (var candidate in VolvoP2ConfigOptionCatalog.Entries)
            Assert.Equal(1, CountOccurrences(overview, candidate.CanonicalId));
        Assert.Equal(8, VolvoP2ConfigOptionCatalog.Entries.Count);
    }

    [Fact]
    public void OverviewIdentifiesItselfAsOfflineResearchNotRealVehicleSettings()
    {
        var overview = Overview();
        Assert.Contains("offline research", overview);
        Assert.Contains("NOT VEHICLE SETTINGS", overview);
        Assert.Contains("This viewer neither identifies nor contacts any vehicle", overview);
        Assert.Contains("Actual model year", overview);
    }

    [Fact]
    public void CurrentAndDesiredAreNeverFabricatedFromCatalogNames()
    {
        var overview = Overview();
        Assert.Contains("Current values: unavailable", overview);
        Assert.Contains("Desired values: disabled", overview);
        Assert.Equal(8, CountOccurrences(overview, "Module, firmware, raw mapping: unknown"));
    }

    [Fact]
    public void SourceAndAdvertisedYearAreExplicitlyUnverified()
    {
        var overview = Overview();
        Assert.Contains("user-provided third-party screenshots", overview);
        Assert.Contains("advertisement only", overview);
        Assert.Contains("MY2006+", overview);
        Assert.Equal(8, CountOccurrences(overview, "Claimed model years: 2006–2014"));
        Assert.Equal(8, CountOccurrences(overview, "Verification: ModelYearUnconfirmed"));
    }

    [Fact]
    public void PreviewNeverEnablesPinReadOrWriteOperations()
    {
        var overview = Overview();
        Assert.Contains("PIN/service authentication: unavailable", overview);
        Assert.Contains("ECU writing: disabled", overview);
        Assert.Contains("This screen cannot request a PIN, read or alter a vehicle", overview);
        Assert.Equal(8, CountOccurrences(overview, "Read/write disabled"));
    }

    [Fact]
    public void SafetySensitiveCategoriesAreConservativelyMarked()
    {
        var overview = Overview();
        Assert.Contains("• Speed limit policy [VehicleSpeedPolicy]", overview);
        Assert.Contains("• Automatic locking [LockingAndAlarm]", overview);
        Assert.True(CountOccurrences(overview, "required before any vehicle operation") >= 2);
        Assert.Contains("Service interval [Maintenance]", overview);
    }

    [Fact]
    public void MainWindowHasExplicitOfflineActionOutsideSevenButtonProfileGrid()
    {
        var button = Named("P2ConfigCatalogButton");
        Assert.Equal("Button", button.Name.LocalName);
        Assert.Equal("P2ConfigCatalog_Click", button.Attribute("Click")?.Value);
        Assert.Contains("offline", button.Attribute("Content")?.Value,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no vehicle", button.Attribute("ToolTipService.ToolTip")?.Value,
            StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual("ProfileActionsPanel",
            button.Parent?.Attribute(XName.Get("Name",
                "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value);
    }

    [Fact]
    public void PreviewActionCannotInterfereWithCompactSnapshotLayout()
    {
        var panel = Named("ProfileActionsPanel");
        Assert.Equal(7, panel.Descendants()
            .Count(e => e.Name.LocalName == "Button"));
        var button = Named("P2ConfigCatalogButton");
        Assert.Contains("CemConfigPage", button.Ancestors()
            .Select(e => e.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value));
        var metadata = Named("EcuInfoText");
        Assert.Equal("2", metadata.Parent?.Attribute("Grid.Row")?.Value);
        Assert.Equal("76", metadata.Parent?.Attribute("MaxHeight")?.Value);
    }

    private static XElement Named(string name)
    {
        using var stream = typeof(VolvoP2ConfigCandidateDisplayTests).Assembly
            .GetManifestResourceStream("Dvinholm.App.Tests.MainWindow.xaml")
            ?? throw new InvalidOperationException("MainWindow.xaml resource missing.");
        return XDocument.Load(stream).Descendants().Single(e =>
            e.Attribute(XName.Get("Name",
                "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == name);
    }

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var start = 0;
        while (true)
        {
            var index = source.IndexOf(value, start, StringComparison.Ordinal);
            if (index < 0) return count;
            count++;
            start = index + value.Length;
        }
    }
}
