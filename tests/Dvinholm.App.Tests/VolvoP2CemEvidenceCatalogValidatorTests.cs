namespace Dvinholm.App.Tests;

/// <summary>
/// Eight synthetic offline catalog-quality checks. A source string or
/// IsVerified flag alone is NOT proof of real Volvo CEM compatibility.
/// </summary>
public sealed class VolvoP2CemEvidenceCatalogValidatorTests
{
    [Fact]
    public void EmptyCatalogIsStructurallyValidButSupportsNothing()
    {
        Assert.Empty(VolvoP2CemEvidenceCatalogValidator.Validate([]));
        Assert.Empty(VolvoP2CemSupportMatrix.ProductionEntries);
    }

    [Fact]
    public void TwoDidSourcedSyntheticCatalogCanYieldReadOnlyAssessmentOnly()
    {
        var item = Entry();
        Assert.Empty(VolvoP2CemEvidenceCatalogValidator.Validate([item]));
        var observed = VolvoP2CemSupportMatrix.Assess(2007, Snapshot(), [item]);
        Assert.Equal(VolvoP2CemSupportKind.DocumentedReadOnlyDidSet, observed.Kind);
        Assert.False(observed.PinRetrievalSupported);
        Assert.False(observed.ConfigurationWriteSupported);
    }

    [Fact]
    public void WrongOrUnverifiedScopeIsRejected()
    {
        var baseline = Entry();
        var invalid = new[]
        {
            baseline with { Identity = baseline.Identity with { IsVerified = false } },
            baseline with { Identity = baseline.Identity with { Platform = "P3" } },
            baseline with { Identity = baseline.Identity with { ModuleName = "ECM" } },
            baseline with { Identity = baseline.Identity with { EvidenceSource = "" } },
            baseline with { Identity = baseline.Identity with { VariantId = new string('a', 121) } }
        };
        foreach (var candidate in invalid)
            Assert.Single(VolvoP2CemEvidenceCatalogValidator.Validate([candidate]));
    }

    [Fact]
    public void InvalidYearRangeAndCanAddressingAreRejected()
    {
        var baseline = Entry();
        var invalidYears = new[]
        {
            baseline with { ModelYearFrom = 1899 },
            baseline with { ModelYearThrough = 2101 },
            baseline with { ModelYearFrom = 2009, ModelYearThrough = 2005 }
        };
        foreach (var candidate in invalidYears)
            Assert.Contains(VolvoP2CemEvidenceCatalogValidator.Validate([candidate]),
                issue => issue.Code == VolvoP2CemEvidenceIssueCode.InvalidScope);

        var invalidCan = new[]
        {
            baseline with { Identity = baseline.Identity with { RequestId = 0x800 } },
            baseline with { Identity = baseline.Identity with { ResponseId = 0x800 } },
            baseline with { Identity = baseline.Identity with { ResponseId = 0x7E0 } }
        };
        foreach (var candidate in invalidCan)
            Assert.Contains(VolvoP2CemEvidenceCatalogValidator.Validate([candidate]),
                issue => issue.Code == VolvoP2CemEvidenceIssueCode.InvalidCanAddressing);
    }

    [Fact]
    public void MissingDuplicateVinOrMalformedIdentityFingerprintIsRejected()
    {
        var baseline = Entry();
        var invalid = new[]
        {
            baseline with { Identity = baseline.Identity with
                { RequiredIdentifiers = [new EcuDidReadback(0xF187, "AA")] } },
            baseline with { Identity = baseline.Identity with
                { RequiredIdentifiers = [
                    new EcuDidReadback(0xF187, "AA"),
                    new EcuDidReadback(0xF187, "BB")] } },
            baseline with { Identity = baseline.Identity with
                { RequiredIdentifiers = [
                    new EcuDidReadback(0xF187, "AA"),
                    new EcuDidReadback(0xF190, "BB")] } },
            baseline with { Identity = baseline.Identity with
                { RequiredIdentifiers = [
                    new EcuDidReadback(0xF187, "AA"),
                    new EcuDidReadback(0xF189, "ZZ")] } },
            baseline with { Identity = baseline.Identity with
                { RequiredIdentifiers = [
                    new EcuDidReadback(0xF187, "AA"),
                    new EcuDidReadback(0xF189, "ABC")] } }
        };
        foreach (var candidate in invalid)
        {
            Assert.Contains(VolvoP2CemEvidenceCatalogValidator.Validate([candidate]),
                issue => issue.Code == VolvoP2CemEvidenceIssueCode.InvalidFingerprint);
            Assert.Equal(VolvoP2CemSupportKind.Unknown,
                VolvoP2CemSupportMatrix.Assess(2007, Snapshot(), [candidate]).Kind);
        }
    }

