namespace Dvinholm.App.Tests;

/// <summary>
/// Eight deterministic synthetic tests. The fake CAN IDs and DID bytes are
/// NOT Volvo P2/CEM specifications or production compatibility claims.
/// </summary>
public sealed class VolvoP2CemSupportMatrixTests
{
    [Fact]
    public void ProductionCatalogIsEmptyUntilRealEvidenceExists()
    {
        Assert.Empty(VolvoP2CemSupportMatrix.ProductionEntries);
        var assessment = VolvoP2CemSupportMatrix.Assess(
            2007, Snapshot(), VolvoP2CemSupportMatrix.ProductionEntries);
        Assert.Equal(VolvoP2CemSupportKind.Unknown, assessment.Kind);
        Assert.Null(assessment.VariantId);
        Assert.False(assessment.PinRetrievalSupported);
        Assert.False(assessment.ConfigurationWriteSupported);
    }

    [Fact]
    public void ExactVerifiedHardwareSoftwareFingerprintSupportsOnlyReadOnlyIdentification()
    {
        var result = VolvoP2CemSupportMatrix.Assess(
            2007, Snapshot(), [Entry() with { DocumentedReadableDids = [] }]);
        Assert.Equal(VolvoP2CemSupportKind.VerifiedIdentityReadOnly, result.Kind);
        Assert.Equal("synthetic.p2.cem.revA", result.VariantId);
        Assert.Empty(result.ObservedDocumentedDids);
        Assert.False(result.PinRetrievalSupported);
        Assert.False(result.ConfigurationWriteSupported);
    }

    [Fact]
    public void SeparatelyDocumentedAndObservedDidsAreListedDeterministically()
    {
        var result = VolvoP2CemSupportMatrix.Assess(
            2007, Snapshot(), [Entry()]);
        Assert.Equal(VolvoP2CemSupportKind.DocumentedReadOnlyDidSet, result.Kind);
        Assert.Equal(new ushort[] { 0xF187, 0xF189 },
            result.ObservedDocumentedDids);
        Assert.False(result.PinRetrievalSupported);
        Assert.False(result.ConfigurationWriteSupported);
        var immutable = Assert.IsAssignableFrom<IList<ushort>>(result.ObservedDocumentedDids);
        Assert.True(immutable.IsReadOnly);
    }

    [Fact]
    public void WrongOrMissingModelYearNeverSelectsVariant()
    {
        foreach (var year in new[] { 1901, 2004, 2010, 2101 })
        {
            var result = VolvoP2CemSupportMatrix.Assess(
                year, Snapshot(), [Entry() with { ModelYearFrom = 2005, ModelYearThrough = 2009 }]);
            Assert.Equal(VolvoP2CemSupportKind.Unknown, result.Kind);
        }

        Assert.Equal(VolvoP2CemSupportKind.DocumentedReadOnlyDidSet,
            VolvoP2CemSupportMatrix.Assess(2005, Snapshot(), [Entry()]).Kind);
        Assert.Equal(VolvoP2CemSupportKind.DocumentedReadOnlyDidSet,
            VolvoP2CemSupportMatrix.Assess(2009, Snapshot(), [Entry()]).Kind);
    }

    [Fact]
    public void UnverifiedOrDifferentPlatformAndModuleCannotMatch()
    {
        var original = Entry();
        var invalid = new[]
        {
            original with { Identity = original.Identity with { IsVerified = false } },
            original with { Identity = original.Identity with { Platform = "P3" } },
            original with { Identity = original.Identity with { ModuleName = "ECM" } },
            original with { ModelYearFrom = 2010, ModelYearThrough = 2009 },
            original with { Identity = original.Identity with { EvidenceSource = "" } },
            original with { ReadDidEvidenceSource = "" }
        };
        foreach (var entry in invalid)
            Assert.Equal(VolvoP2CemSupportKind.Unknown,
                VolvoP2CemSupportMatrix.Assess(2007, Snapshot(), [entry]).Kind);
    }

    [Fact]
    public void WrongCanIdsSoftwareRevisionOrMissingFingerprintKeepsUnknown()
    {
        var input = Snapshot();
        var altered = new[]
        {
            input with { RequestId = 0x7E1 },
            input with { ResponseId = 0x7E9 },
            input with { IsExtended = true },
            input with { Values = [new EcuDidReadback(0xF187, "AA11"),
                new EcuDidReadback(0xF189, "CC33")] },
            input with { Values = [new EcuDidReadback(0xF187, "AA11")] }
        };
        foreach (var item in altered)
            Assert.Equal(VolvoP2CemSupportKind.Unknown,
                VolvoP2CemSupportMatrix.Assess(2007, item, [Entry()]).Kind);
    }

    [Fact]
    public void ConflictingVerifiedEntriesFailClosedAsAmbiguous()
    {
        var original = Entry();
        var conflict = original with
        {
            Identity = original.Identity with { VariantId = "synthetic.other.rev" }
        };
        var assessment = VolvoP2CemSupportMatrix.Assess(
            2007, Snapshot(), [original, conflict]);
        Assert.Equal(VolvoP2CemSupportKind.Ambiguous, assessment.Kind);
        Assert.Null(assessment.VariantId);
        Assert.Empty(assessment.ObservedDocumentedDids);
    }

    [Fact]
    public void VinMappingsDuplicateDidClaimsAndAbsentDocumentedDidsCannotGrantWrite()
    {
        var original = Entry();
        var invalid = new[]
        {
            original with { DocumentedReadableDids = [0xF190] },
            original with { DocumentedReadableDids = [0xF187, 0xF187] }
        };
        foreach (var entry in invalid)
            Assert.Equal(VolvoP2CemSupportKind.Unknown,
                VolvoP2CemSupportMatrix.Assess(2007, Snapshot(), [entry]).Kind);

        var absent = original with { DocumentedReadableDids = [0xF188] };
        var result = VolvoP2CemSupportMatrix.Assess(2007, Snapshot(), [absent]);
        Assert.Equal(VolvoP2CemSupportKind.VerifiedIdentityReadOnly, result.Kind);
        Assert.Empty(result.ObservedDocumentedDids);
        Assert.False(result.PinRetrievalSupported);
        Assert.False(result.ConfigurationWriteSupported);
    }

    private static EcuConfigurationReadSnapshot Snapshot() =>
        new("synthetic.cem", "Synthetic CEM", 0x7E0, 0x7E8, false, false,
            new DateTimeOffset(2026, 10, 27, 0, 0, 0, TimeSpan.Zero),
            [
                new EcuDidReadback(0xF187, "AA11"),
                new EcuDidReadback(0xF189, "BB22")
            ]);

    private static VolvoP2CemSupportEntry Entry() =>
        new(2005, 2009,
            new EcuIdentityEvidenceRule("synthetic.p2.cem.revA", "P2", "CEM",
                0x7E0, 0x7E8, false, true, "Synthetic independent test fixture",
                [
                    new EcuDidReadback(0xF187, "AA11"),
                    new EcuDidReadback(0xF189, "BB22")
                ]),
            [0xF189, 0xF187], "Synthetic DID mapping fixture only");
}
