namespace Dvinholm.App.Tests;

/// <summary>
/// Eight deterministic offline permission-denial tests. All evidence
/// references and CEM identities are synthetic, not Volvo specifications.
/// </summary>
public sealed class VolvoP2CemServiceCapabilityMatrixTests
{
    [Fact]
    public void AllSixServiceCategoriesExistButNoneCanExecute()
    {
        Assert.Equal(6, VolvoP2CemServiceCapabilityMatrix.ResearchOperations.Count);
        Assert.Empty(VolvoP2CemSupportMatrix.ProductionEntries);
        foreach (var operation in VolvoP2CemServiceCapabilityMatrix.ResearchOperations)
        {
            var result = Assess(operation, Unknown());
            Assert.Contains(VolvoP2ServiceEvidenceGap.OperationalServiceNotImplemented,
                result.BlockingRequirements);
            Assert.False(result.MaySendEcuRequest);
            Assert.False(result.MayAcquirePin);
            Assert.False(result.MayAuthenticateService);
            Assert.False(result.MayReadOrWriteConfiguration);
        }
    }

    [Fact]
    public void UnknownOrUnconfirmedYearNeverEstablishesCompatibility()
    {
        foreach (var context in new[]
        {
            Unknown() with { ModelYear = null },
            Unknown() with { ModelYear = 2007, ModelYearIndependentlyConfirmed = false }
        })
        {
            var assessment = Assess(VolvoP2CemServiceOperation.IdentifyCem, context);
            Assert.Contains(VolvoP2ServiceEvidenceGap.ConfirmedXc90P2ModelYear,
                assessment.BlockingRequirements);
        }
    }

    [Fact]
    public void OutsideFirstGenerationYearsAndUnknownOrAmbiguousCemStayBlocked()
    {
        foreach (var year in new[] { 2002, 2015 })
            Assert.Contains(VolvoP2ServiceEvidenceGap.ConfirmedXc90P2ModelYear,
                Assess(VolvoP2CemServiceOperation.ReadConfiguration,
                    Unknown() with { ModelYear = year }).BlockingRequirements);

        foreach (var kind in new[] { VolvoP2CemSupportKind.Unknown,
                                       VolvoP2CemSupportKind.Ambiguous })
            Assert.Contains(VolvoP2ServiceEvidenceGap.ExactCemHardwareSoftwareIdentity,
                Assess(VolvoP2CemServiceOperation.IdentifyCem,
                    Unknown() with { Identity = Identity(kind) }).BlockingRequirements);
    }

    [Fact]
    public void SyntheticReadOnlyIdentityCannotAuthorizeService()
    {
        var context = Unknown() with
        {
            Identity = Identity(VolvoP2CemSupportKind.DocumentedReadOnlyDidSet)
        };
        var result = Assess(VolvoP2CemServiceOperation.AuthenticateConfigurationService, context);
        Assert.DoesNotContain(VolvoP2ServiceEvidenceGap.ExactCemHardwareSoftwareIdentity,
            result.BlockingRequirements);
        Assert.Contains(VolvoP2ServiceEvidenceGap.IndependentlyDocumentedLegitimateServiceRoute,
            result.BlockingRequirements);
        Assert.False(result.MayAuthenticateService);
        Assert.False(result.MaySendEcuRequest);
    }

    [Fact]
    public void ConfigurationReadAndPreviewRequireExplicitMappingEvidence()
    {
        foreach (var operation in new[]
        {
            VolvoP2CemServiceOperation.ReadConfiguration,
            VolvoP2CemServiceOperation.PreviewConfigurationChange,
            VolvoP2CemServiceOperation.WriteConfiguration
        })
        {
            var result = Assess(operation, Unknown());
            Assert.Contains(VolvoP2ServiceEvidenceGap.IndependentlyDocumentedConfigurationMapping,
                result.BlockingRequirements);
            var hypotheticallySourced = Assess(operation,
                Unknown() with { ConfigurationMappingEvidenceReference = "Synthetic mapping review 01" });
            Assert.DoesNotContain(VolvoP2ServiceEvidenceGap.IndependentlyDocumentedConfigurationMapping,
                hypotheticallySourced.BlockingRequirements);
            Assert.False(hypotheticallySourced.MayReadOrWriteConfiguration);
        }
    }