    [Fact]
    public void UnsourcedDuplicateVinAndExcessiveDocumentedDidsAreRejected()
    {
        var baseline = Entry();
        var invalid = new[]
        {
            baseline with { ReadDidEvidenceSource = "" },
            baseline with { DocumentedReadableDids = [0xF190] },
            baseline with { DocumentedReadableDids = [0xF187, 0xF187] },
            baseline with {
                DocumentedReadableDids = Enumerable.Range(0, 129)
                    .Select(i => (ushort)(0xF000 + i)).ToArray()
            }
        };
        foreach (var candidate in invalid)
            Assert.Contains(VolvoP2CemEvidenceCatalogValidator.Validate([candidate]),
                issue => issue.Code == VolvoP2CemEvidenceIssueCode.InvalidDidMapping);

        Assert.Empty(VolvoP2CemEvidenceCatalogValidator.Validate(
            [baseline with { DocumentedReadableDids = [], ReadDidEvidenceSource = "" }]));
    }

    [Fact]
    public void OverlappingVariantYearsFailClosedButDistinctFirmwareMayShareCanIds()
    {
        var first = Entry();
        var overlapping = first with { ModelYearFrom = 2008, ModelYearThrough = 2012 };
        var issues = VolvoP2CemEvidenceCatalogValidator.Validate([first, overlapping]);
        Assert.Contains(issues, x =>
            x.Code == VolvoP2CemEvidenceIssueCode.ConflictingVariantYears &&
            x.EntryIndex == 1);
        Assert.Equal(VolvoP2CemSupportKind.Unknown,
            VolvoP2CemSupportMatrix.Assess(2008, Snapshot(), [first, overlapping]).Kind);

        var disjoint = first with { ModelYearFrom = 2010, ModelYearThrough = 2014 };
        Assert.Empty(VolvoP2CemEvidenceCatalogValidator.Validate([first, disjoint]));

        var differentRevision = first with { Identity = first.Identity with {
            VariantId = "synthetic.p2.revB",
            RequiredIdentifiers = [
                new EcuDidReadback(0xF187, "CC"),
                new EcuDidReadback(0xF189, "DD")
            ]
        } };
        Assert.Empty(VolvoP2CemEvidenceCatalogValidator.Validate([first, differentRevision]));
    }

    [Fact]
    public void OversizedCatalogStopsAfter129thEntryAndIssuesRemainReadOnly()
    {
        var first = Entry();
        var many = Enumerable.Range(0, 130).Select(i => first with {
            Identity = first.Identity with { VariantId = "synthetic-" + i }
        });
        var issues = VolvoP2CemEvidenceCatalogValidator.Validate(many);
        Assert.Contains(issues, x => x.Code == VolvoP2CemEvidenceIssueCode.TooManyEntries &&
            x.EntryIndex == VolvoP2CemEvidenceCatalogValidator.MaximumEntries);

        var list = Assert.IsAssignableFrom<IList<VolvoP2CemEvidenceIssue>>(issues);
        Assert.True(list.IsReadOnly);
        Assert.Equal(VolvoP2CemSupportKind.Unknown,
            VolvoP2CemSupportMatrix.Assess(2007, Snapshot(), many).Kind);
    }

    private static VolvoP2CemSupportEntry Entry() =>
        new(2005, 2009,
            new EcuIdentityEvidenceRule(
                "synthetic.p2.revA", "P2", "CEM", 0x7E0, 0x7E8, false,
                true, "Independent synthetic test data; not actual vehicle evidence",
                [
                    new EcuDidReadback(0xF187, "AA11"),
                    new EcuDidReadback(0xF189, "BB22")
                ]),
            [0xF187, 0xF189], "Synthetic DID observation");

    private static EcuConfigurationReadSnapshot Snapshot() =>
        new("synthetic.cem", "Synthetic CEM", 0x7E0, 0x7E8, false,
            false, new DateTimeOffset(2026, 10, 27, 0, 0, 0, TimeSpan.Zero),
            [
                new EcuDidReadback(0xF187, "AA11"),
                new EcuDidReadback(0xF189, "BB22")
            ]);
}
