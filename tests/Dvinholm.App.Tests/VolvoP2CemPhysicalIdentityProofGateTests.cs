namespace Dvinholm.App.Tests;

/// <summary>
/// Eight synthetic, offline tests: a DID match or copied research reference
/// never authenticates a physical XC90 P2 CEM. No J2534 or ECU access.
/// </summary>
public sealed class VolvoP2CemPhysicalIdentityProofGateTests
{
    [Fact]
    public void ProductionHasNoPhysicalCemAttestationOrBundledSignatures()
    {
        Assert.Empty(VolvoP2CemSupportMatrix.ProductionEntries);
        var observed = VolvoP2CemSupportMatrix.Assess(
            2007, Snapshot(), VolvoP2CemSupportMatrix.ProductionEntries);
        Assert.Equal(VolvoP2CemSupportKind.Unknown, observed.Kind);
        Assert.False(observed.HasAuthenticatedPhysicalCem);
        AssertBlocked(VolvoP2CemServiceOperation.IdentifyCem, Context(observed));
    }

    [Fact]
    public void AMatchingSyntheticMultiDidFingerprintIsStillNotPhysicalIdentity()
    {
        var observed = VolvoP2CemSupportMatrix.Assess(
            2007, Snapshot(), [Entry()]);
        Assert.Equal(VolvoP2CemSupportKind.DocumentedReadOnlyDidSet, observed.Kind);
        Assert.Equal("synthetic.cem.revA", observed.VariantId);
        Assert.False(observed.HasAuthenticatedPhysicalCem);
        AssertBlocked(VolvoP2CemServiceOperation.IdentifyCem, Context(observed));
    }

    [Fact]
    public void AnIdentityOnlyMatchWithoutDocumentedDidsStillHasPhysicalGap()
    {
        var entry = Entry() with { DocumentedReadableDids = [] };
        var observed = VolvoP2CemSupportMatrix.Assess(
            2007, Snapshot(), [entry]);
        Assert.Equal(VolvoP2CemSupportKind.VerifiedIdentityReadOnly, observed.Kind);
        Assert.False(observed.HasAuthenticatedPhysicalCem);
        AssertBlocked(VolvoP2CemServiceOperation.ReadConfiguration, Context(observed));
    }

    [Fact]
    public void FullHypotheticalResearchReferencesCannotClosePhysicalIdentityGap()
    {
        var observed = SyntheticAssessment(VolvoP2CemSupportKind.VerifiedIdentityReadOnly);
        var context = Context(observed) with
        {
            ConfigurationMappingEvidenceReference = "Synthetic mapping document 01",
            LegitimateServiceProcedureEvidenceReference = "Synthetic service report 01",
            CredentialScope = VolvoP2ServiceCredentialScope.ConfigurationService,
            ConfigurationServiceEntitlementConfirmed = true,
            PhysicalJ2534BenchRecordReference = "Synthetic bench record 01",
            BackupRecoveryBenchRecordReference = "Synthetic recovery report 01"
        };
        var result = VolvoP2CemServiceCapabilityMatrix.Assess(
            VolvoP2CemServiceOperation.WriteConfiguration, context);
        Assert.Equal(new[]
        {
            VolvoP2ServiceEvidenceGap.ExactCemHardwareSoftwareIdentity,
            VolvoP2ServiceEvidenceGap.OperationalServiceNotImplemented
        }, result.BlockingRequirements);
        Denied(result);
    }

    [Fact]
    public void AllSixResearchOperationsKeepIdentityGapEvenForSyntheticVerifiedCard()
    {
        var context = Context(SyntheticAssessment(
            VolvoP2CemSupportKind.DocumentedReadOnlyDidSet));
        foreach (var operation in VolvoP2CemServiceCapabilityMatrix.ResearchOperations)
            AssertBlocked(operation, context);
    }

