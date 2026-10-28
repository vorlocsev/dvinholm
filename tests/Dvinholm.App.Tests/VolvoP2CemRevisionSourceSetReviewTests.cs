namespace Dvinholm.App.Tests;

/// <summary>Eight source-set tests with invented part numbers, no ECU/J2534.</summary>
public sealed class VolvoP2CemRevisionSourceSetReviewTests
{
    private static VolvoP2CemIdentityResearchDocument Card() =>
        new(VolvoP2CemIdentityResearchExport.Format, 1, true, false, false,
            2007, "HW-001", "SW-001", "DIAG-001", true);

    private static VolvoP2CemRevisionSourceClaim Claim(string suffix = "one") =>
        new(VolvoP2CemRevisionSourceComparison.Format, 1, true, false, false,
            2007, 2008, "HW-001", "SW-001", "DIAG-001",
            "Synthetic research reference " + suffix);

    [Fact]
    public void OneMatchingSourceStillDoesNotGrantAnyCapabilities()
    {
        var result = VolvoP2CemRevisionSourceSetReview.Assess(Card(), [Claim()]);
        Assert.Equal(VolvoP2CemSourceSetKind.OneMatchingUnverifiedClaim, result.Kind);
        Assert.Equal(1, result.YearScopedClaims);
        Assert.False(result.HasAuthenticatedCemIdentity);
        Assert.False(result.HasIndependentSourceAuthentication);
        Assert.False(result.HasVerifiedCemTransport);
        Assert.False(result.CanReadConfiguration);
        Assert.False(result.CanAcquirePin);
        Assert.False(result.CanWriteConfiguration);
    }

    [Fact]
    public void TwoMatchingDifferentLabelsAreOnlyConsistentUserClaims()
    {
        var result = VolvoP2CemRevisionSourceSetReview.Assess(
            Card(), [Claim("first"), Claim("second")]);
        Assert.Equal(VolvoP2CemSourceSetKind.MultipleMatchingUnverifiedClaims, result.Kind);
        Assert.Equal(2, result.MatchingClaims);
        Assert.False(result.HasIndependentSourceAuthentication);
    }

    [Fact]
    public void OneConflictingRevisionBlocksFirstMatchSelectionRegardlessOfOrder()
    {
        var good = Claim("good");
        var conflicting = Claim("other") with { ClaimedSoftwarePartNumber = "SW-002" };
        foreach (var sources in new[] { new[] { good, conflicting }, new[] { conflicting, good } })
        {
            var result = VolvoP2CemRevisionSourceSetReview.Assess(Card(), sources);
            Assert.Equal(VolvoP2CemSourceSetKind.ConflictingInScopeClaims, result.Kind);
            Assert.Equal(1, result.MatchingClaims);
            Assert.Equal(1, result.ContradictingClaims);
            Assert.False(result.CanReadConfiguration);
        }
    }

    [Fact]
    public void NoSourcesForClaimedYearIsNotAnUnsupportedVehicleVerdict()
    {
        var result = VolvoP2CemRevisionSourceSetReview.Assess(Card(), [
            Claim() with { ModelYearFrom = 2009, ModelYearThrough = 2010 }
        ]);
        Assert.Equal(VolvoP2CemSourceSetKind.NoInScopeClaims, result.Kind);
        Assert.Equal(0, result.YearScopedClaims);
        Assert.Equal(1, result.OutsideYearScopeClaims);
    }

    [Fact]
    public void OutOfScopeRecordDoesNotBecomeInScopeContradiction()
    {
        var result = VolvoP2CemRevisionSourceSetReview.Assess(Card(), [
            Claim("same"),
            Claim("other") with { ModelYearFrom = 2009, ModelYearThrough = 2011,
                ClaimedHardwarePartNumber = "HW-002" }
        ]);
        Assert.Equal(VolvoP2CemSourceSetKind.OneMatchingUnverifiedClaim, result.Kind);
        Assert.Equal(1, result.OutsideYearScopeClaims);
        Assert.Equal(0, result.ContradictingClaims);
    }

    [Fact]
    public void DuplicateSourceReferenceIsNotIndependentEvidence()
    {
        var result = VolvoP2CemRevisionSourceSetReview.Assess(Card(), [
            Claim("same"), Claim("SAME")
        ]);
        Assert.Equal(VolvoP2CemSourceSetKind.DuplicateSourceReference, result.Kind);
        Assert.False(result.HasIndependentSourceAuthentication);
    }

    [Fact]
    public void EmptyTooManyAndMalformedInputsFailClosedBeforeReport()
    {
        Assert.Throws<InvalidDataException>(() =>
            VolvoP2CemRevisionSourceSetReview.Assess(Card(), []));
        Assert.Throws<InvalidDataException>(() =>
            VolvoP2CemRevisionSourceSetReview.Assess(Card(),
                Enumerable.Range(0, 17).Select(n => Claim(n.ToString("D3")))));
        Assert.Throws<InvalidDataException>(() =>
            VolvoP2CemRevisionSourceSetReview.Assess(Card(), [
                Claim() with { MayAccessEcu = true }
            ]));
        Assert.Throws<InvalidDataException>(() =>
            VolvoP2CemRevisionSourceSetReview.Assess(
                Card() with { IsRestorableBackup = true }, [Claim()]));
    }

    [Fact]
    public void ReportNeverLeaksPartNumbersSourceTextOrPathsAndUiUsesMultiplePicker()
    {
        var report = VolvoP2CemRevisionSourceSetReview.BuildReadOnlyReport(
            Card(), [Claim("PRIVATE-SOURCE-EXAMPLE")]);
        Assert.Contains("UNVERIFIED", report);
        Assert.Contains("DISABLED", report);
        Assert.DoesNotContain("HW-001", report);
        Assert.DoesNotContain("SW-001", report);
        Assert.DoesNotContain("DIAG-001", report);
        Assert.DoesNotContain("PRIVATE-SOURCE-EXAMPLE", report);
        Assert.True(report.Length < 1700);
        var ui = FindUi();
        Assert.Contains("PickMultipleFilesAsync()", ui);
        Assert.Contains("VolvoP2CemRevisionSourceSetReview", ui);
        Assert.Contains(".BuildReadOnlyReport(manual, claims)", ui);
        Assert.Empty(VolvoP2CemSupportMatrix.ProductionEntries);
    }

    private static string FindUi()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, "src", "Dvinholm.App", "MainWindow.xaml.cs");
            if (File.Exists(path))
                return File.ReadAllText(path);
        }
        throw new FileNotFoundException("MainWindow.xaml.cs");
    }
}
