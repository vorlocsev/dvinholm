namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline synthetic checks for the documented XC90 P2 model-year
/// boundary and genuine UTC provenance in read-only signature matching.
/// </summary>
public sealed class VolvoP2CemSignatureScopeAndUtcTests
{
    [Fact]
    public void CatalogAcceptsTheDocumentedFirstAndLastModelYearOnly()
    {
        foreach (int year in new[] { 2003, 2014 })
        {
            Assert.Empty(VolvoP2CemEvidenceCatalogValidator.Validate(
                [Entry() with { ModelYearFrom = year, ModelYearThrough = year }]));
        }
        Assert.Empty(VolvoP2CemEvidenceCatalogValidator.Validate(
            [Entry() with { ModelYearFrom = 2003, ModelYearThrough = 2014 }]));
    }

    [Fact]
    public void CatalogRejectsMy2002EvenIfTheFingerprintClaimsVerification()
    {
        var issues = VolvoP2CemEvidenceCatalogValidator.Validate(
            [Entry() with { ModelYearFrom = 2002, ModelYearThrough = 2003 }]);
        Assert.Contains(issues,
            issue => issue.Code == VolvoP2CemEvidenceIssueCode.InvalidScope);
    }

    [Fact]
    public void CatalogRejectsMy2015EvenIfTheFingerprintClaimsVerification()
    {
        var issues = VolvoP2CemEvidenceCatalogValidator.Validate(
            [Entry() with { ModelYearFrom = 2014, ModelYearThrough = 2015 }]);
        Assert.Contains(issues,
            issue => issue.Code == VolvoP2CemEvidenceIssueCode.InvalidScope);
    }

    [Fact]
    public void CatalogCannotHideOutOfScopeYearInsideABroaderSpan()
    {
        foreach (var entry in new[]
        {
            Entry() with { ModelYearFrom = 1900, ModelYearThrough = 2014 },
            Entry() with { ModelYearFrom = 2003, ModelYearThrough = 2100 },
            Entry() with { ModelYearFrom = 2002, ModelYearThrough = 2015 }
        })
        {
            Assert.Contains(VolvoP2CemEvidenceCatalogValidator.Validate([entry]),
                issue => issue.Code == VolvoP2CemEvidenceIssueCode.InvalidScope);
        }
    }

    [Fact]
    public void DirectSupportMatcherRejectsOutOfScopeModelYears()
    {
        foreach (int year in new[] { 2002, 2015, 1900, 2100 })
        {
            var result = VolvoP2CemSupportMatrix.Assess(
                year, Snapshot(),
                [Entry() with { ModelYearFrom = year, ModelYearThrough = year }]);
            Denied(result);
        }
    }

    [Fact]
    public void MissingCaptureTimestampCannotMatchSyntheticSignature()
    {
        var capture = Snapshot() with { CapturedAtUtc = default };
        var result = VolvoP2CemSupportMatrix.Assess(2007, capture, [Entry()]);
        Denied(result);
    }

    [Fact]
    public void NonUtcTimestampCannotMatchEvenWithMatchingDidsAndCanIds()
    {
        var capture = Snapshot() with
        {
            CapturedAtUtc = new DateTimeOffset(
                2026, 10, 27, 12, 0, 0, TimeSpan.FromHours(2))
        };
        Denied(VolvoP2CemSupportMatrix.Assess(2007, capture, [Entry()]));
    }

    [Fact]
    public void ValidUtcSyntheticMatchRemainsResearchOnlyAndProductionIsEmpty()
    {
        var result = VolvoP2CemSupportMatrix.Assess(2007, Snapshot(), [Entry()]);
        Assert.Equal(VolvoP2CemSupportKind.DocumentedReadOnlyDidSet, result.Kind);
        Assert.Equal(2, result.ObservedDocumentedDids.Count);
        Assert.False(result.PinRetrievalSupported);
        Assert.False(result.ConfigurationWriteSupported);
        Assert.Empty(VolvoP2CemSupportMatrix.ProductionEntries);
        Denied(VolvoP2CemSupportMatrix.Assess(
            2007, Snapshot(), VolvoP2CemSupportMatrix.ProductionEntries));
    }

    private static void Denied(VolvoP2CemSupportAssessment result)
    {
        Assert.Equal(VolvoP2CemSupportKind.Unknown, result.Kind);
        Assert.Null(result.VariantId);
        Assert.Empty(result.ObservedDocumentedDids);
        Assert.False(result.PinRetrievalSupported);
        Assert.False(result.ConfigurationWriteSupported);
    }

    private static EcuConfigurationReadSnapshot Snapshot() =>
        new("synthetic.p2.cem", "Synthetic research only", 0x7E0, 0x7E8,
            false, false, new DateTimeOffset(
                2026, 10, 27, 0, 0, 0, TimeSpan.Zero),
            [new EcuDidReadback(0xF187, "AA11"),
             new EcuDidReadback(0xF189, "BB22")]);

    private static VolvoP2CemSupportEntry Entry() =>
        new(2005, 2009,
            new EcuIdentityEvidenceRule(
                "synthetic.p2.cem.revA", "P2", "CEM",
                0x7E0, 0x7E8, false, true,
                "Offline synthetic test fixture, no physical observation",
                [new EcuDidReadback(0xF187, "AA11"),
                 new EcuDidReadback(0xF189, "BB22")]),
            [0xF187, 0xF189], "Synthetic read-only DID test fixture");
}
