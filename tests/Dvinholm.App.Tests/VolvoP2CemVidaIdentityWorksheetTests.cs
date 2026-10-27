namespace Dvinholm.App.Tests;

/// <summary>
/// Eight synthetic, offline preflight checks. No real module identifiers,
/// VIN, PIN, serial number, J2534 interface or diagnostic access.
/// </summary>
public sealed class VolvoP2CemVidaIdentityWorksheetTests
{
    [Fact]
    public void RequiresThreePartNumbersAndOneModelYear()
    {
        foreach (var args in new[]
        {
            new string?[] { null, "HW123", "SW456", "DS789" },
            ["2007", "", "SW456", "DS789"],
            ["2007", "HW123", " ", "DS789"],
            ["2007", "HW123", "SW456", null]
        })
        {
            var result = Assess(args[0], args[1], args[2], args[3]);
            Assert.Equal(VolvoP2CemVidaWorksheetKind.MissingFields, result.Kind);
            Denied(result);
        }
    }

    [Fact]
    public void RejectsOutOfScopeMalformedOrSpacePaddedModelYears()
    {
        foreach (var year in new[] { "2001", "2015", "200", "02007", "2007 ",
            "2OO7", "-2007", "9999" })
        {
            var result = Assess(year, "HW123", "SW456", "DS789");
            Assert.Equal(VolvoP2CemVidaWorksheetKind.InvalidModelYear, result.Kind);
            Denied(result);
        }
    }

    [Fact]
    public void EarliestAndLatestFirstGenerationResearchYearsAreNotSupportProof()
    {
        foreach (var year in new[] { "2002", "2007", "2014" })
        {
            var result = Assess(year, "HW123", "SW456", "DS789", true);
            Assert.Equal(VolvoP2CemVidaWorksheetKind.ManualClaimUnverified, result.Kind);
            Assert.Contains("UNVERIFIED USER-TRANSCRIBED CLAIM", result.Summary);
            Denied(result);
        }
    }

    [Fact]
    public void RejectsControlCharactersWhitespaceAndOversizedPartNumberFields()
    {
        foreach (var invalid in new[]
        {
            "A", "XY", "NO_NUMBERS", "ABC 123", "ABC\n123", "ABC:123",
            "ABC\\123", "ABC=123", new string('A', 41) + "9"
        })
        {
            var result = Assess("2007", invalid, "SW456", "DS789");
            Assert.Equal(VolvoP2CemVidaWorksheetKind.InvalidPartNumber, result.Kind);
            Assert.DoesNotContain(invalid, result.Summary);
            Denied(result);
        }
    }

    [Fact]
    public void RefusesUninterruptedSeventeenCharacterVinLikeValues()
    {
        const string syntheticVinShape = "TESTVIN1234567890";
        Assert.Equal(17, syntheticVinShape.Length);
        foreach (var result in new[]
        {
            Assess("2007", syntheticVinShape, "SW456", "DS789"),
            Assess("2007", "HW123", syntheticVinShape, "DS789"),
            Assess("2007", "HW123", "SW456", syntheticVinShape)
        })
        {
            Assert.Equal(VolvoP2CemVidaWorksheetKind.InvalidPartNumber, result.Kind);
            Assert.DoesNotContain(syntheticVinShape, result.Summary);
            Denied(result);
        }
    }

    [Fact]
    public void ProducesNormalizedThreeFieldReportWithNoSerialPinOrVinInputs()
    {
        var result = Assess("2007", " hw-123 ", "sw.456", "diag/789", true);
        Assert.Equal(VolvoP2CemVidaWorksheetKind.ManualClaimUnverified, result.Kind);
        Assert.Contains("Claimed XC90 model year: 2007", result.Summary);
        Assert.Contains("Claimed hardware P/N: HW-123", result.Summary);
        Assert.Contains("Claimed software P/N: SW.456", result.Summary);
        Assert.Contains("Claimed diagnostic software P/N: DIAG/789", result.Summary);
        Assert.Contains("Hardware serial and VIN: intentionally not collected", result.Summary);
        Denied(result);
    }

    [Fact]
    public void ClaimedVidaSourceNeverConvertsSelfReportIntoVerification()
    {
        var fromVida = Assess("2007", "HW123", "SW456", "DS789", true);
        var other = Assess("2007", "HW123", "SW456", "DS789", false);
        Assert.Equal(VolvoP2CemVidaWorksheetKind.ManualClaimUnverified, fromVida.Kind);
        Assert.Equal(VolvoP2CemVidaWorksheetKind.ManualClaimUnverified, other.Kind);
        Assert.Contains("User says these fields were read in VIDA", fromVida.Summary);
        Assert.Contains("not independently verified", fromVida.Summary);
        Assert.Contains("Other / not independently documented", other.Summary);
        Denied(fromVida);
        Denied(other);
    }

    [Fact]
    public void NeverEnablesVehicleTrafficPinOrWriteRegardlessOfInput()
    {
        foreach (var report in new[]
        {
            Assess("2007", "HW123", "SW456", "DS789", true),
            Assess("2007", "HW123", "SW456", "DS789", false),
            Assess("2015", "HW123", "SW456", "DS789", true),
            Assess("2007", "HW123", "SECRET 12", "DS789", true)
        })
            Denied(report);
        Assert.Contains("No CAN/UDS/K-Line requests", Assess(
            "2007", "HW123", "SW456", "DS789").Summary);
    }

    private static VolvoP2CemVidaWorksheetAssessment Assess(
        string? year, string? hardware, string? software, string? diagnostics,
        bool fromVida = false) =>
        VolvoP2CemVidaIdentityWorksheet.Assess(
            year, hardware, software, diagnostics, fromVida);

    private static void Denied(VolvoP2CemVidaWorksheetAssessment result)
    {
        Assert.False(result.IsVerifiedPhysicalCem);
        Assert.False(result.HasVerifiedCanOrKLineTransport);
        Assert.False(result.HasVerifiedConfigurationMapping);
        Assert.False(result.MayRequestLiveEcu);
        Assert.False(result.MayRequestPin);
        Assert.False(result.MayEditOrWriteConfiguration);
    }
}
