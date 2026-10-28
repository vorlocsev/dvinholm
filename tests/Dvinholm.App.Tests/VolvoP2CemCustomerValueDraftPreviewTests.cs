namespace Dvinholm.App.Tests;

/// <summary>
/// Eight deterministic offline preview checks. No WinUI dispatcher, ECU
/// connection, PIN, profile, file IO or CEM programming is involved.
/// </summary>
public sealed class VolvoP2CemCustomerValueDraftPreviewTests
{
    private static string Review(
        string? year, bool claimedYearChecked,
        VolvoP2CemCustomerParameterKind kind,
        string? value, string? high = null) =>
        VolvoP2CemCustomerValueDraftPreview.Build(
            year, claimedYearChecked, kind, value, high);

    [Fact]
    public void CanonicalSeatLowHighOnlyCreatesEphemeralResearchPreview()
    {
        var report = Review("2005", true,
            VolvoP2CemCustomerParameterKind.SeatHeaterLowHighCelsius, "37", "41");
        Assert.Contains("ListedInHistoricalSourceOnly", report);
        Assert.Contains("37 LOW; 41 HIGH", report);
        Assert.Contains("Current vehicle value: UNKNOWN", report);
        Assert.Contains("Closing this dialog discards", report);
        Assert.Contains("No CAN/J2534/UDS", report);
        Assert.DoesNotContain("Write to ECU", report);
    }

    [Fact]
    public void InvalidSeatRelationshipDoesNotEchoProposedValues()
    {
        var report = Review("2005", true,
            VolvoP2CemCustomerParameterKind.SeatHeaterLowHighCelsius, "41", "37");
        Assert.Contains("NotListedOrInvalidRelationship", report);
        Assert.DoesNotContain("41 LOW", report);
        Assert.DoesNotContain("37 HIGH", report);
        Assert.Contains("NOT approved or printed", report);
    }

    [Fact]
    public void UncheckedYearNeverCreatesValidatedResearchValue()
    {
        var report = Review("2005", false,
            VolvoP2CemCustomerParameterKind.ApproachLightsSeconds, "30");
        Assert.Contains("VehicleYearUnconfirmed", report);
        Assert.DoesNotContain("Proposed research notation: 30", report);
        Assert.Contains("self-reported, NOT authenticated", report);
    }

    [Fact]
    public void InvalidOrOutOfScopeYearNeverExtrapolatesEvidence()
    {
        foreach (var year in new[] { "", "abcd", "02005", "2007", "2014" })
        {
            var report = Review(year, true,
                VolvoP2CemCustomerParameterKind.ApproachLightsSeconds, "60");
            Assert.DoesNotContain("ListedInHistoricalSourceOnly", report);
            Assert.DoesNotContain("Proposed research notation: 60", report);
        }
    }

    [Fact]
    public void ApproachLightCandidatesAreHistoricalProposalsOnly()
    {
        foreach (var value in new[] { "0", "30", "60", "90" })
        {
            var report = Review("2006", true,
                VolvoP2CemCustomerParameterKind.ApproachLightsSeconds, value);
            Assert.Contains("Proposed research notation: " + value, report);
            Assert.Contains("No diagnostic connection", report);
        }
        var bad = Review("2006", true,
            VolvoP2CemCustomerParameterKind.ApproachLightsSeconds, "120");
        Assert.DoesNotContain("Proposed research notation: 120", bad);
    }

    [Fact]
    public void PostDryingAndRunningLampCandidatesRemainSourceScoped()
    {
        var switchReport = Review("2004", true,
            VolvoP2CemCustomerParameterKind.PostDrying, "ON");
        Assert.Contains("Proposed research notation: ON", switchReport);

        var lampsReport = Review("2004", true,
            VolvoP2CemCustomerParameterKind.DaytimeRunningLamps, "FLEX_LOW");
        Assert.Contains("country-specific legal review required", lampsReport);
        Assert.Contains("Proposed research notation: FLEX_LOW", lampsReport);
        Assert.Contains("vehicle configuration file and Volvo software products", lampsReport,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnknownKindAndMissingOrExtraValuesFailWithoutEcho()
    {
        foreach (var report in new[]
        {
            Review("2005", true, (VolvoP2CemCustomerParameterKind)99, "SECRET123"),
            Review("2005", true, VolvoP2CemCustomerParameterKind.PostDrying, ""),
            Review("2005", true, VolvoP2CemCustomerParameterKind.ApproachLightsSeconds, "30", "41"),
            Review("2005", true, VolvoP2CemCustomerParameterKind.SeatHeaterLowHighCelsius, "37")
        })
        {
            Assert.DoesNotContain("Proposed research notation:", report);
            Assert.Contains("Current vehicle value: UNKNOWN", report);
        }
    }

    [Fact]
    public void AccidentalSensitiveInputNeverAppearsInPreview()
    {
        foreach (var input in new[]
        {
            "SECRET_PIN_1234", "VERY_PRIVATE_PASSWORD", "YV1CM592771234567"
        })
        {
            var report = Review("2005", true,
                VolvoP2CemCustomerParameterKind.DaytimeRunningLamps, input);
            Assert.DoesNotContain(input, report);
            Assert.Contains("No CAN/J2534/UDS, PIN/security access", report);
            Assert.Contains("No file, profile or setting is changed", report);
        }
    }
}
