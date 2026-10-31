namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline tests of two user-authored CEM version claims. No real
/// hardware proof, CAN traffic, security access or configuration changes.
/// </summary>
public sealed class VolvoP2CemRevisionEvidenceTriageTests
{
    [Fact]
    public void OutOfYearScopeDoesNotTreatMatchingTextAsEvidence()
    {
        var card = Card();
        var source = Claim() with { ModelYearFrom = 2008, ModelYearThrough = 2009 };
        var r = VolvoP2CemRevisionEvidenceTriage.Assess(card, source);
        Assert.Equal(VolvoP2CemRevisionEvidenceTriageKind.OutsideClaimedYearScope, r.Kind);
        Assert.False(r.HardwareTextMatches);
        Assert.False(r.ApplicationSoftwareTextMatches);
        Assert.False(r.DiagnosticSoftwareTextMatches);
        Assert.Contains("outside", VolvoP2CemRevisionEvidenceTriage.BuildReadOnlyReport(card, source));
        Denied(r);
    }

    [Fact]
    public void DifferentHardwareTakesPrecedenceOverSameSoftwareAndCategory()
    {
        var card = Card();
        var source = Claim() with { ClaimedHardwarePartNumber = "30728539" };
        var r = VolvoP2CemRevisionEvidenceTriage.Assess(card, source);
        Assert.Equal(VolvoP2CemRevisionEvidenceTriageKind.DifferentHardwareClaims, r.Kind);
        Assert.False(r.HardwareTextMatches);
        Assert.True(r.ApplicationSoftwareTextMatches);
        Assert.True(r.DiagnosticSoftwareTextMatches);
        Assert.Contains("Cannot infer the same physical CEM",
            VolvoP2CemRevisionEvidenceTriage.BuildReadOnlyReport(card, source));
        Denied(r);
    }

    [Fact]
    public void SameHardwareButDifferentApplicationSoftwareNeverPromotesRevision()
    {
        var source = Claim() with { ClaimedSoftwarePartNumber = "SW-002" };
        var r = VolvoP2CemRevisionEvidenceTriage.Assess(Card(), source);
        Assert.Equal(
            VolvoP2CemRevisionEvidenceTriageKind.DifferentApplicationSoftwareClaims,
            r.Kind);
        Assert.True(r.HardwareTextMatches);
        Assert.False(r.ApplicationSoftwareTextMatches);
        Assert.True(r.DiagnosticSoftwareTextMatches);
        Assert.Contains("Revision remains unresolved",
            VolvoP2CemRevisionEvidenceTriage.BuildReadOnlyReport(Card(), source));
        Denied(r);
    }

    [Fact]
    public void DiagnosticSoftwareMismatchIsSeparateFromApplicationSoftware()
    {
        var source = Claim() with { ClaimedDiagnosticSoftwarePartNumber = "DIAG-002" };
        var r = VolvoP2CemRevisionEvidenceTriage.Assess(Card(), source);
        Assert.Equal(
            VolvoP2CemRevisionEvidenceTriageKind.DifferentDiagnosticSoftwareClaims,
            r.Kind);
        Assert.True(r.HardwareTextMatches);
        Assert.True(r.ApplicationSoftwareTextMatches);
        Assert.False(r.DiagnosticSoftwareTextMatches);
        Denied(r);
    }

    [Fact]
    public void AllThreeClaimsMatchingWithOneCommunityMentionRemainUnverified()
    {
        var r = VolvoP2CemRevisionEvidenceTriage.Assess(Card(), Claim());
        Assert.Equal(
            VolvoP2CemRevisionEvidenceTriageKind.SameTextWithSingleCommunityMention,
            r.Kind);
        Assert.True(r.HardwareTextMatches);
        Assert.True(r.ApplicationSoftwareTextMatches);
        Assert.True(r.DiagnosticSoftwareTextMatches);
        Assert.Contains("one selected community category mention ONLY",
            VolvoP2CemRevisionEvidenceTriage.BuildReadOnlyReport(Card(), Claim()));
        Denied(r);
    }

