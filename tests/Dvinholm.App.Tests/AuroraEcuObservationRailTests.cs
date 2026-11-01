using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline gates for the Aurora side-view passive RX observation rail.
/// No module identity, physical placement or TX address is inferred.
/// </summary>
public sealed class AuroraEcuObservationRailTests
{
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XDocument Layout()
    {
        using var stream = typeof(AuroraEcuObservationRailTests).Assembly
            .GetManifestResourceStream("Dvinholm.App.Tests.MainWindow.xaml")
            ?? throw new InvalidOperationException("Missing embedded XAML");
        return XDocument.Load(stream);
    }

    private static XElement Named(XDocument doc, string name) =>
        doc.Descendants().Single(e => (string?)e.Attribute(X + "Name") == name);

    private static string Source(string path)
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var full = Path.Combine(dir.FullName, path.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(full)) return File.ReadAllText(full);
        }
        throw new FileNotFoundException(path);
    }

    [Fact]
    public void CompleteScanCreatesNumberedRawResponseTilesNotModuleNames()
    {
        var items = AuroraEcuObservationProjection.Project(new[]
        {
            new UdsEcuCandidate(0x7E8, 0x7E0, false, 3, 8),
            new UdsEcuCandidate(0x18DAF110, 0x18DA10F1, true, 2, 8)
        }, complete: true);
        Assert.Equal(2, items.Count);
        Assert.Equal("#1 · RX 0x7E8", items[0].Label);
        Assert.Equal("#2 · RX 0x18DAF110", items[1].Label);
        Assert.Equal(0, items[0].Index);
        Assert.Equal(1, items[1].Index);
        Assert.All(items, t => Assert.True(t.Selectable));
        Assert.All(items, t => Assert.Contains("unverified", t.Hint));
        Assert.DoesNotContain(items, t => t.Label.Contains("CEM", StringComparison.Ordinal));
    }

    [Fact]
    public void IncompleteScanShowsObservationsButNeverAllowsSelection()
    {
        var tiles = AuroraEcuObservationProjection.Project(
            new[] { new UdsEcuCandidate(0x7E8, 0x7E0, false, 1, 8) }, complete: false);
        Assert.Single(tiles);
        Assert.False(tiles[0].Selectable);
    }

    [Fact]
    public void EmptyAndNullInputsAreSafeAndNeverInventResponses()
    {
        Assert.Empty(AuroraEcuObservationProjection.Project(Array.Empty<UdsEcuCandidate>(), true));
        Assert.Empty(AuroraEcuObservationProjection.Project(Array.Empty<UdsEcuCandidate>(), false));
        Assert.Throws<ArgumentNullException>(() =>
            AuroraEcuObservationProjection.Project(null!, true));
    }

    [Fact]
    public void TileCountIsBoundedAndOrderPreserved()
    {
        var candidates = Enumerable.Range(0, AuroraEcuObservationProjection.MaxVisibleTiles + 3)
            .Select(i => new UdsEcuCandidate((uint)(0x700 + i), null, false, 1, 8)).ToArray();
        var tiles = AuroraEcuObservationProjection.Project(candidates, true);
        Assert.Equal(AuroraEcuObservationProjection.MaxVisibleTiles, tiles.Count);
        Assert.Equal("#1 · RX 0x700", tiles[0].Label);
        Assert.Equal($"#{tiles.Count} · RX 0x{(0x700 + tiles.Count - 1):X3}", tiles[^1].Label);
    }

    [Fact]
    public void InvalidOrOutOfRangeResponseDataFailsClosed()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AuroraEcuObservationProjection.Project(
                new[] { new UdsEcuCandidate(0x800, null, false, 1, 8) }, true));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AuroraEcuObservationProjection.Project(
                new[] { new UdsEcuCandidate(0x20000000, null, true, 1, 8) }, true));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AuroraEcuObservationProjection.Project(
                new[] { new UdsEcuCandidate(0x7E8, null, false, -1, 8) }, true));
        Assert.Throws<ArgumentException>(() =>
            AuroraEcuObservationProjection.Project(new UdsEcuCandidate[] { null! }, true));
    }

    [Fact]
    public void RailIsOutsideSideProfileAndExplainsItIsNotPhysicalPlacement()
    {
        var doc = Layout();
        var rail = Named(doc, "EcuObservationRail");
        Assert.Contains(Named(doc, "EcuPage"), rail.Ancestors());
        Assert.DoesNotContain(Named(doc, "EcuP2SideProfile"), rail.Ancestors());
        Assert.DoesNotContain(Named(doc, "EcuP3SideProfile"), rail.Ancestors());
        Assert.Equal("Horizontal", (string?)rail.Attribute("Orientation"));
        Assert.Equal("Auto", (string?)rail.Parent?.Attribute("HorizontalScrollBarVisibility"));
        Assert.Contains("не позиции ECU", Named(doc, "EcuPage").ToString());
        Assert.NotNull(Named(doc, "EcuObservationRailSummary"));
    }

    [Fact]
    public void SelectionUsesExistingPassiveListAndRejectsIncompleteScan()
    {
        var source = Source("src/Dvinholm.App/MainWindow.AuroraEcuObservations.cs");
        Assert.Contains("if (!_ecuScanComplete", source);
        Assert.Contains("!EcusView.IsEnabled", source);
        Assert.Contains("EcusView.SelectedIndex = index;", source);
        Assert.DoesNotContain("UdsRequestId.Text =", source);
        Assert.DoesNotContain("ReadDid_Click(", source);
        Assert.DoesNotContain("ReadDtc_Click(", source);
        Assert.DoesNotContain("_session.", source);
    }

    [Fact]
    public void RescanAndDisconnectResetTilesAndExistingRequestAddressGuards()
    {
        var main = Source("src/Dvinholm.App/MainWindow.xaml.cs");
        var controls = Source("src/Dvinholm.App/MainWindow.AuroraEcuObservations.cs");
        Assert.Contains("ClearAuroraEcuObservationRail();", main);
        Assert.Contains("ShowAuroraEcuObservationRail(found, complete);", main);
        Assert.Contains("HighlightAuroraEcuObservation(index);", main);
        Assert.Contains("EcuObservationRail.Children.Clear();", controls);
        Assert.Contains("UdsRequestId.Text = string.Empty;", main);
        Assert.Contains("!ReferenceEquals(_ecus[index], row)", main);
    }
}
