namespace Dvinholm.App.Tests;

/// <summary>
/// Eight synthetic CEM variant research gates. These never use a CAN adapter,
/// PIN, vehicle, real module fingerprint or executable read service.
/// </summary>
public sealed class VolvoP2CemVariantPreReadGateTests
{
    private static VolvoP2CemIdentityResearchDocument Card() =>
        new(VolvoP2CemIdentityResearchExport.Format, 1, true, false, false,
            2007, "HW-001", "SW-001", "DIAG-001", true);

    private static VolvoP2CemRevisionSourceClaim Claim(string source = "one") =>
        new(VolvoP2CemRevisionSourceComparison.Format, 1, true, false, false,
            2007, 2008, "HW-001", "SW-001", "DIAG-001",
            "PRIVATE-CITATION-" + source);

    private static VolvoP2CemVariantPreReadAssessment Review(
        params VolvoP2CemRevisionSourceClaim[] claims) =>
        VolvoP2CemVariantPreReadGate.Assess(Card(), claims);

    private static VolvoP2CemPreReadFinding Finding(
        VolvoP2CemVariantPreReadAssessment review,
        VolvoP2CemPreReadRequirement requirement) =>
        Assert.Single(review.Checks, x => x.Requirement == requirement).Finding;

    private static void AssertFullyBlocked(VolvoP2CemVariantPreReadAssessment review)
    {
        Assert.False(review.MaySendDiagnosticRequest);
        Assert.False(review.MayReadLiveConfiguration);
        Assert.False(review.MayAcquirePin);
        Assert.False(review.MayWriteConfiguration);
        Assert.Equal(7, review.Checks.Count);
        Assert.Equal(7, review.Checks.Select(x => x.Requirement).Distinct().Count());
        Assert.Equal(VolvoP2CemPreReadFinding.NotImplemented,
            Finding(review, VolvoP2CemPreReadRequirement.ReviewedCemReadImplementation));
    }

    [Fact]
    public void OneTextualRevisionMatchDoesNotVerifyPhysicalCem()
    {
        var result = Review(Claim());
        Assert.Equal(VolvoP2CemSourceSetKind.OneMatchingUnverifiedClaim, result.SourceKind);
        Assert.Equal(VolvoP2CemPreReadFinding.UnverifiedClaimOnly,
            Finding(result, VolvoP2CemPreReadRequirement.HardwareSoftwareClaimConsistency));
        AssertFullyBlocked(result);
    }

    [Fact]
    public void MultipleMatchingFilesStillCannotVerifyTransportOrReadMapping()
    {
        var result = Review(Claim("first"), Claim("second"));
        Assert.Equal(VolvoP2CemSourceSetKind.MultipleMatchingUnverifiedClaims, result.SourceKind);
        foreach (var requirement in new[]
        {
            VolvoP2CemPreReadRequirement.IndependentlyAuthenticatedCemIdentity,
            VolvoP2CemPreReadRequirement.VerifiedDiagnosticTransportAndAddresses,
            VolvoP2CemPreReadRequirement.RevisionSpecificConfigurationReadSemantics,
            VolvoP2CemPreReadRequirement.PhysicalReadOnlyJ2534Acceptance
        })
            Assert.Equal(VolvoP2CemPreReadFinding.IndependentlyVerifiedEvidenceMissing,
                Finding(result, requirement));
        AssertFullyBlocked(result);
    }

    [Fact]
    public void ContradictoryInScopeSoftwareClaimsRequireInvestigation()
    {
        var result = Review(Claim(), Claim("other") with
        {
            ClaimedSoftwarePartNumber = "SW-OTHER"
        });
        Assert.Equal(VolvoP2CemSourceSetKind.ConflictingInScopeClaims, result.SourceKind);
        Assert.Equal(VolvoP2CemPreReadFinding.ConflictingSourceClaims,
            Finding(result, VolvoP2CemPreReadRequirement.HardwareSoftwareClaimConsistency));
        Assert.Contains("Resolve contradictory", result.NextResearchAction);
        AssertFullyBlocked(result);
    }

