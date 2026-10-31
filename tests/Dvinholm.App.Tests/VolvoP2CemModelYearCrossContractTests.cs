using System.Text.Json;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight deterministic offline gates preventing MY2002 manual claims from
/// passing as documented MY2003 XC90 P2 evidence. No ECU or J2534 calls.
/// </summary>
public sealed class VolvoP2CemModelYearCrossContractTests
{
    private static VolvoP2CemVidaWorksheetAssessment Worksheet(string year) =>
        VolvoP2CemVidaIdentityWorksheet.Assess(
            year, "HW123", "SW456", "DS789", manuallyTranscribedFromVida: true);

    private static VolvoP2CemIdentityResearchDocument ManualCard(string year) =>
        VolvoP2CemIdentityResearchExport.InspectJson(
            VolvoP2CemIdentityResearchExport.CreateJson(
                year, "HW123", "SW456", "DS789", true));

    private static VolvoP2CemRevisionSourceClaim Source(int from, int through) =>
        new(VolvoP2CemRevisionSourceComparison.Format, 1,
            IsUnverifiedResearchClaim: true, IsRestorableBackup: false,
            MayAccessEcu: false, ModelYearFrom: from, ModelYearThrough: through,
            ClaimedHardwarePartNumber: "HW123",
            ClaimedSoftwarePartNumber: "SW456",
            ClaimedDiagnosticSoftwarePartNumber: "DS789",
            SourceReference: "https://example.invalid/xp2-manual");

    [Fact]
    public void WorksheetSharesThePublishedHsCanFirstAndLastModelYear()
    {
        Assert.Equal(VolvoP2CemHsCanSpeedAdvisory.FirstDocumentedXc90ModelYear,
            VolvoP2CemVidaIdentityWorksheet.FirstGenerationXc90EarliestModelYear);
        Assert.Equal(VolvoP2CemHsCanSpeedAdvisory.LastFirstGenerationXc90ModelYear,
            VolvoP2CemVidaIdentityWorksheet.FirstGenerationXc90LastModelYear);
        Assert.Equal(2003, VolvoP2CemVidaIdentityWorksheet.FirstGenerationXc90EarliestModelYear);
    }

    [Fact]
    public void ManualYear2002IsNotAValidXc90ModelYearClaim()
    {
        var review = Worksheet("2002");
        Assert.Equal(VolvoP2CemVidaWorksheetKind.InvalidModelYear, review.Kind);
        Assert.DoesNotContain("HW123", review.Summary);
        Assert.False(review.IsVerifiedPhysicalCem);
        Assert.False(review.MayRequestLiveEcu);
        Assert.False(review.MayRequestPin);
        Assert.False(review.MayEditOrWriteConfiguration);
    }

    [Fact]
    public void Boundary2003And2014RetainUnverifiedManualOnlyStatus()
    {
        foreach (string year in new[] { "2003", "2014" })
        {
            var review = Worksheet(year);
            Assert.Equal(VolvoP2CemVidaWorksheetKind.ManualClaimUnverified, review.Kind);
            Assert.Contains("UNVERIFIED USER-TRANSCRIBED CLAIM", review.Summary);
            Assert.False(review.IsVerifiedPhysicalCem);
            Assert.False(review.MayRequestLiveEcu);
            Assert.False(review.MayRequestPin);
        }
        Assert.Equal(VolvoP2CemVidaWorksheetKind.InvalidModelYear, Worksheet("2015").Kind);
    }

    [Fact]
    public void ManualResearchJsonExporterRejects2002InsteadOfSavingAnInvalidClaim()
    {
        Assert.Throws<InvalidDataException>(() =>
            VolvoP2CemIdentityResearchExport.CreateJson(
                "2002", "HW123", "SW456", "DS789", true));
        var json = VolvoP2CemIdentityResearchExport.CreateJson(
            "2003", "HW123", "SW456", "DS789", false);
        Assert.Equal(2003,
            VolvoP2CemIdentityResearchExport.InspectJson(json).ClaimedModelYear);
    }

    [Fact]
    public void PreviouslyExportedButEditedMy2002CardIsRejectedOnImport()
    {
        var valid = VolvoP2CemIdentityResearchExport.CreateJson(
            "2003", "HW123", "SW456", "DS789", true);
        const string oldField = "\"claimedModelYear\":2003";
        Assert.Contains(oldField, valid);
        var forged = valid.Replace(oldField, "\"claimedModelYear\":2002",
            StringComparison.Ordinal);
        Assert.Throws<InvalidDataException>(() =>
            VolvoP2CemIdentityResearchExport.InspectJson(forged));
    }

    [Fact]
    public void OutOfScope2002RevisionSourceCannotBeCrossMatchedWith2003Card()
    {
        var manual = ManualCard("2003");
        Assert.Throws<InvalidDataException>(() =>
            VolvoP2CemRevisionSourceComparison.Compare(manual, Source(2002, 2003)));
        Assert.Throws<InvalidDataException>(() =>
            VolvoP2CemRevisionSourceComparison.Compare(manual, Source(2002, 2014)));
    }

    [Fact]
    public void SerializedRevisionSourceClaimCannotSmuggleMy2002IntoImporter()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        var json = JsonSerializer.Serialize(Source(2002, 2003), options);
        Assert.Throws<InvalidDataException>(() =>
            VolvoP2CemRevisionSourceComparison.InspectJson(json));
        var valid = JsonSerializer.Serialize(Source(2003, 2014), options);
        Assert.Equal(2003,
            VolvoP2CemRevisionSourceComparison.InspectJson(valid).ModelYearFrom);
    }

    [Fact]
    public void Consistent2003ClaimsStillCannotUnlockCemOrRequestPin()
    {
        var match = VolvoP2CemRevisionSourceComparison.Compare(
            ManualCard("2003"), Source(2003, 2003));
        Assert.Equal(
            VolvoP2CemRevisionSourceMatchKind.SameUnverifiedPartNumberClaims,
            match.Kind);
        Assert.False(match.HasAuthenticatedCemIdentity);
        Assert.False(match.HasVerifiedDiagnosticAddressing);
        Assert.False(match.MayConnectOrReadCem);
        Assert.False(match.MayAcquirePin);
        Assert.False(match.MayWriteConfiguration);
        var readiness = VolvoP2CemReadinessMatrix.Assess(
            2003, true, VolvoP2NetworkKind.HighSpeedCan, 250_000,
            VolvoP2CemDiagnosticInterface.DiCE);
        Assert.Equal(VolvoP2CemReadinessStatus.PublishedReferenceOnly,
            readiness.Rows.Single(row =>
                row.Dimension == VolvoP2CemReadinessDimension.Xc90P2ModelYear).Status);
        Assert.False(readiness.CanConnectToCem);
        Assert.False(readiness.CanReadConfiguration);
        Assert.False(readiness.CanAcquireOrAuthorizePin);
    }
}