    [Fact]
    public void SelfClaimedYearAndSourceStringsAreNotPhysicalProof()
    {
        var context = Context(SyntheticAssessment(
            VolvoP2CemSupportKind.VerifiedIdentityReadOnly)) with
        {
            ModelYearIndependentlyConfirmed = false,
            PhysicalJ2534BenchRecordReference = "SENSITIVE-SYNTHETIC-REFERENCE-01"
        };
        var result = VolvoP2CemServiceCapabilityMatrix.Assess(
            VolvoP2CemServiceOperation.ReadConfiguration, context);
        Assert.Contains(VolvoP2ServiceEvidenceGap.ConfirmedXc90P2ModelYear,
            result.BlockingRequirements);
        Assert.Contains(VolvoP2ServiceEvidenceGap.ExactCemHardwareSoftwareIdentity,
            result.BlockingRequirements);
        Assert.DoesNotContain("SENSITIVE-SYNTHETIC", result.Explanation);
        Denied(result);
    }

    [Fact]
    public void UnknownAndAmbiguousResearchOutcomesCannotAuthenticateHardware()
    {
        foreach (var kind in new[]
        {
            VolvoP2CemSupportKind.Unknown,
            VolvoP2CemSupportKind.Ambiguous
        })
        {
            var observed = SyntheticAssessment(kind);
            Assert.False(observed.HasAuthenticatedPhysicalCem);
            AssertBlocked(VolvoP2CemServiceOperation.ReviewPinAcquisition,
                Context(observed));
        }
    }

    [Fact]
    public void ForgedVariantIdOrAbsentIdentityCannotCloseProofOrRequestPin()
    {
        foreach (var observed in new VolvoP2CemSupportAssessment?[]
        {
            null,
            SyntheticAssessment(VolvoP2CemSupportKind.VerifiedIdentityReadOnly)
                with { VariantId = null },
            SyntheticAssessment(VolvoP2CemSupportKind.DocumentedReadOnlyDidSet)
                with { VariantId = "user-says-verified-real-CEM" }
        })
        {
            var context = Context(observed);
            AssertBlocked(
                VolvoP2CemServiceOperation.AuthenticateConfigurationService, context);
        }
    }

    private static VolvoP2CemSupportAssessment SyntheticAssessment(
        VolvoP2CemSupportKind kind) =>
        new(kind, "synthetic.cem.revA", Array.Empty<ushort>(),
            "Fabricated research label, not authenticated hardware");

    private static VolvoP2CemServiceResearchContext Context(
        VolvoP2CemSupportAssessment? identity) =>
        new(ModelYear: 2007, ModelYearIndependentlyConfirmed: true,
            Identity: identity, ConfigurationMappingEvidenceReference: null,
            LegitimateServiceProcedureEvidenceReference: null,
            CredentialScope: VolvoP2ServiceCredentialScope.Unspecified,
            ConfigurationServiceEntitlementConfirmed: false,
            PhysicalJ2534BenchRecordReference: null,
            BackupRecoveryBenchRecordReference: null);

    private static void AssertBlocked(
        VolvoP2CemServiceOperation operation,
        VolvoP2CemServiceResearchContext context)
    {
        var result = VolvoP2CemServiceCapabilityMatrix.Assess(operation, context);
        Assert.Contains(VolvoP2ServiceEvidenceGap.ExactCemHardwareSoftwareIdentity,
            result.BlockingRequirements);
        Assert.Contains(VolvoP2ServiceEvidenceGap.OperationalServiceNotImplemented,
            result.BlockingRequirements);
        Denied(result);
    }

    private static void Denied(VolvoP2CemServiceCapabilityAssessment result)
    {
        Assert.False(result.MaySendEcuRequest);
        Assert.False(result.MayAcquirePin);
        Assert.False(result.MayAuthenticateService);
        Assert.False(result.MayReadOrWriteConfiguration);
    }

    private static EcuConfigurationReadSnapshot Snapshot() =>
        new("synthetic.profile", "Synthetic P2 CEM profile", 0x7E0, 0x7E8,
            false, false,
            new DateTimeOffset(2026, 10, 27, 0, 0, 0, TimeSpan.Zero),
            [new EcuDidReadback(0xF187, "AA11"),
             new EcuDidReadback(0xF189, "BB22")]);

    private static VolvoP2CemSupportEntry Entry() =>
        new(2005, 2009,
            new EcuIdentityEvidenceRule("synthetic.cem.revA", "P2", "CEM",
                0x7E0, 0x7E8, false, true,
                "Offline fabricated identity fingerprint only",
                [new EcuDidReadback(0xF187, "AA11"),
                 new EcuDidReadback(0xF189, "BB22")]),
            [0xF187, 0xF189], "Fabricated DID metadata fixture only");
}
