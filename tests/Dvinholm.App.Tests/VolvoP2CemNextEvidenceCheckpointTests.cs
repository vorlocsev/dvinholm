namespace Dvinholm.App.Tests;

/// <summary>
/// Eight deterministic offline acceptance gates for a single prioritized
/// XC90 P2/CEM evidence checkpoint. No J2534, PIN or ECU service is used.
/// </summary>
public sealed class VolvoP2CemNextEvidenceCheckpointTests
{
    private static VolvoP2CemNextEvidenceCheckpoint Plan(
        int? year = 2009, bool checkedYear = true,
        VolvoP2NetworkKind network = VolvoP2NetworkKind.HighSpeedCan,
        uint? rate = 500_000,
        VolvoP2CemDiagnosticInterface adapter = VolvoP2CemDiagnosticInterface.DiCE) =>
        VolvoP2CemNextEvidencePlanner.Plan(year, checkedYear, network, rate, adapter);

    [Fact]
    public void MissingOrUncheckedYearRemainsFirstEvidenceGap()
    {
        Assert.Equal(VolvoP2CemNextEvidenceKind.VerifyModelYear, Plan(year: null).Kind);
        Assert.Equal(VolvoP2CemNextEvidenceKind.VerifyModelYear,
            Plan(checkedYear: false, adapter: VolvoP2CemDiagnosticInterface.J2534_1).Kind);
    }

    [Fact]
    public void OutOfScopeYearStopsBeforeTransportOrDeviceRecommendations()
    {
        foreach (int year in new[] { 2002, 2015 })
        {
            var step = Plan(year: year);
            Assert.Equal(VolvoP2CemNextEvidenceKind.OutsideFirstGenerationScope, step.Kind);
            Assert.Contains("STOP", step.Action);
        }
    }

    [Fact]
    public void J2534OneIsNotPromotedToVidaDiagnosticReadiness()
    {
        var step = Plan(adapter: VolvoP2CemDiagnosticInterface.J2534_1,
            network: VolvoP2NetworkKind.Unknown);
        Assert.Equal(VolvoP2CemNextEvidenceKind.ResolveUnsupportedVidaInterface, step.Kind);
        Assert.Contains("not a VIDA diagnostics interface", step.Action);
        Assert.False(step.CanOpenCemSession);
    }

    [Fact]
    public void UnidentifiedNetworkNeverSelectsBusSpeedOrCemAddress()
    {
        var step = Plan(network: VolvoP2NetworkKind.Unknown);
        Assert.Equal(VolvoP2CemNextEvidenceKind.IdentifyPhysicalNetwork, step.Kind);
        Assert.DoesNotContain("500000", step.Action);
        Assert.DoesNotContain("7E0", step.Action);
    }

    [Fact]
    public void LowSpeedAndKLineDoNotInheritHistoricalHsCanRate()
    {
        foreach (var network in new[] {
            VolvoP2NetworkKind.LowSpeedCan, VolvoP2NetworkKind.OtherOrKLine })
        {
            var step = Plan(network: network);
            Assert.Equal(VolvoP2CemNextEvidenceKind.DocumentOtherNetwork, step.Kind);
            Assert.Contains("non-HS-CAN", step.Action);
        }
    }

    [Fact]
    public void MissingOrConflictingHsCanSpeedProducesDistinctCheckpoints()
    {
        Assert.Equal(VolvoP2CemNextEvidenceKind.SpecifyHistoricalHsCanRate,
            Plan(rate: null).Kind);
        Assert.Equal(VolvoP2CemNextEvidenceKind.SpecifyHistoricalHsCanRate,
            Plan(rate: 0).Kind);
        Assert.Equal(VolvoP2CemNextEvidenceKind.ResolveHsCanRateConflict,
            Plan(year: 2004, rate: 500_000).Kind);
        Assert.Equal(VolvoP2CemNextEvidenceKind.CorroborateCemRevision,
            Plan(year: 2004, rate: 250_000).Kind);
    }

    [Fact]
    public void DiceAndOtherInterfacesHaveDifferentUnverifiedNextSteps()
    {
        Assert.Equal(VolvoP2CemNextEvidenceKind.CorroborateCemRevision,
            Plan().Kind);
        foreach (var adapter in new[] {
            VolvoP2CemDiagnosticInterface.J2534_2,
            VolvoP2CemDiagnosticInterface.Unspecified,
            VolvoP2CemDiagnosticInterface.Other })
        {
            var step = Plan(adapter: adapter);
            Assert.Equal(VolvoP2CemNextEvidenceKind.QualifyDiagnosticInterface, step.Kind);
            Assert.Contains("physical CEM diagnostic support", step.Action);
        }
    }

    [Fact]
    public void NoPlanGrantsAccessAndExistingOfflineReportStaysRedacted()
    {
        foreach (var step in new[] {
            Plan(), Plan(year: null),
            Plan(network: VolvoP2NetworkKind.Unknown),
            Plan(adapter: VolvoP2CemDiagnosticInterface.J2534_1),
            Plan(adapter: VolvoP2CemDiagnosticInterface.J2534_2),
            Plan(year: 2016),
            Plan(year: 2004, rate: 500_000) })
        {
            Assert.False(step.CanOpenCemSession);
            Assert.False(step.CanReadConfiguration);
            Assert.False(step.CanAcquirePin);
            Assert.False(step.CanWriteConfiguration);
            Assert.True(step.Action.Length < 180);
        }

        var report = VolvoP2CemReadinessMatrix.BuildOfflineReport(
            2009, true, VolvoP2NetworkKind.HighSpeedCan, 500_000,
            VolvoP2CemDiagnosticInterface.DiCE);
        Assert.Contains("Next evidence checkpoint (OFFLINE ONLY): CorroborateCemRevision", report);
        Assert.Contains("does not authenticate a physical CEM", report);
        Assert.Contains("BLOCKED: CEM configuration reads", report);
        Assert.DoesNotContain("7E0", report);
        Assert.DoesNotContain("VIN:", report);
        Assert.DoesNotContain("PIN:", report);
        Assert.True(report.Length < 5000);
    }
}
