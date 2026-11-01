namespace Dvinholm.App.Tests;

/// <summary>
/// Eight synthetic, offline wording/provenance regressions. A match in an
/// editable research catalog cannot authenticate an XC90 P2 physical CEM.
/// </summary>
public sealed class VolvoP2CemResearchMatchWordingTests
{
    [Fact]
    public void ProductionEmptyCatalogNeverClaimsVerifiedPhysicalCem()
    {
        Assert.Empty(VolvoP2CemSupportMatrix.ProductionEntries);
        var result = Assess([]);
        Assert.Equal(VolvoP2CemSupportKind.Unknown, result.Kind);
        Assert.Contains("research fingerprint", result.Explanation);
        AssertUnverifiedAndDenied(result);
    }

    [Fact]
    public void SyntheticIdentityOnlyMatchIsDescribedAsResearchNotAuthentication()
    {
        var result = Assess([Entry() with { DocumentedReadableDids = [] }]);
        Assert.Equal(VolvoP2CemSupportKind.VerifiedIdentityReadOnly, result.Kind);
        Assert.Empty(result.ObservedDocumentedDids);
        Assert.Contains("research metadata only", result.Explanation);
        Assert.Contains("no separately documented DID observed", result.Explanation);
        Assert.DoesNotContain("Verified read-only ECU identity", result.Explanation);
        AssertUnverifiedAndDenied(result);
    }

    [Fact]
    public void ObservedSyntheticDidsAreNotClaimedAsDecodedConfiguration()
    {
        var result = Assess([Entry()]);
        Assert.Equal(VolvoP2CemSupportKind.DocumentedReadOnlyDidSet, result.Kind);
        Assert.Equal(new ushort[] { 0xF187, 0xF189 }, result.ObservedDocumentedDids);
        Assert.Contains("previously observed, separately listed DIDs", result.Explanation);
        Assert.Contains("not decoded configuration or a restorable backup", result.Explanation);
        Assert.DoesNotContain("Verified, sourced", result.Explanation);
        AssertUnverifiedAndDenied(result);
    }

    [Fact]
    public void ClaimedVerificationAndPrivateEvidenceSourcesDoNotLeakOrAuthenticate()
    {
        const string sensitive = "PRIVATE-SYNTHETIC-IDENTITY-SOURCE";
        var entry = Entry() with
        {
            Identity = Entry().Identity with { EvidenceSource = sensitive },
            ReadDidEvidenceSource = "PRIVATE-SYNTHETIC-DID-SOURCE"
        };
        var result = Assess([entry], Snapshot() with
        {
            ProfileMarkedVerified = true,
            ProfileId = "PRIVATE-SYNTHETIC-PROFILE"
        });
        Assert.Equal(VolvoP2CemSupportKind.DocumentedReadOnlyDidSet, result.Kind);
        Assert.DoesNotContain(sensitive, result.Explanation);
        Assert.DoesNotContain("PRIVATE-SYNTHETIC", result.Explanation);
        Assert.Contains("source authenticity are unverified", result.Explanation);
        AssertUnverifiedAndDenied(result);
    }

    [Fact]
    public void OutOfRangeYearDoesNotPromoteAnyCandidateToVerifiedIdentity()
    {
        foreach (var year in new[] { 2002, 2015 })
        {
            var result = Assess([Entry()], year: year);
            Assert.Equal(VolvoP2CemSupportKind.Unknown, result.Kind);
            Assert.Null(result.VariantId);
            AssertUnverifiedAndDenied(result);
        }
    }

    [Fact]
    public void DistinctMatchingResearchVariantsStayAmbiguousAndUnattributed()
    {
        var other = Entry() with
        {
            Identity = Entry().Identity with
            {
                VariantId = "private.synthetic.revision.two"
            }
        };
        var result = Assess([Entry(), other]);
        Assert.Equal(VolvoP2CemSupportKind.Ambiguous, result.Kind);
        Assert.Null(result.VariantId);
        Assert.Empty(result.ObservedDocumentedDids);
        Assert.Contains("research fingerprints", result.Explanation);
        Assert.DoesNotContain("private.synthetic", result.Explanation);
        AssertUnverifiedAndDenied(result);
    }

    [Fact]
    public void InvalidCatalogEntryCannotSilentlyMakeTheOtherRuleAuthoritative()
    {
        var invalid = Entry() with
        {
            Identity = Entry().Identity with { RequestId = 0x800u }
        };
        var result = Assess([Entry(), invalid]);
        Assert.Equal(VolvoP2CemSupportKind.Unknown, result.Kind);
        Assert.Null(result.VariantId);
        AssertUnverifiedAndDenied(result);
    }

    [Fact]
    public void ExistingCaptureStatusKeepsSyntheticPhysicalCemUnconfirmed()
    {
        var review = VolvoP2CemCaptureIdentityReview.Assess(
            Snapshot(), independentlyConfirmedModelYear: 2007, evidence: [Entry()]);
        Assert.Equal(VolvoP2CemCaptureIdentityKind.ResearchSignatureMatched, review.Kind);
        Assert.Contains("physical CEM identity", review.ActivityMessage);
        Assert.Contains("unconfirmed", review.ActivityMessage);
        Assert.False(review.HasAuthenticatedPhysicalCem);
        Assert.False(review.MayReadCemConfiguration);
        Assert.False(review.MayAcquirePin);
        Assert.False(review.MayWriteConfiguration);
    }

    private static void AssertUnverifiedAndDenied(
        VolvoP2CemSupportAssessment result)
    {
        Assert.Contains("unverified", result.Explanation);
        Assert.False(result.HasAuthenticatedPhysicalCem);
        Assert.False(result.PinRetrievalSupported);
        Assert.False(result.ConfigurationWriteSupported);
        Assert.DoesNotContain("authenticated physical CEM", result.Explanation);
    }

    private static VolvoP2CemSupportAssessment Assess(
        IEnumerable<VolvoP2CemSupportEntry> entries,
        EcuConfigurationReadSnapshot? snapshot = null,
        int year = 2007) =>
        VolvoP2CemSupportMatrix.Assess(year, snapshot ?? Snapshot(), entries);

    private static EcuConfigurationReadSnapshot Snapshot() =>
        new("synthetic.cem", "Synthetic module", 0x7E0, 0x7E8, false, false,
            new DateTimeOffset(2026, 10, 27, 0, 0, 0, TimeSpan.Zero),
            [new EcuDidReadback(0xF187, "AA11"),
             new EcuDidReadback(0xF189, "BB22")]);

    private static VolvoP2CemSupportEntry Entry() =>
        new(2005, 2009, new EcuIdentityEvidenceRule(
            "synthetic.revision.one", "P2", "CEM",
            0x7E0, 0x7E8, false, true, "Synthetic offline research only",
            [new EcuDidReadback(0xF187, "AA11"),
             new EcuDidReadback(0xF189, "BB22")]),
            [0xF187, 0xF189], "Synthetic observed DID claims only");
}