    [Fact]
    public void PinAndServiceRequireLegitimateRouteAndConfigurationScopedEntitlement()
    {
        foreach (var operation in new[]
        {
            VolvoP2CemServiceOperation.ReviewPinAcquisition,
            VolvoP2CemServiceOperation.AuthenticateConfigurationService
        })
        {
            var unknown = Assess(operation, Unknown());
            Assert.Contains(VolvoP2ServiceEvidenceGap.IndependentlyDocumentedLegitimateServiceRoute,
                unknown.BlockingRequirements);
            Assert.Contains(VolvoP2ServiceEvidenceGap.ConfigurationServiceCredentialScopeAndEntitlement,
                unknown.BlockingRequirements);
            var immobilizer = Assess(operation, FullHypothetical() with
            {
                CredentialScope = VolvoP2ServiceCredentialScope.ImmobilizerOrKeyMaterial
            });
            Assert.Contains(VolvoP2ServiceEvidenceGap.ConfigurationServiceCredentialScopeAndEntitlement,
                immobilizer.BlockingRequirements);
            Assert.False(immobilizer.MayAcquirePin);
        }
    }

    [Fact]
    public void ActualHardwareAndRecoveryReferencesAreSeparateWriteGates()
    {
        var context = FullHypothetical() with
        {
            PhysicalJ2534BenchRecordReference = null,
            BackupRecoveryBenchRecordReference = "  "
        };
        var result = Assess(VolvoP2CemServiceOperation.WriteConfiguration, context);
        Assert.Contains(VolvoP2ServiceEvidenceGap.PhysicalJ2534CemAcceptance,
            result.BlockingRequirements);
        Assert.Contains(VolvoP2ServiceEvidenceGap.TestedBackupAndRecovery,
            result.BlockingRequirements);
        Assert.False(result.MayReadOrWriteConfiguration);
    }

    [Fact]
    public void HypotheticalCompleteClaimsNeverGrantPermissionLeakReferencesOrAllowMutation()
    {
        var context = FullHypothetical() with
        {
            LegitimateServiceProcedureEvidenceReference = "SensitiveSyntheticReference-ONLY-TEST",
            PhysicalJ2534BenchRecordReference = "Synthetic hardware report 01"
        };
        foreach (var operation in VolvoP2CemServiceCapabilityMatrix.ResearchOperations)
        {
            var result = Assess(operation, context);
            Assert.Equal(new[] { VolvoP2ServiceEvidenceGap.OperationalServiceNotImplemented },
                result.BlockingRequirements);
            Assert.False(result.MaySendEcuRequest);
            Assert.False(result.MayAcquirePin);
            Assert.False(result.MayAuthenticateService);
            Assert.False(result.MayReadOrWriteConfiguration);
            Assert.DoesNotContain("SensitiveSyntheticReference", result.Explanation);
            var list = Assert.IsAssignableFrom<IList<VolvoP2ServiceEvidenceGap>>(
                result.BlockingRequirements);
            Assert.Throws<NotSupportedException>(() =>
                list[0] = VolvoP2ServiceEvidenceGap.ConfirmedXc90P2ModelYear);
        }
        Assert.Equal(new[] { VolvoP2ServiceEvidenceGap.OperationalServiceNotImplemented },
            Assess((VolvoP2CemServiceOperation)1000, context).BlockingRequirements);
    }

    private static VolvoP2CemServiceCapabilityAssessment Assess(
        VolvoP2CemServiceOperation operation, VolvoP2CemServiceResearchContext context) =>
        VolvoP2CemServiceCapabilityMatrix.Assess(operation, context);

    private static VolvoP2CemSupportAssessment Identity(VolvoP2CemSupportKind kind) =>
        new(kind, kind == VolvoP2CemSupportKind.Unknown ? null : "synthetic.revA",
            Array.Empty<ushort>(), "Synthetic identity classification only");

    private static VolvoP2CemServiceResearchContext Unknown() =>
        new(2007, true, null, null, null,
            VolvoP2ServiceCredentialScope.Unspecified, false, null, null);

    private static VolvoP2CemServiceResearchContext FullHypothetical() =>
        new(2007, true, Identity(VolvoP2CemSupportKind.VerifiedIdentityReadOnly),
            "Synthetic mapping review 01", "Synthetic service review 01",
            VolvoP2ServiceCredentialScope.ConfigurationService, true,
            "Synthetic adapter bench record 01", "Synthetic recovery bench 01");
}
