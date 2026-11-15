using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Source-level contracts for an opt-in real Windows desktop capture of all
/// nine manually selected reference vehicles. No screenshots are faked in CI.
/// </summary>
public sealed class AuroraVehicleLiveCaptureContracts
{
    private static string Source(string relative)
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName,
                relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path)) return File.ReadAllText(path);
        }
        throw new FileNotFoundException(relative);
    }

    private static readonly XNamespace X =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void VehicleCaptureIsOptInAndRequiresEcuInExplicitPageSet()
    {
        var script = Source("eng/Capture-UnifiedAuroraWinUI3.ps1");
        Assert.Contains("[switch]$CaptureVehicleModels", script);
        Assert.Contains("if ($CaptureVehicleModels -and 'Ecu' -notin $Pages)", script);
        Assert.Contains("if ($CaptureVehicleModels -and $page -eq 'Ecu')", script);
        Assert.Contains("Capture-OnlyVehicleModels $rect.Left $rect.Top", script);
        Assert.Contains("$Pages = @('CemPin','CemConfiguration','Ecu','Diagnostics','Updates','Connection')", script);
    }

    [Fact]
    public void NineCaptureOptionsAreExactlyTheNineCatalogTags()
    {
        var script = Source("eng/Capture-UnifiedAuroraWinUI3.ps1");
        var from = script.IndexOf("$vehicleModels = @(", StringComparison.Ordinal);
        var to = script.IndexOf("function Get-VehicleModelSelector", from, StringComparison.Ordinal);
        Assert.True(from >= 0 && to > from);
        var choices = script[from..to];
        foreach (var model in AuroraVehicleArtworkCatalog.Models)
            Assert.Contains("id='" + model.Id + "'", choices);
        Assert.Equal(9, choices.Split(" id='", StringSplitOptions.None).Length - 1);
        Assert.Contains("XC90 II · SPA", choices);
    }

    [Fact]
    public void ScreenshotTargetAndCaptionHaveStableUiaIds()
    {
        var xaml = XDocument.Parse(Source("src/Dvinholm.App/MainWindow.xaml"));
        var selector = xaml.Descendants().Single(e =>
            (string?)e.Attribute(X + "Name") == "EcuVehicleModel");
        var caption = xaml.Descendants().Single(e =>
            (string?)e.Attribute(X + "Name") == "EcuDiagramCaption");
        Assert.Equal("DvinholmVehicleModelSelector",
            (string?)selector.Attribute("AutomationProperties.AutomationId"));
        Assert.Equal("DvinholmVehicleArtworkCaption",
            (string?)caption.Attribute("AutomationProperties.AutomationId"));
    }

    [Fact]
    public void SelectionUsesUiaAndChecksTheActualDisplayCaption()
    {
        var script = Source("eng/Capture-UnifiedAuroraWinUI3.ps1");
        var from = script.IndexOf("function Select-OnlyVehicleArtwork(", StringComparison.Ordinal);
        var to = script.IndexOf("function Capture-OnlyVehicleModels(", from, StringComparison.Ordinal);
        Assert.True(from >= 0 && to > from);
        var probe = script[from..to];
        Assert.Contains("[System.Windows.Automation.ExpandCollapsePattern]::Pattern", probe);
        Assert.Contains("[System.Windows.Automation.SelectionItemPattern]::Pattern", probe);
        Assert.Contains("$select.Select()", probe);
        Assert.Contains("$expand.Collapse()", probe);
        Assert.Contains("Get-SelectedVehicleModelName", probe);
        Assert.Contains("DvinholmVehicleArtworkCaption", probe);
        Assert.Contains("$caption.Current.Name -cne $expected", probe);
        Assert.DoesNotContain("SendInput", probe);
        Assert.DoesNotContain("SendKeys", probe);
        Assert.DoesNotContain("MouseEvent", probe);
        Assert.DoesNotContain("InvokePattern", probe);
    }

    [Fact]
    public void CaptureChecksWindowDpiSizePositionAndForegroundBeforePixels()
    {
        var script = Source("eng/Capture-UnifiedAuroraWinUI3.ps1");
        var from = script.IndexOf("function Capture-OnlyVehicleModels(", StringComparison.Ordinal);
        var to = script.IndexOf("# Preflight the fixed viewport", from, StringComparison.Ordinal);
        Assert.True(from >= 0 && to > from);
        var probe = script[from..to];
        Assert.Contains("GetForegroundWindow()", probe);
        Assert.Contains("GetDpiForWindow($handle)", probe);
        Assert.Contains("GetWindowRect($handle", probe);
        Assert.Contains("GetClientRect($handle", probe);
        Assert.Contains("$outerNow.Left -ne $left", probe);
        Assert.Contains("$fixedWidthDip * $dpi / 96.0", probe);
        Assert.Contains("GetCursorPos([ref]$pointer)", probe);
        Assert.Contains("CopyFromScreen($left, $top", probe);
        Assert.Contains("Confirm-WorkspaceState 'Ecu' -RequireVisible", probe);
    }

    [Fact]
    public void OriginalManualModelSelectionRestoredEvenAfterFailure()
    {
        var script = Source("eng/Capture-UnifiedAuroraWinUI3.ps1");
        var from = script.IndexOf("function Capture-OnlyVehicleModels(", StringComparison.Ordinal);
        var to = script.IndexOf("# Preflight the fixed viewport", from, StringComparison.Ordinal);
        var probe = script[from..to];
        Assert.Contains("$originalName = Get-SelectedVehicleModelName", probe);
        Assert.Contains("finally {", probe);
        Assert.Contains("[void](Select-OnlyVehicleArtwork $original[0])", probe);
        Assert.True(probe.IndexOf("$bitmap.Save($destination", StringComparison.Ordinal) <
            probe.IndexOf("[void](Select-OnlyVehicleArtwork $original[0])", StringComparison.Ordinal));
    }

    [Fact]
    public void OnlyNineRealModelScreenshotsCanProduceSeparateManifest()
    {
        var script = Source("eng/Capture-UnifiedAuroraWinUI3.ps1");
        Assert.Contains("$script:vehicleResults += [ordered]@{", script);
        Assert.Contains("if ($vehicleResults.Count -ne $vehicleModels.Count)", script);
        Assert.Contains("Aurora-$Language-vehicle-models-manifest.json", script);
        Assert.Contains("Aurora-Ecu-Model-$($model.id)-$Language-$fixedWidthDip-dip.png", script);
        Assert.Contains("actual desktop pixels with verified UIA model, caption and WinUI image decode", script);
        Assert.Contains("human review", script);
        Assert.DoesNotContain("vehicle model visually verified by CI", script);
    }

    [Fact]
    public void VehicleVisualReviewNeverInitiatesCanOrVehicleIdentification()
    {
        var script = Source("eng/Capture-UnifiedAuroraWinUI3.ps1");
        var from = script.IndexOf("# A manual visual-artwork check.", StringComparison.Ordinal);
        var to = script.IndexOf("# Preflight the fixed viewport", from, StringComparison.Ordinal);
        Assert.True(from >= 0 && to > from);
        var section = script[from..to];
        foreach (var forbidden in new[] { "PassThru", "ReadDtc_Click(",
            "ReadCemPin(", "Connect_Click(", "SendKeys", "SendInput",
            "MouseEvent", "SetFocus(", "InvokePattern", "Invoke-WebRequest",
            "DtcClearButton" })
            Assert.DoesNotContain(forbidden, section);
        Assert.Contains("selectedModelNameVerifiedByUia", section);
        Assert.Contains("sourceCommitOperatorDeclared", section);
    }

    [Fact]
    public void ModelScreenshotsRequireActualWinUiImageDecodeBeforeSavingPixels()
    {
        var script = Source("eng/Capture-UnifiedAuroraWinUI3.ps1");
        var guard = script.IndexOf("function Assert-OnlyVehicleAtlasLoaded", StringComparison.Ordinal);
        var select = script.IndexOf("function Select-OnlyVehicleArtwork(", guard, StringComparison.Ordinal);
        var capture = script.IndexOf("function Capture-OnlyVehicleModels(", select, StringComparison.Ordinal);
        var bitmap = script.IndexOf("$bitmap = [System.Drawing.Bitmap]::new($width, $height)",
            capture, StringComparison.Ordinal);
        var pixelSave = script.IndexOf("$bitmap.Save($destination", bitmap, StringComparison.Ordinal);
        Assert.True(guard >= 0 && select > guard && capture > select &&
            bitmap > capture && pixelSave > bitmap);
        var verify = script[guard..select];
        Assert.Contains("DvinholmVehicleArtworkCaption", verify);
        Assert.Contains("$caption.Current.HelpText -cne 'DvinholmVehicleAtlasLoaded'", verify);
        Assert.Contains("throw 'Vehicle atlas SVG was not confirmed decoded by WinUI", verify);
        Assert.Contains("[void](Assert-OnlyVehicleAtlasLoaded)", script[select..capture]);
        var beforePixels = script[capture..bitmap];
        Assert.Contains("[void](Assert-OnlyVehicleAtlasLoaded)", beforePixels);
        Assert.Contains("atlasDecodedByWinUiImageOpened = $true", script);
        Assert.DoesNotContain("SendInput", verify);
        Assert.DoesNotContain("InvokePattern", verify);
    }

    [Fact]
    public void RealModelScreenshotsMustHaveNineDistinctArtboardPixelHashes()
    {
        var xaml = XDocument.Parse(Source("src/Dvinholm.App/MainWindow.xaml"));
        var artboard = xaml.Descendants().Single(e =>
            (string?)e.Attribute(X + "Name") == "EcuVehicleArtboard");
        Assert.Equal("DvinholmVehicleArtworkViewport",
            (string?)artboard.Attribute("AutomationProperties.AutomationId"));
        var script = Source("eng/Capture-UnifiedAuroraWinUI3.ps1");
        Assert.Contains("function Get-OnlyVehicleArtworkPixelHash(", script);
        Assert.Contains("$viewport.Current.BoundingRectangle", script);
        Assert.Contains("$bitmap.Clone(", script);
        Assert.Contains("SHA256]::Create()", script);
        Assert.Contains("$captureViewportHashes = @{}", script);
        Assert.Contains("$captureViewportHashes.ContainsKey($artHash)", script);
        Assert.Contains("artworkViewportSha256 = $artHash", script);
        Assert.Contains("atlas model switch not proven", script);
        var start = script.IndexOf("function Capture-OnlyVehicleModels(", StringComparison.Ordinal);
        var end = script.IndexOf("# Preflight the fixed viewport", start, StringComparison.Ordinal);
        var capture = script[start..end];
        Assert.True(capture.IndexOf("Get-OnlyVehicleArtworkPixelHash", StringComparison.Ordinal)
            < capture.IndexOf("$bitmap.Save($destination", StringComparison.Ordinal));
        Assert.DoesNotContain("SendKeys", capture);
        Assert.DoesNotContain("InvokePattern", capture);
    }

}
