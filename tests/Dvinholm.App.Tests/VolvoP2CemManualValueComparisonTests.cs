namespace Dvinholm.App.Tests;

/// <summary>
/// Eight deterministic in-memory tests, no WinUI window, J2534, physical
/// CEM, VIN, PIN, identity enrollment, backups or write instructions.
/// </summary>
public sealed class VolvoP2CemManualValueComparisonTests
{
    private static VolvoP2CemManualValueComparisonAssessment Check(
        string? year, bool yearChecked,
        VolvoP2CemCustomerParameterKind kind,
        string? reference, string? referenceHigh,
        string? proposal, string? proposalHigh) =>
        VolvoP2CemManualValueComparison.Assess(
            year, yearChecked, kind, reference, referenceHigh, proposal, proposalHigh);

    private static string Report(
        string? year, bool yearChecked,
        VolvoP2CemCustomerParameterKind kind,
        string? reference, string? referenceHigh,
        string? proposal, string? proposalHigh) =>
        VolvoP2CemManualValueComparison.BuildReadOnlyReport(
            year, yearChecked, kind, reference, referenceHigh, proposal, proposalHigh);

    [Fact]
    public void IdenticalApproachLightNotesAreNotAProvenEcuBaseline()
    {
        var text = Report("2005", true,
            VolvoP2CemCustomerParameterKind.ApproachLightsSeconds,
            "30", null, "30", null);
        Assert.Contains("SameManualNotation", text);
        Assert.Contains("Manual reference notation (NOT current ECU): 30", text);
        Assert.Contains("Proposed research notation: 30", text);
        Assert.Contains("SAME (two manual claims", text);
        Assert.Contains("Actual CURRENT CEM value: UNKNOWN", text);
    }

    [Fact]
    public void DifferentApproachLightNotesAreNotARealConfigurationChange()
    {
        var text = Report("2005", true,
            VolvoP2CemCustomerParameterKind.ApproachLightsSeconds,
            "30", null, "60", null);
        Assert.Contains("DifferentManualNotation", text);
        Assert.Contains("DIFFERENT (not evidence a vehicle setting changed)", text);
        Assert.DoesNotContain("ECU configured", text);
    }

    [Fact]
    public void SeatLowHighPairComparisonChecksBothLevels()
    {
        var equal = Check("2003", true,
            VolvoP2CemCustomerParameterKind.SeatHeaterLowHighCelsius,
            "37", "41", "37", "41");
        var diff = Report("2003", true,
            VolvoP2CemCustomerParameterKind.SeatHeaterLowHighCelsius,
            "37", "41", "37", "43");
        Assert.Equal(VolvoP2CemManualValueComparisonKind.SameManualNotation,
            equal.Kind);
        Assert.Contains("DifferentManualNotation", diff);
        Assert.Contains("37 LOW; 41 HIGH", diff);
        Assert.Contains("37 LOW; 43 HIGH", diff);
    }

    [Fact]
    public void MissingOrInvalidManualReferenceFailsClosed()
    {
        foreach (var reference in new string?[] { null, "", "45", "001", "SECRET_PIN_1234" })
        {
            var text = Report("2005", true,
                VolvoP2CemCustomerParameterKind.ApproachLightsSeconds,
                reference, null, "60", null);
            Assert.DoesNotContain("Proposed research notation:", text);
            Assert.DoesNotContain("Manual reference notation (NOT current ECU):", text);
            Assert.DoesNotContain("SECRET_PIN_1234", text);
        }
        Assert.Equal(VolvoP2CemManualValueComparisonKind.ReferenceMissing,
            Check("2005", true, VolvoP2CemCustomerParameterKind.ApproachLightsSeconds,
                null, null, "30", null).Kind);
    }

    [Fact]
    public void InvalidProposalNeverLeaksEvenValidReference()
    {
        foreach (var unsafeValue in new[] { "VERY_PRIVATE_PASSWORD",
                     "YV1CM592771234567", "45", "flex_low", "FLEX_NOTREAL" })
        {
            var report = Report("2005", true,
                VolvoP2CemCustomerParameterKind.DaytimeRunningLamps,
                "FLEX_LOW", null, unsafeValue, null);
            Assert.Contains("ProposalInvalidOrUnconfirmed", report);
            Assert.DoesNotContain(unsafeValue, report);
            Assert.DoesNotContain("Manual reference notation (NOT current ECU):", report);
        }
    }

    [Fact]
    public void UncheckedOrUnlistedYearsNeverCompareManualClaims()
    {
        foreach (var year in new string?[] { null, "", "2002", "2007", "02005", "abcd" })
        {
            var report = Report(year, true,
                VolvoP2CemCustomerParameterKind.PostDrying,
                "OFF", null, "ON", null);
            Assert.Equal(false, report.Contains("Proposed research notation:", StringComparison.Ordinal));
        }
        Assert.Equal(VolvoP2CemManualValueComparisonKind.ReferenceInvalidOrUnconfirmed,
            Check("2005", false, VolvoP2CemCustomerParameterKind.PostDrying,
                "OFF", null, "ON", null).Kind);
    }

    [Fact]
    public void InvalidSeatRelationshipExtraValueAndUnknownParameterFailClosed()
    {
        foreach (var sample in new[]
        {
            Check("2006", true, VolvoP2CemCustomerParameterKind.SeatHeaterLowHighCelsius,
                "41", "37", "37", "41"),
            Check("2006", true, VolvoP2CemCustomerParameterKind.ApproachLightsSeconds,
                "30", "41", "60", null),
            Check("2006", true, (VolvoP2CemCustomerParameterKind)999,
                "30", null, "60", null)
        })
            Assert.Equal(VolvoP2CemManualValueComparisonKind.ReferenceInvalidOrUnconfirmed,
                sample.Kind);
        Assert.Equal(VolvoP2CemManualValueComparisonKind.ProposalInvalidOrUnconfirmed,
            Check("2006", true, VolvoP2CemCustomerParameterKind.SeatHeaterLowHighCelsius,
                "37", "41", "41", "37").Kind);
    }

    [Fact]
    public void EveryResultPermanentlyDeniesAllLiveCemAndPinCapabilities()
    {
        var cases = new[]
        {
            Check("2005", true, VolvoP2CemCustomerParameterKind.PostDrying,
                "ON", null, "OFF", null),
            Check("2005", true, VolvoP2CemCustomerParameterKind.PostDrying,
                "ON", null, "ON", null),
            Check("2005", false, VolvoP2CemCustomerParameterKind.PostDrying,
                "ON", null, "OFF", null),
            Check("2005", true, VolvoP2CemCustomerParameterKind.PostDrying,
                null, null, "OFF", null)
        };
        foreach (var result in cases)
        {
            Assert.False(result.HasAuthenticatedReferenceFromEcu);
            Assert.False(result.HasVerifiedFirmwareValueMapping);
            Assert.False(result.MayConnectOrReadCem);
            Assert.False(result.MayPrepareOrWriteConfiguration);
            Assert.False(result.MayRequestPinOrSecurityAccess);
        }
        var text = Report("2005", true,
            VolvoP2CemCustomerParameterKind.PostDrying,
            "ON", null, "OFF", null);
        Assert.Contains("Nothing saved to disk or ECU", text);
        Assert.Contains("No vehicle-config backup, write plan, J2534/UDS", text);
    }
}
