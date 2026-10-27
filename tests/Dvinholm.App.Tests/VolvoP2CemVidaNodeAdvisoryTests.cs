namespace Dvinholm.App.Tests;

/// <summary>
/// Eight deterministic checks for source-limited logical VIDA node
/// interpretation. NO CAN requests, DiCE/J2534 hardware or CEM access.
/// </summary>
public sealed class VolvoP2CemVidaNodeAdvisoryTests
{
    [Fact]
    public void NoIndependentModelYearMeansNoNodeOrLiveCapability()
    {
        foreach (var value in new (int?, bool)[]
        {
            (null, true), (2007, false), (null, false)
        })
        {
            var result = Assess(value.Item1, value.Item2);
            Assert.Equal(VolvoP2CemVidaNodeAssessmentKind.ModelYearNotIndependentlyConfirmed,
                result.Kind);
            Assert.Null(result.ReportedWorkshopNode);
            Denied(result);
        }
    }

    [Fact]
    public void OutsideDocumentedScopeCannotSelectNode()
    {
        foreach (var year in new[] { 1999, 2002, 2015, 2026 })
        {
            var result = Assess(year);
            Assert.Equal(VolvoP2CemVidaNodeAssessmentKind.OutsideDocumentedXc90P2Scope,
                result.Kind);
            Assert.Null(result.ReportedWorkshopNode);
            Denied(result);
        }
    }

    [Fact]
    public void EarlyP2WorkshopReferenceIsText40NotCanId()
    {
        foreach (var year in new[] { 2003, 2004 })
        {
            var result = Assess(year);
            Assert.Equal(VolvoP2CemVidaNodeAssessmentKind.WorkshopLogicalNodeReferenceOnly,
                result.Kind);
            Assert.Equal("40", result.ReportedWorkshopNode);
            Assert.Contains("NOT a CAN ID", result.Explanation);
            Denied(result);
        }
    }

    [Fact]
    public void LaterP2WorkshopReferenceIsText50NotCanId()
    {
        foreach (var year in new[] { 2005, 2007, 2014 })
        {
            var result = Assess(year);
            Assert.Equal(VolvoP2CemVidaNodeAssessmentKind.WorkshopLogicalNodeReferenceOnly,
                result.Kind);
            Assert.Equal("50", result.ReportedWorkshopNode);
            Assert.Contains("not a CAN ID", result.Explanation,
                StringComparison.OrdinalIgnoreCase);
            Denied(result);
        }
    }

    [Fact]
    public void J25341IsExplicitlyNotSupportedForVidaDiagnostics()
    {
        foreach (var year in new int?[] { null, 2004, 2007, 2015 })
        {
            var result = VolvoP2CemVidaNodeAdvisory.Assess(year,
                modelYearIndependentlyConfirmed: year is not null,
                VolvoP2CemDiagnosticInterface.J2534_1);
            Assert.True(result.SelectedInterfaceCannotDoVidaDiagnostics);
            Denied(result);
        }
        Assert.Contains("cannot be used for VIDA diagnostics",
            Assess(2007, VolvoP2CemDiagnosticInterface.J2534_1).Explanation,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DiceRecommendationDoesNotProveCemCompatibility()
    {
        var result = Assess(2007, VolvoP2CemDiagnosticInterface.DiCE);
        Assert.Contains("DiCE", result.Explanation);
        Assert.Contains("not verified", result.Explanation);
        Assert.False(result.SelectedInterfaceCannotDoVidaDiagnostics);
        Denied(result);
    }

    [Fact]
    public void J25342OrOtherDeviceCannotInferCemDiagnostics()
    {
        foreach (var tool in new[]
        {
            VolvoP2CemDiagnosticInterface.J2534_2,
            VolvoP2CemDiagnosticInterface.Unspecified,
            VolvoP2CemDiagnosticInterface.Other
        })
        {
            var result = Assess(2007, tool);
            Assert.Equal("50", result.ReportedWorkshopNode);
            Assert.False(result.SelectedInterfaceCannotDoVidaDiagnostics);
            Denied(result);
        }
    }

    [Fact]
    public void CandidateViewerDisplaysEvidenceWithoutTreatingNodesAsCommands()
    {
        var advisory = VolvoP2CemVidaNodeAdvisory.BuildOfflineOverview();
        var screen = VolvoP2ConfigCandidateDisplay.BuildOverview();
        Assert.Contains(advisory, screen);
        Assert.Contains("third-party reproduction", advisory);
        Assert.Contains("2003-2004 = 40", advisory);
        Assert.Contains("2005-2014 = 50", advisory);
        Assert.Contains("not raw CAN arbitration IDs", advisory);
        Assert.Contains("J2534-1", advisory);
        Assert.Contains("CANNOT do VIDA diagnostics", advisory);
        Assert.Contains("No CEM connection", advisory);
        Denied(Assess(2007));
    }

    private static VolvoP2CemVidaNodeAssessment Assess(
        int? year, VolvoP2CemDiagnosticInterface tool =
            VolvoP2CemDiagnosticInterface.Unspecified) =>
        VolvoP2CemVidaNodeAdvisory.Assess(year, year is not null, tool);

    private static void Denied(VolvoP2CemVidaNodeAssessment result)
    {
        Assert.Null(result.VerifiedRequestCanId);
        Assert.Null(result.VerifiedResponseCanId);
        Assert.Null(result.VerifiedDiagnosticDid);
        Assert.False(result.MayOpenCemSession);
        Assert.False(result.MayReadCemIdentity);
        Assert.False(result.MayReadOrWriteConfiguration);
        Assert.False(result.MayAcquirePin);
    }
}
