using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline GUI contracts for the approved CEM PIN workspace in the
/// self-contained Release build. No WinUI launch or J2534 vehicle session.
/// </summary>
public sealed class CemSafeReleaseWorkspaceTests
{
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XDocument Layout()
    {
        using var stream = typeof(CemSafeReleaseWorkspaceTests).Assembly
            .GetManifestResourceStream("Dvinholm.App.Tests.MainWindow.xaml")
            ?? throw new InvalidOperationException("CEM XAML fixture missing.");
        return XDocument.Load(stream);
    }

    private static XElement Named(XDocument document, string name) =>
        document.Descendants().Single(node =>
            (string?)node.Attribute(X + "Name") == name);

    private static string ReadSource(string path)
    {
        for (DirectoryInfo? folder = new(AppContext.BaseDirectory);
             folder is not null; folder = folder.Parent)
        {
            var candidate = Path.Combine(folder.FullName,
                path.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
        }
        throw new FileNotFoundException(path);
    }

    [Fact]
    public void IdentityCardIsVisibleByDefaultInsideCemPinWorkspace()
    {
        var layout = Layout();
        Assert.Equal("Collapsed", (string?)Named(layout, "CemPinPage").Attribute("Visibility"));
        var card = Named(layout, "CemPinIdentityCard");
        Assert.Equal("Visible", (string?)card.Attribute("Visibility"));
        Assert.Contains(card, Named(layout, "CemPinPage").Descendants());
        Assert.Equal("StackPanel", card.Elements().Single().Name.LocalName);
    }

    [Fact]
    public void LocalPrerequisiteButtonAndResultAreReachableInRelease()
    {
        var layout = Layout();
        var card = Named(layout, "CemPinIdentityCard");
        var button = Named(layout, "CemPartPrerequisiteButton");
        var result = Named(layout, "CemPartPrerequisiteExpander");
        Assert.Contains(button, card.Descendants());
        Assert.Contains(result, card.Descendants());
        Assert.Null(button.Attribute("Visibility"));
        Assert.Null(button.Attribute("IsEnabled"));
        Assert.Equal("CheckCemPrerequisites_Click", (string?)button.Attribute("Click"));
        Assert.Equal("False", (string?)result.Attribute("IsExpanded"));
        Assert.Contains(Named(layout, "CemPartPrerequisiteText"), result.Descendants());
    }

    [Fact]
    public void ExperimentalPartNumberButtonRemainsHiddenAndDisabled()
    {
        var button = Named(Layout(), "CemPartNumberReadButton");
        Assert.Equal("Collapsed", (string?)button.Attribute("Visibility"));
        Assert.Equal("False", (string?)button.Attribute("IsEnabled"));
        Assert.Equal("ReadCemPartNumber_Click", (string?)button.Attribute("Click"));
    }

    [Fact]
    public void ExperimentalPinSearchCardRemainsCollapsedByDefault()
    {
        var layout = Layout();
        Assert.Equal("Collapsed", (string?)Named(layout, "CemPinSearchCard").Attribute("Visibility"));
        Assert.Equal("Collapsed", (string?)Named(layout, "CemPinLivePanel").Attribute("Visibility"));
        Assert.Equal("Collapsed", (string?)Named(layout, "CemPinSavedButton").Attribute("Visibility"));
    }

    [Fact]
    public void PinSearchAndRevealButtonsCannotAccidentallyEnableOnReleaseStartup()
    {
        var layout = Layout();
        Assert.Equal("False", (string?)Named(layout, "CemPinReadButton").Attribute("IsEnabled"));
        Assert.Equal("False", (string?)Named(layout, "CemPinRevealButton").Attribute("IsEnabled"));
        Assert.Equal("False", (string?)Named(layout, "CemPinResultText").Attribute("IsTextSelectionEnabled"));
        Assert.Equal("No PIN result.", (string?)Named(layout, "CemPinResultText").Attribute("Text"));
    }

    [Fact]
    public void BothIdentityCardLabelsAreLocallyTranslatedAndNeverImplyPermission()
    {
        var layout = Layout();
        var card = Named(layout, "CemPinIdentityCard");
        foreach (var name in new[] { "CemPinIdentitySectionTitle", "CemPinIdentitySectionIntro" })
            Assert.Contains(Named(layout, name), card.Descendants());

        var code = ReadSource("src/Dvinholm.App/MainWindow.CemPin.cs");
        Assert.Contains("CemPinIdentitySectionTitle.Text = L(", code);
        Assert.Contains("CemPinIdentitySectionIntro.Text = L(", code);
        Assert.Contains("experimental reading is Development-only", code);
        Assert.Contains("экспериментальное чтение доступно только в Development", code);
        Assert.DoesNotContain("CemPinIdentitySectionIntro.Text = identity.PartNumber", code);
    }

    [Fact]
    public void StableNeverClaimsResearchContextOrVehicleAuthorization()
    {
        var code = ReadSource("src/Dvinholm.App/MainWindow.CemPin.cs");
        var start = code.IndexOf("private CemReadOnlyPrerequisiteAssessment InspectCemReadOnlyPrerequisites(",
            StringComparison.Ordinal);
        var end = code.IndexOf("private void CheckCemPrerequisites_Click(", start,
            StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        Assert.Contains("const bool safeResearchContext = false", code[start..end]);

        var report = CemReadOnlyPrerequisiteReport.Assess(
            new(true, true, true, true, true, true, true, false));
        Assert.False(report.EligibleForSourceDefinedPartReadOnly);
        Assert.False(report.MayAcquirePin);
        Assert.False(report.MayWriteConfiguration);
    }

    [Fact]
    public void BothWorkspacesRetainReadOnlyIdentityAndLocalizationPath()
    {
        var layout = Layout();
        Assert.Contains(Named(layout, "CemIdentityEvidencePinText"),
            Named(layout, "CemPinPage").Descendants());
        Assert.Contains(Named(layout, "CemIdentityEvidenceConfigText"),
            Named(layout, "CemConfigPage").Descendants());
        var code = ReadSource("src/Dvinholm.App/MainWindow.CemPin.cs");
        var a = code.IndexOf("private void RefreshCemIdentityEvidencePresentation()",
            StringComparison.Ordinal);
        var b = code.IndexOf("private void CemPinRangeChanged(", a, StringComparison.Ordinal);
        Assert.True(a >= 0 && b > a);
        var body = code[a..b];
        Assert.Contains("CemIdentityEvidencePinText.Text = text;", body);
        Assert.Contains("CemIdentityEvidenceConfigText.Text = text;", body);
        Assert.DoesNotContain("Task.Run", body);
        Assert.DoesNotContain("bus.Send(", body);
        Assert.DoesNotContain("ReadCemPin_Click", body);

        var language = ReadSource("src/Dvinholm.App/MainWindow.UnifiedUpdates.cs");
        Assert.Contains("RefreshCemIdentityEvidencePresentation();", language);
        Assert.Contains("RefreshCemPrerequisitePresentation();", language);
    }
}
