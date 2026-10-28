namespace Dvinholm.App.Tests;

/// <summary>
/// Eight synthetic-only assertions for a historical/offline DID evidence
/// cross-check. Fake addresses/DIDs are not real Volvo CEM specifications.
/// </summary>
public sealed class VolvoP2CemHistoricalDidEvidenceTests
{
    [Fact]
    public void EmptyProductionCatalogKeepsHistoricalIdentityUnknown()
    {
        Assert.Empty(VolvoP2CemSupportMatrix.ProductionEntries);
        var result = VolvoP2CemHistoricalDidEvidence.Assess(
            Card(), Historical(), VolvoP2CemSupportMatrix.ProductionEntries);
        Assert.Equal(VolvoP2CemHistoricalDidEvidenceKind.MissingProductionEvidence,
            result.Kind);
        Assert.Empty(result.PreviouslyObservedDocumentedDids);
        Assert.False(result.CanReadLiveConfiguration);
    }

    [Fact]
    public void SyntheticExactRevisionOnlyClassifiesPreviouslyObservedDocumentedDid()
    {
        var result = VolvoP2CemHistoricalDidEvidence.Assess(
            Card(), Historical(), [Evidence()]);
        Assert.Equal(VolvoP2CemHistoricalDidEvidenceKind.DocumentedPreviouslyObservedDidsResearchOnly,
            result.Kind);
        Assert.Equal(new ushort[] { 0xF188 },
            result.PreviouslyObservedDocumentedDids);
        Assert.False(result.HasAuthenticatedPhysicalCem);
        Assert.False(result.HasProvenSameVehicleOrModule);
        Assert.False(result.HasVerifiedConfigurationSemantics);
        Assert.False(result.CanSendJ2534OrUds);
        Assert.False(result.CanReadLiveConfiguration);
        Assert.False(result.CanAcquirePin);
        Assert.False(result.CanWriteConfiguration);
        Assert.True(Assert.IsAssignableFrom<IList<ushort>>(
            result.PreviouslyObservedDocumentedDids).IsReadOnly);
    }

    [Fact]
    public void UnobservedDocumentedDidIsNotAReadPlan()
    {
        var historical = Historical(includeDocumentedValue: false);
        var result = VolvoP2CemHistoricalDidEvidence.Assess(
            Card(), historical, [Evidence()]);
        Assert.Equal(VolvoP2CemHistoricalDidEvidenceKind.IdentityOnlyNoDocumentedDids,
            result.Kind);
        Assert.Empty(result.PreviouslyObservedDocumentedDids);
    }

    [Fact]
    public void ChangedFirmwareCanAddressAndClaimedYearNeverMatchRevision()
    {
        var inputs = new[]
        {
            (Card(), Historical(firmware: "AA12")),
            (Card(), Historical(requestId: 0x7E1u)),
            (Card() with { ClaimedModelYear = 2011 }, Historical())
        };
        foreach (var (card, historical) in inputs)
        {
            var result = VolvoP2CemHistoricalDidEvidence.Assess(
                card, historical, [Evidence()]);
            Assert.Equal(VolvoP2CemHistoricalDidEvidenceKind.UnknownOrUnmatchedRevision,
                result.Kind);
            Assert.Empty(result.PreviouslyObservedDocumentedDids);
        }
    }

    [Fact]
    public void InvalidNonmatchingCatalogEntryInvalidatesAllEvidence()
    {
        var original = Evidence();
        var invalid = original with
        {
            ModelYearFrom = 2010,
            ModelYearThrough = 2011,
            Identity = original.Identity with
            {
                VariantId = "synthetic.other",
                RequestId = 0x800u
            }
        };
        var result = VolvoP2CemHistoricalDidEvidence.Assess(
            Card(), Historical(), [original, invalid]);
        Assert.Equal(VolvoP2CemHistoricalDidEvidenceKind.InvalidEvidenceCatalog,
            result.Kind);
        Assert.Empty(result.PreviouslyObservedDocumentedDids);
    }

    [Fact]
    public void MultiplePlausibleSyntheticRevisionsRemainAmbiguous()
    {
        var original = Evidence();
        var second = original with
        {
            Identity = original.Identity with { VariantId = "synthetic.alternative" }
        };
        var result = VolvoP2CemHistoricalDidEvidence.Assess(
            Card(), Historical(), [original, second]);
        Assert.Equal(VolvoP2CemHistoricalDidEvidenceKind.AmbiguousRevision,
            result.Kind);
        Assert.Empty(result.PreviouslyObservedDocumentedDids);
    }

    [Fact]
    public void ForgedManualAndMalformedSnapshotNeverPromoteIdentity()
    {
        Assert.Throws<InvalidDataException>(() =>
            VolvoP2CemHistoricalDidEvidence.Assess(
                Card() with { MayAccessEcu = true }, Historical(), [Evidence()]));
        Assert.Throws<InvalidDataException>(() =>
            VolvoP2CemHistoricalDidEvidence.Assess(
                Card(), Historical() with
                {
                    Values =
                    [
                        new EcuDidReadback(0xF187, "AA11"),
                        new EcuDidReadback(0xF187, "AA11")
                    ]
                }, [Evidence()]));
    }

    [Fact]
    public void ExtraEvidenceReportRedactsEveryRawIdentifierAndValue()
    {
        var historical = Historical();
        var report = VolvoP2CemHistoricalDidEvidence.BuildReadOnlyReport(
            Card(), historical, [Evidence()]);
        Assert.Contains("DocumentedPreviouslyObservedDidsResearchOnly", report);
        Assert.Contains("DISABLED", report);
        foreach (var secret in new[]
        {
            "HW-001", "SW-001", "DIAG-001", "AA11", "BB22",
            "CAFE", "7E0", "7E8", "synthetic.test.profile",
            "synthetic.cem.revA", "Synthetic DID catalog attribution"
        })
            Assert.DoesNotContain(secret, report, StringComparison.OrdinalIgnoreCase);
    }

    private static VolvoP2CemIdentityResearchDocument Card() =>
        VolvoP2CemIdentityResearchExport.InspectJson(
            VolvoP2CemIdentityResearchExport.CreateJson(
                "2007", "HW-001", "SW-001", "DIAG-001", true));

    private static EcuConfigurationExportDocument Historical(
        string firmware = "AA11", uint requestId = 0x7E0u,
        bool includeDocumentedValue = true)
    {
        var dids = new List<EcuDidReadback>
        {
            new(0xF187, firmware),
            new(0xF189, "BB22")
        };
        if (includeDocumentedValue)
            dids.Add(new EcuDidReadback(0xF188, "CAFE"));

        var snapshot = new EcuConfigurationReadSnapshot(
            "synthetic.test.profile", "Synthetic module",
            requestId, 0x7E8u, false, false,
            new DateTimeOffset(2026, 10, 28, 0, 0, 0, TimeSpan.Zero),
            dids);
        return EcuConfigurationSnapshotInspection.InspectJson(
            EcuConfigurationSnapshotExport.CreateJson(snapshot, true));
    }

    private static VolvoP2CemSupportEntry Evidence() =>
        new(2005, 2009,
            new EcuIdentityEvidenceRule(
                "synthetic.cem.revA", "P2", "CEM",
                0x7E0, 0x7E8, false, true,
                "Synthetic offline identity catalog attribution",
                [
                    new EcuDidReadback(0xF187, "AA11"),
                    new EcuDidReadback(0xF189, "BB22")
                ]),
            [0xF188],
            "Synthetic DID catalog attribution, not a real Volvo mapping");
}
