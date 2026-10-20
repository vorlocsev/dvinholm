namespace Dvinholm.App.Tests;

public sealed class UdsOfflineSampleTests
{
    [Fact]
    public void Format_LabelsEveryExampleAsSyntheticAndNotVehicleData()
    {
        var result = UdsOfflineSample.Format();
        Assert.StartsWith(UdsOfflineSample.Disclaimer, result);
        Assert.Contains("OFFLINE SYNTHETIC UDS SAMPLE", result);
        Assert.Contains("no adapter, ECU, or vehicle was queried", result);
        Assert.Contains("fictional", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Format_IsDeterministicAcrossCalls()
    {
        Assert.Equal(UdsOfflineSample.Format(), UdsOfflineSample.Format());
    }

    [Fact]
    public void Format_ShowsFictionalVinThroughExistingValidatedDidFormatter()
    {
        var result = UdsOfflineSample.Format();
        Assert.Contains("Synthetic VIN (DID F190)", result);
        Assert.Contains("DID 0xF190 (VIN): TEST0000000000ABC", result);
        Assert.DoesNotContain("VIN format invalid", result);
    }

    [Fact]
    public void Format_IncludesOriginalHexBytesOfFictionalVin()
    {
        var result = UdsOfflineSample.Format();
        Assert.Contains("raw: F1 90 54 45 53 54", result);
    }

    [Fact]
    public void Format_UsesExistingUnverifiedAsciiDidFormatter()
    {
        var result = UdsOfflineSample.Format();
        Assert.Contains("Synthetic generic DID F187", result);
        Assert.Contains("DID 0xF187: ASCII preview (unverified): \"DEMO-ECM\"", result);
        Assert.Contains("raw: F1 87 44 45 4D 4F 2D 45 43 4D", result);
    }

    [Fact]
    public void Format_ExplainsTwoSyntheticDtcRecordsAndRequestedMask()
    {
        var result = UdsOfflineSample.Format();
        Assert.Contains("2 DTC records returned (requested status mask 0xFF)", result);
        Assert.Contains("DTC 0x123456 | status 0x08: confirmed DTC", result);
        Assert.Contains("DTC 0x00C0DE | status 0x04: pending DTC", result);
    }

    [Fact]
    public void Format_DoesNotInventVehicleSpecificDiagnosticMeanings()
    {
        var result = UdsOfflineSample.Format();
        Assert.DoesNotContain("P1234", result);
        Assert.DoesNotContain("Volvo", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("replace sensor", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Format_DoesNotContainActiveDiagnosticInstructionsOrClaims()
    {
        var result = UdsOfflineSample.Format();
        Assert.DoesNotContain("Connected to", result);
        Assert.DoesNotContain("Read complete", result);
        Assert.DoesNotContain("fault-free", result, StringComparison.OrdinalIgnoreCase);
    }
}
