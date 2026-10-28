namespace Dvinholm.App.Tests;

/// <summary>
/// Eight synthetic checks of *research-only* transport warnings.
/// No physical adapter, vehicle, live CAN, session, PIN or configuration IO.
/// </summary>
public sealed class VolvoP2CemTransportResearchPreflightTests
{
    private static VolvoP2CemTransportResearchAssessment Check(
        int? year, bool claimedChecked, VolvoP2NetworkKind network,
        uint? speed, VolvoP2CemDiagnosticInterface adapter) =>
        VolvoP2CemTransportResearchPreflight.Assess(
            year, claimedChecked, network, speed, adapter);

    private static string Report(
        int? year, bool claimedChecked, VolvoP2NetworkKind network,
        uint? speed, VolvoP2CemDiagnosticInterface adapter) =>
        VolvoP2CemTransportResearchPreflight.BuildReadOnlyReport(
            year, claimedChecked, network, speed, adapter);

    [Fact]
    public void DocumentedEarlyXc90HsCanRateStaysResearchOnly()
    {
        foreach (var year in new[] { 2003, 2004 })
        {
            var r = Check(year, true, VolvoP2NetworkKind.HighSpeedCan,
                250_000, VolvoP2CemDiagnosticInterface.DiCE);
            Assert.Equal(VolvoP2CemTransportResearchKind.HistoricalHsCanRateMatchesOnly, r.Kind);
            Assert.Equal((uint)250_000, r.HistoricalHsCanRate);
            Assert.Equal("40", r.ReportedVidaLogicalNode);
            Assert.False(r.MaySendJ2534OrUds);
        }
    }

    [Fact]
    public void DocumentedLaterXc90HsCanRateNeverAuthenticatesCem()
    {
        foreach (var year in new[] { 2005, 2007, 2014 })
        {
            var r = Check(year, true, VolvoP2NetworkKind.HighSpeedCan,
                500_000, VolvoP2CemDiagnosticInterface.J2534_2);
            Assert.Equal(VolvoP2CemTransportResearchKind.HistoricalHsCanRateMatchesOnly, r.Kind);
            Assert.Equal((uint)500_000, r.HistoricalHsCanRate);
            Assert.Equal("50", r.ReportedVidaLogicalNode);
            Assert.False(r.HasVerifiedPhysicalTransport);
            Assert.False(r.MayReadConfiguration);
        }
    }

    [Fact]
    public void WrongRateIsExplicitStopEvenWithYearAndDiceClaims()
    {
        var r = Report(2007, true, VolvoP2NetworkKind.HighSpeedCan,
            250_000, VolvoP2CemDiagnosticInterface.DiCE);
        Assert.Contains("ConflictsWithPublishedHsCanGuidance", r);
        Assert.Contains("500 kbit/s", r);
        Assert.Contains("block communication", r);
        Assert.Contains("Do NOT auto-connect", r);
    }

    [Fact]
    public void UnconfirmedOrUnsupportedYearDoesNotInferSpeed()
    {
        var cases = new[]
        {
            Check(null, false, VolvoP2NetworkKind.HighSpeedCan, 500_000,
                VolvoP2CemDiagnosticInterface.DiCE),
            Check(2005, false, VolvoP2NetworkKind.HighSpeedCan, 500_000,
                VolvoP2CemDiagnosticInterface.DiCE),
            Check(2002, true, VolvoP2NetworkKind.HighSpeedCan, 250_000,
                VolvoP2CemDiagnosticInterface.DiCE),
            Check(2015, true, VolvoP2NetworkKind.HighSpeedCan, 500_000,
                VolvoP2CemDiagnosticInterface.DiCE)
        };
        foreach (var c in cases)
        {
            Assert.Null(c.HistoricalHsCanRate);
            Assert.False(c.MayAutoSelectAdapterSpeed);
            Assert.NotEqual(VolvoP2CemTransportResearchKind.HistoricalHsCanRateMatchesOnly, c.Kind);
        }
    }

    [Fact]
    public void DifferentOrUnknownNetworksNeverInheritHsCanSpeed()
    {
        foreach (var network in new[]
        {
            VolvoP2NetworkKind.Unknown, VolvoP2NetworkKind.LowSpeedCan,
            VolvoP2NetworkKind.OtherOrKLine
        })
        {
            var r = Check(2005, true, network, 500_000,
                VolvoP2CemDiagnosticInterface.DiCE);
            Assert.Null(r.HistoricalHsCanRate);
            Assert.False(r.MayOpenCemDiagnosticSession);
        }
        var text = Report(2005, true, VolvoP2NetworkKind.OtherOrKLine,
            500_000, VolvoP2CemDiagnosticInterface.DiCE);
        Assert.Contains("MS/LS-CAN and K-line", text);
    }

    [Fact]
    public void UnspecifiedBaudRateCannotProceedOrSelectItself()
    {
        foreach (var rate in new uint?[] { null, 0 })
        {
            var r = Check(2005, true, VolvoP2NetworkKind.HighSpeedCan,
                rate, VolvoP2CemDiagnosticInterface.DiCE);
            Assert.Equal(VolvoP2CemTransportResearchKind.BaudRateUnspecified, r.Kind);
            Assert.Equal((uint)500_000, r.HistoricalHsCanRate);
            Assert.False(r.MayAutoSelectAdapterSpeed);
        }
    }

    [Fact]
    public void J2534OneHasManufacturerSpecificDiagnosticWarning()
    {
        var j1 = Check(2005, true, VolvoP2NetworkKind.HighSpeedCan,
            500_000, VolvoP2CemDiagnosticInterface.J2534_1);
        Assert.True(j1.J2534OneUnsupportedForVidaDiagnostics);
        var report = Report(2005, true, VolvoP2NetworkKind.HighSpeedCan,
            500_000, VolvoP2CemDiagnosticInterface.J2534_1);
        Assert.Contains("STOP: Volvo states J2534-1 cannot be used for VIDA diagnostics.", report);
        Assert.Contains("NOT a CAN ID", report);
        var j2 = Check(2005, true, VolvoP2NetworkKind.HighSpeedCan,
            500_000, VolvoP2CemDiagnosticInterface.J2534_2);
        Assert.False(j2.J2534OneUnsupportedForVidaDiagnostics);
        Assert.False(j2.MayOpenCemDiagnosticSession);
    }

    [Fact]
    public void EveryClaimCombinationPermanentlyBlocksAllLiveServices()
    {
        foreach (var adapter in Enum.GetValues<VolvoP2CemDiagnosticInterface>())
        foreach (var network in Enum.GetValues<VolvoP2NetworkKind>())
        {
            var r = Check(2007, true, network, 500_000, adapter);
            Assert.False(r.HasAuthenticatedCemIdentity);
            Assert.False(r.HasVerifiedPhysicalTransport);
            Assert.False(r.MayAutoSelectAdapterSpeed);
            Assert.False(r.MayOpenCemDiagnosticSession);
            Assert.False(r.MaySendJ2534OrUds);
            Assert.False(r.MayReadConfiguration);
            Assert.False(r.MayAcquirePin);
            Assert.False(r.MayWriteConfiguration);
        }
        var text = Report(2007, true, VolvoP2NetworkKind.HighSpeedCan,
            500_000, VolvoP2CemDiagnosticInterface.DiCE);
        Assert.Contains("CEM configuration read/write, security/PIN", text);
        Assert.Contains("This preview changes no ECU", text);
    }
}
