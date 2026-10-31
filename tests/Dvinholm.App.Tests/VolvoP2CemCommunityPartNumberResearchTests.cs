namespace Dvinholm.App.Tests;

/// <summary>
/// Eight deterministic source-limited offline checks. Factual P/N mentions
/// are *community claims*, not physical XC90 compatibility or PIN services.
/// </summary>
public sealed class VolvoP2CemCommunityPartNumberResearchTests
{
    [Fact]
    public void HistoricalBrickP2NumberIsOnlyACommunityResearchMention()
    {
        var result = VolvoP2CemCommunityPartNumberResearch.Assess("8645716");
        Assert.Equal(VolvoP2CemCommunityPartNumberKind.ReportedBrickCem, result.Kind);
        Assert.Contains("P2 CEM-B", result.ReadOnlyExplanation);
        Assert.Contains("third-party research list only", result.ReadOnlyExplanation);
        AssertBlocked(result);
    }

    [Fact]
    public void SelectedLAndHNumbersUseDistinctCommunityCategories()
    {
        var l = VolvoP2CemCommunityPartNumberResearch.Assess("30786889");
        var h = VolvoP2CemCommunityPartNumberResearch.Assess("30786890");
        Assert.Equal(VolvoP2CemCommunityPartNumberKind.ReportedLCem, l.Kind);
        Assert.Equal(VolvoP2CemCommunityPartNumberKind.ReportedHCem, h.Kind);
        Assert.Contains("CEM-L", l.ReadOnlyExplanation);
        Assert.Contains("CEM-H", h.ReadOnlyExplanation);
        AssertBlocked(l);
        AssertBlocked(h);
    }

    [Fact]
    public void NumberFoundUnderBothLAndHRemainsAmbiguous()
    {
        var result = VolvoP2CemCommunityPartNumberResearch.Assess("30682982");
        Assert.Equal(VolvoP2CemCommunityPartNumberKind.ConflictingCommunityCategories,
            result.Kind);
        Assert.Contains("CATEGORY AMBIGUOUS", result.ReadOnlyExplanation);
        Assert.DoesNotContain("under P2 CEM-L", result.ReadOnlyExplanation);
        Assert.DoesNotContain("under P2 CEM-H", result.ReadOnlyExplanation);
        AssertBlocked(result);
    }

    [Fact]
    public void AbsentNumberIsNotMisreportedAsIncompatibleWithVolvo()
    {
        var result = VolvoP2CemCommunityPartNumberResearch.Assess("12345678");
        Assert.Equal(VolvoP2CemCommunityPartNumberKind.NotInSelectedResearchSample,
            result.Kind);
        Assert.Contains("NON-EXHAUSTIVE", result.ReadOnlyExplanation);
        Assert.Contains("not evidence of incompatibility", result.ReadOnlyExplanation);
        AssertBlocked(result);
    }

    [Fact]
    public void VinLikeSecretMalformedAndSpacePaddedInputsAreNotEchoed()
    {
        foreach (string? input in new[]
        {
            null, "", "30786889 ", " 30786889", "SW30786889",
            "123456789", "3078688", "YV1CM59H271234567",
            "PIN=1234", "https://sample.invalid", "30682\n982"
        })
        {
            var result = VolvoP2CemCommunityPartNumberResearch.Assess(input);
            Assert.Equal(
                VolvoP2CemCommunityPartNumberKind.InvalidOrMissingPartNumber, result.Kind);
            if (!string.IsNullOrEmpty(input))
                Assert.DoesNotContain(input, result.ReadOnlyExplanation);
            AssertBlocked(result);
        }
    }

    [Fact]
    public void EveryCatalogOutcomeDeniesLiveDiagnosticPinAndWriteOperations()
    {
        foreach (var input in new[]
        {
            "8645716", "8688434", "30682981", "30786889",
            "30728539", "30786890", "30682982", "12345678", "bad"
        })
        {
            var result = VolvoP2CemCommunityPartNumberResearch.Assess(input);
            AssertBlocked(result);
            var report = VolvoP2CemCommunityPartNumberResearch.BuildResearchNote(input);
            Assert.Contains("No speed, transceiver, CAN request or unlock command", report);
            Assert.DoesNotContain(input, report);
        }
    }

    [Fact]
    public void ValidManualVidaWorksheetDisplaysCrossReferenceWithoutVerification()
    {
        var report = VolvoP2CemVidaIdentityWorksheet.Assess(
            "2007", "30682982", "SW123", "DS456", true);
        Assert.Equal(VolvoP2CemVidaWorksheetKind.ManualClaimUnverified, report.Kind);
        Assert.Contains("Claimed hardware P/N: 30682982", report.Summary);
        Assert.Contains("CATEGORY AMBIGUOUS", report.Summary);
        Assert.Contains("not an XC90 model-year fitment table", report.Summary);
        Assert.Contains("UNVERIFIED USER-TRANSCRIBED CLAIM", report.Summary);
        Assert.False(report.IsVerifiedPhysicalCem);
        Assert.False(report.MayRequestPin);
        Assert.False(report.MayRequestLiveEcu);
        Assert.False(report.MayEditOrWriteConfiguration);
    }

    [Fact]
    public void ExportedResearchSchemaDoesNotGainExecutableOrHardwareProofFields()
    {
        var json = VolvoP2CemIdentityResearchExport.CreateJson(
            "2007", "30786889", "SW123", "DS456", userClaimsVidaSource: true);
        var record = VolvoP2CemIdentityResearchExport.InspectJson(json);
        Assert.True(record.IsUnverifiedManualClaim);
        Assert.False(record.MayAccessEcu);
        Assert.False(record.IsRestorableBackup);
        Assert.DoesNotContain("ReportedLCem", json);
        Assert.DoesNotContain("MayRequestPin", json);
        var description = VolvoP2CemIdentityResearchFileInspection.BuildReadOnlyReview(record);
        Assert.Contains("third-party research list only", description);
        Assert.Contains("NOT an authenticated VIDA/ECU reading", description);
        Assert.DoesNotContain("CAN_500KBPS", description);
    }

    private static void AssertBlocked(VolvoP2CemCommunityPartNumberAssessment result)
    {
        Assert.False(result.IsAuthenticatedPhysicalCem);
        Assert.False(result.HasVerifiedXc90Fitment);
        Assert.False(result.HasVerifiedProtocolOrCanAddress);
        Assert.False(result.MaySelectBusSpeed);
        Assert.False(result.MayIssueDiagnosticRequest);
        Assert.False(result.MayRequestPin);
        Assert.False(result.MayReadOrWriteConfiguration);
    }
}
