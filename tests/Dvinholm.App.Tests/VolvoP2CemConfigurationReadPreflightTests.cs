namespace Dvinholm.App.Tests;

/// <summary>
/// Eight deterministic synthetic tests. Real CEM evidence and J2534 are absent.
/// </summary>
public sealed class VolvoP2CemConfigurationReadPreflightTests
{
    private static VolvoP2CemIdentityResearchDocument Card() =>
        new(VolvoP2CemIdentityResearchExport.Format, 1, true, false, false,
            2007, "HW-001", "SW-001", "DIAG-001", true);

    private static VolvoP2CemRevisionSourceClaim Claim(string source = "first") =>
        new(VolvoP2CemRevisionSourceComparison.Format, 1, true, false, false,
            2007, 2008, "HW-001", "SW-001", "DIAG-001",
            "Synthetic reference " + source);

    private static VolvoP2CemConfigurationReadPreflightAssessment Review(
        params VolvoP2CemRevisionSourceClaim[] claims) =>
        VolvoP2CemConfigurationReadPreflight.Assess(Card(), claims);

    [Fact]
    public void OneMatchingClaimNeverPromotesReadOrAuthentication()
    {
        var review = Review(Claim());
        Assert.Equal(VolvoP2CemSourceSetKind.OneMatchingUnverifiedClaim, review.SourceSetKind);
        Assert.False(review.HasAuthenticatedPhysicalCem);
        Assert.False(review.HasVerifiedReadMechanism);
        Assert.False(review.MayBuildDiagnosticRequest);
        Assert.False(review.MayReadLiveCemConfiguration);
        Assert.False(review.MayAcquirePin);
        Assert.False(review.MayWriteConfiguration);
    }

    [Fact]
    public void EvenTwoMatchingClaimsLeaveAllFiveReadRequirementsOpen()
    {
        var review = Review(Claim("one"), Claim("two"));
        Assert.Equal(VolvoP2CemSourceSetKind.MultipleMatchingUnverifiedClaims, review.SourceSetKind);
        Assert.Equal(new[]
        {
            VolvoP2ServiceEvidenceGap.ConfirmedXc90P2ModelYear,
            VolvoP2ServiceEvidenceGap.ExactCemHardwareSoftwareIdentity,
            VolvoP2ServiceEvidenceGap.IndependentlyDocumentedConfigurationMapping,
            VolvoP2ServiceEvidenceGap.PhysicalJ2534CemAcceptance,
            VolvoP2ServiceEvidenceGap.OperationalServiceNotImplemented
        }, review.BlockingRequirements);
    }

    [Fact]
    public void ContradictorySoftwarePartNumberIsExplicitlyFlagged()
    {
        var review = Review(Claim(), Claim("two") with
        {
            ClaimedSoftwarePartNumber = "SW-002"
        });
        Assert.True(review.SourceClaimsAreConflicting);
        Assert.False(review.SourceClaimsAreDuplicated);
        Assert.Equal(VolvoP2CemSourceSetKind.ConflictingInScopeClaims, review.SourceSetKind);
        Assert.False(review.MayReadLiveCemConfiguration);
    }

    [Fact]
    public void RepeatedCitationLabelDoesNotEstablishIndependentCorroboration()
    {
        var review = Review(Claim("First"), Claim("FIRST"));
        Assert.True(review.SourceClaimsAreDuplicated);
        Assert.Equal(VolvoP2CemSourceSetKind.DuplicateSourceReference, review.SourceSetKind);
        Assert.False(review.MayBuildDiagnosticRequest);
    }

    [Fact]
    public void OutOfScopeClaimsDoNotVerifyCemOrReadProcedure()
    {
        var review = Review(Claim() with { ModelYearFrom = 2010, ModelYearThrough = 2014 });
        Assert.Equal(VolvoP2CemSourceSetKind.NoInScopeClaims, review.SourceSetKind);
        Assert.Contains(VolvoP2ServiceEvidenceGap.ExactCemHardwareSoftwareIdentity,
            review.BlockingRequirements);
        Assert.False(review.HasVerifiedReadMechanism);
    }

    [Fact]
    public void ForgedCapabilitiesOrInvalidCardAreRejected()
    {
        Assert.Throws<InvalidDataException>(() => Review(Claim() with { MayAccessEcu = true }));
        Assert.Throws<InvalidDataException>(() =>
            VolvoP2CemConfigurationReadPreflight.Assess(
                Card() with { IsRestorableBackup = true }, [Claim()]));
        Assert.Throws<InvalidDataException>(() => Review());
        Assert.Throws<ArgumentNullException>(() =>
            VolvoP2CemConfigurationReadPreflight.Assess(Card(), null!));
    }

    [Fact]
    public void ReadRequirementsAreImmutableAndCannotBeEnrolled()
    {
        var review = Review(Claim());
        var gaps = Assert.IsAssignableFrom<IList<VolvoP2ServiceEvidenceGap>>(review.BlockingRequirements);
        Assert.Throws<NotSupportedException>(() =>
            gaps[0] = VolvoP2ServiceEvidenceGap.OperationalServiceNotImplemented);
        Assert.Empty(VolvoP2CemSupportMatrix.ProductionEntries);
        Assert.Empty(VolvoP2CemParameterEvidenceCatalog.ProductionRecords);
        Assert.False(review.MayWriteConfiguration);
    }

    [Fact]
    public void RedactedReportIsBoundedAndConnectedToExistingOfflineUi()
    {
        var report = VolvoP2CemConfigurationReadPreflight.BuildReadOnlyReport(Card(), [
            Claim("PRIVATE-CITATION")
        ]);
        Assert.Contains("MISSING", report);
        Assert.Contains("DISABLED", report);
        Assert.Contains("not implemented", report);
        Assert.DoesNotContain("HW-001", report);
        Assert.DoesNotContain("SW-001", report);
        Assert.DoesNotContain("PRIVATE-CITATION", report);
        Assert.DoesNotContain("7E0", report);
        Assert.True(report.Length < 3000);

        var ui = FindUi();
        Assert.Contains("VolvoP2CemConfigurationReadPreflight", ui);
        Assert.Contains(".BuildReadOnlyReport(manual, claims)", ui);
    }

    private static string FindUi()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
        {
            var file = Path.Combine(dir.FullName, "src", "Dvinholm.App", "MainWindow.xaml.cs");
            if (File.Exists(file)) return File.ReadAllText(file);
        }
        throw new FileNotFoundException("MainWindow.xaml.cs");
    }
}
