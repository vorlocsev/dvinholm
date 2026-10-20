namespace Dvinholm.App.Tests;

public sealed class UdsOfflineTextReportTests
{
    [Fact]
    public void Build_StartsWithAnExplicitSyntheticTextReportHeader()
    {
        var report = UdsOfflineTextReport.Build();
        Assert.StartsWith("Dvinholm — OFFLINE SYNTHETIC UDS TXT REPORT", report);
    }

    [Fact]
    public void Build_StatesNoDeviceEcuOrVehicleWasQueried()
    {
        var report = UdsOfflineTextReport.Build();
        Assert.Contains("No J2534 adapter, ECU or vehicle was accessed", report);
        Assert.Contains("This is not a diagnostic report", report);
    }

    [Fact]
    public void Build_PreservesTheFullExistingPreviewWithItsProvenance()
    {
        var report = UdsOfflineTextReport.Build();
        Assert.Contains(UdsOfflineSample.Format(), report);
        Assert.Contains(UdsOfflineSample.Disclaimer, report);
    }

    [Fact]
    public void Build_IsDeterministicAndContainsNoTimestamps()
    {
        var first = UdsOfflineTextReport.Build();
        Assert.Equal(first, UdsOfflineTextReport.Build());
        Assert.DoesNotContain("2026-", first);
    }

    [Fact]
    public void SuggestedNameMarksSavedFileAsSynthetic()
    {
        Assert.Equal("uds-SYNTHETIC-example", UdsOfflineTextReport.SuggestedFileName);
        Assert.DoesNotContain('/', UdsOfflineTextReport.SuggestedFileName);
        Assert.DoesNotContain('\\', UdsOfflineTextReport.SuggestedFileName);
    }

    [Fact]
    public void Build_IncludesOnlyFictionalVinAndItsOriginalHexBytes()
    {
        var report = UdsOfflineTextReport.Build();
        Assert.Contains("DID 0xF190 (VIN): TEST0000000000ABC", report);
        Assert.Contains("raw: F1 90 54 45 53 54", report);
        Assert.DoesNotContain("VIN format invalid", report);
    }

    [Fact]
    public void Build_PreservesUnverifiedDidAsciiAndHexPayload()
    {
        var report = UdsOfflineTextReport.Build();
        Assert.Contains("ASCII preview (unverified): \"DEMO-ECM\"", report);
        Assert.Contains("raw: F1 87 44 45 4D 4F 2D 45 43 4D", report);
    }

    [Fact]
    public void Build_PreservesTwoFictionalDtcStatusesWithoutInventedDiagnosis()
    {
        var report = UdsOfflineTextReport.Build();
        Assert.Contains("2 DTC records returned (requested status mask 0xFF)", report);
        Assert.Contains("DTC 0x123456 | status 0x08: confirmed DTC", report);
        Assert.Contains("DTC 0x00C0DE | status 0x04: pending DTC", report);
        Assert.DoesNotContain("replace sensor", report, StringComparison.OrdinalIgnoreCase);
    }
}