    [Fact]
    public void RepeatedCaseInsensitiveSourceReferencesAreNotIndependent()
    {
        var result = Review(Claim("same"), Claim("SAME"));
        Assert.Equal(VolvoP2CemSourceSetKind.DuplicateSourceReference, result.SourceKind);
        Assert.Equal(VolvoP2CemPreReadFinding.ConflictingSourceClaims,
            Finding(result, VolvoP2CemPreReadRequirement.HardwareSoftwareClaimConsistency));
        Assert.Contains("Replace repeated", result.NextResearchAction);
        AssertFullyBlocked(result);
    }

    [Fact]
    public void OutOfScopeYearDoesNotBecomeARevisionCompatibilityClaim()
    {
        var claim = Claim() with { ModelYearFrom = 2010, ModelYearThrough = 2014 };
        var result = Review(claim);
        Assert.Equal(VolvoP2CemSourceSetKind.NoInScopeClaims, result.SourceKind);
        Assert.Equal(VolvoP2CemPreReadFinding.SourceEvidenceMissing,
            Finding(result, VolvoP2CemPreReadRequirement.ModelYearSourceScope));
        Assert.Equal(VolvoP2CemPreReadFinding.SourceEvidenceMissing,
            Finding(result, VolvoP2CemPreReadRequirement.HardwareSoftwareClaimConsistency));
        Assert.Contains("claimed model year", result.NextResearchAction);
        AssertFullyBlocked(result);
    }

    [Fact]
    public void InvalidUntrustedClaimsAndOversizedListsAreRejected()
    {
        Assert.Throws<InvalidDataException>(() => Review(Claim() with { MayAccessEcu = true }));
        Assert.Throws<InvalidDataException>(() => Review(Claim() with { IsRestorableBackup = true }));
        Assert.Throws<InvalidDataException>(() => Review());
        Assert.Throws<InvalidDataException>(() =>
            VolvoP2CemVariantPreReadGate.Assess(Card(),
                Enumerable.Range(0, 17).Select(i => Claim(i.ToString()))));
        Assert.Throws<ArgumentNullException>(() =>
            VolvoP2CemVariantPreReadGate.Assess(Card(), null!));
    }

    [Fact]
    public void ReportRedactsAllClaimedIdentifiersAndIsPresentInExistingUi()
    {
        var report = VolvoP2CemVariantPreReadGate.BuildReadOnlyReport(
            Card(), [Claim("VISIBLE-PRIVATE")]);
        Assert.Contains("PRE-READ CHECK (OFFLINE)", report);
        Assert.Contains("BLOCKED", report);
        Assert.DoesNotContain("HW-001", report);
        Assert.DoesNotContain("SW-001", report);
        Assert.DoesNotContain("DIAG-001", report);
        Assert.DoesNotContain("PRIVATE-CITATION", report);
        Assert.DoesNotContain("7E0", report);
        Assert.DoesNotContain("0x27", report);
        Assert.True(report.Length < 2100);
        var ui = ReadUi();
        Assert.Contains("VolvoP2CemVariantPreReadGate", ui);
        Assert.Contains(".BuildReadOnlyReport(manual, claims)", ui);
    }

    [Fact]
    public void GateResultsCannotBeEditedToEnableOperations()
    {
        var result = Review(Claim());
        var rows = Assert.IsAssignableFrom<IList<VolvoP2CemPreReadCheck>>(result.Checks);
        Assert.Throws<NotSupportedException>(() =>
            rows[0] = new(VolvoP2CemPreReadRequirement.ReviewedCemReadImplementation,
                VolvoP2CemPreReadFinding.UnverifiedClaimOnly));
        Assert.Empty(VolvoP2CemSupportMatrix.ProductionEntries);
        Assert.Empty(VolvoP2CemParameterEvidenceCatalog.ProductionRecords);
        AssertFullyBlocked(result);
    }

    private static string ReadUi()
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