    [Fact]
    public void DuplicatedCommunityPartNumberCannotSelectLOrHCategory()
    {
        var card = Card() with { ClaimedHardwarePartNumber = "30682982" };
        var claim = Claim() with { ClaimedHardwarePartNumber = "30682982" };
        var r = VolvoP2CemRevisionEvidenceTriage.Assess(card, claim);
        Assert.Equal(
            VolvoP2CemRevisionEvidenceTriageKind.SameTextWithAmbiguousCommunityCategory,
            r.Kind);
        var report = VolvoP2CemRevisionEvidenceTriage.BuildReadOnlyReport(card, claim);
        Assert.Contains("occurs in multiple CEM categories", report);
        Assert.DoesNotContain("CEM-L supported", report);
        Assert.DoesNotContain("CEM-H supported", report);
        Denied(r);
    }

    [Fact]
    public void UnlistedNumberDoesNotImplyIncompatiblePhysicalVehicle()
    {
        var card = Card() with { ClaimedHardwarePartNumber = "12345678" };
        var claim = Claim() with { ClaimedHardwarePartNumber = "12345678" };
        var r = VolvoP2CemRevisionEvidenceTriage.Assess(card, claim);
        Assert.Equal(
            VolvoP2CemRevisionEvidenceTriageKind.SameTextWithNoSelectedCommunityMention,
            r.Kind);
        var report = VolvoP2CemRevisionEvidenceTriage.BuildReadOnlyReport(card, claim);
        Assert.Contains("selected non-exhaustive community sample", report);
        Assert.DoesNotContain("incompatible", report);
        Denied(r);
    }

    [Fact]
    public void ForgedPermissionsRejectedAndOriginalPreviewRedactsPrivatePartNumbers()
    {
        Assert.Throws<InvalidDataException>(() =>
            VolvoP2CemRevisionEvidenceTriage.Assess(
                Card() with { MayAccessEcu = true }, Claim()));
        Assert.Throws<InvalidDataException>(() =>
            VolvoP2CemRevisionEvidenceTriage.Assess(
                Card(), Claim() with { IsRestorableBackup = true }));

        var source = Claim() with
        {
            SourceReference = "CONFIDENTIAL-source-not-to-be-shared"
        };
        var report = VolvoP2CemRevisionSourceComparison.BuildReadOnlyReport(
            Card(), source);
        Assert.Contains("CEM HARDWARE / APPLICATION SW / DIAGNOSTIC SW TRIAGE", report);
        Assert.Contains("SameTextWithSingleCommunityMention", report);
        Assert.Contains("all live CEM capabilities DISABLED", report);
        Assert.DoesNotContain(Card().ClaimedHardwarePartNumber, report);
        Assert.DoesNotContain(Card().ClaimedSoftwarePartNumber, report);
        Assert.DoesNotContain(Card().ClaimedDiagnosticSoftwarePartNumber, report);
        Assert.DoesNotContain(source.SourceReference, report);
        Assert.DoesNotContain("VIN12345678901234", report);
        Assert.Contains("No profile, file, adapter or vehicle changes", report);
    }

    private static VolvoP2CemIdentityResearchDocument Card() =>
        new(VolvoP2CemIdentityResearchExport.Format, 1,
            IsUnverifiedManualClaim: true, IsRestorableBackup: false,
            MayAccessEcu: false, ClaimedModelYear: 2007,
            ClaimedHardwarePartNumber: "30786889",
            ClaimedSoftwarePartNumber: "SW-001",
            ClaimedDiagnosticSoftwarePartNumber: "DIAG-001",
            UserClaimsVidaSource: true);

    private static VolvoP2CemRevisionSourceClaim Claim() =>
        new(VolvoP2CemRevisionSourceComparison.Format, 1,
            IsUnverifiedResearchClaim: true, IsRestorableBackup: false,
            MayAccessEcu: false, ModelYearFrom: 2007, ModelYearThrough: 2009,
            ClaimedHardwarePartNumber: "30786889",
            ClaimedSoftwarePartNumber: "SW-001",
            ClaimedDiagnosticSoftwarePartNumber: "DIAG-001",
            SourceReference: "Synthetic independent-source research label");

    private static void Denied(VolvoP2CemRevisionEvidenceTriageAssessment r)
    {
        Assert.False(r.HasAuthenticatedPhysicalCem);
        Assert.False(r.HasIndependentSourceVerification);
        Assert.False(r.HasVerifiedHardwareFirmwareCompatibility);
        Assert.False(r.HasVerifiedTransport);
        Assert.False(r.MayReadConfiguration);
        Assert.False(r.MayRequestPin);
        Assert.False(r.MayWriteConfiguration);
    }
}
