namespace Dvinholm.App.Tests;

/// <summary>
/// Synthetic in-memory revision claims only; no car, keys, OEM mappings
/// or diagnostic operations are involved.
/// </summary>
public sealed class CemConfigurationRevisionResearchTriageTests
{
    private static CemConfigurationMappingResearchClaim Claim(
        int index = 1,
        CemConfigurationIndexedFormat format = CemConfigurationIndexedFormat.P2B9Fc,
        string part = "00000000",
        string revision = "SYNTHETIC-1") =>
        new(format, index, part, revision,
            "Synthetic unverified byte role",
            "https://example.invalid/synthetic-reference");

    private static CemConfigurationResearchCatalogDocument Catalog(
        params CemConfigurationMappingResearchClaim[] claims) =>
        new(CemConfigurationResearchCatalogImport.Format, 1, true, false,
            false, Array.AsReadOnly(claims));

    private static CemConfigurationOfflineComparisonResult Comparison(
        CemConfigurationIndexedFormat format = CemConfigurationIndexedFormat.P2B9Fc,
        params int[] ordinals) =>
        new(format, format == CemConfigurationIndexedFormat.P2B9Fc ? 140 : 252,
            new string('A', 64),
            ordinals.Length == 0 ? new string('A', 64) : new string('B', 64),
            Array.AsReadOnly(ordinals));

    private static CemConfigurationRevisionResearchAssessment Triage(
        CemConfigurationOfflineComparisonResult comparison,
        CemConfigurationResearchCatalogDocument catalog,
        string? part = "00000000", string? revision = "SYNTHETIC-1") =>
        CemConfigurationRevisionResearchTriage.Assess(comparison, catalog, part, revision);

    [Fact]
    public void ExactStringScopeRemainsUnverifiedNotAuthenticated()
    {
        var review = Triage(Comparison(ordinals: [1]), Catalog(Claim()));
        var row = Assert.Single(review.ChangedOrdinals);
        Assert.Equal(CemConfigurationRevisionResearchDisposition.ExactUnverifiedUserClaim,
            row.Disposition);
        Assert.False(row.HasVerifiedCemMeaning);
        Assert.False(review.HasAuthenticatedPhysicalCem);
        Assert.False(review.HasVerifiedSoftwareRevision);
        Assert.False(review.HasVerifiedParameterMeaning);
        Assert.False(review.IsRestorableBackup);
        Assert.False(review.MayReadLiveConfiguration);
        Assert.False(review.MayWriteConfiguration);
        Assert.False(review.MayAcquirePin);
    }

    [Fact]
    public void OnlyOtherSoftwareRevisionIsFlaggedNotMatched()
    {
        var review = Triage(Comparison(ordinals: [1]),
            Catalog(Claim(revision: "SYNTHETIC-2")));
        Assert.Equal(CemConfigurationRevisionResearchDisposition.OnlyOtherSoftwareRevisionClaim,
            review.ChangedOrdinals[0].Disposition);
    }

    [Fact]
    public void OtherHardwarePartIsSeparateFromSoftwareMismatch()
    {
        var review = Triage(Comparison(ordinals: [1]),
            Catalog(Claim(part: "00000001")));
        Assert.Equal(CemConfigurationRevisionResearchDisposition.OnlyOtherHardwarePartClaim,
            review.ChangedOrdinals[0].Disposition);
    }

    [Fact]
    public void P3ClaimNeverFillsAChangedP2Ordinal()
    {
        var review = Triage(Comparison(ordinals: [1]),
            Catalog(Claim(format: CemConfigurationIndexedFormat.P3F106)));
        Assert.Equal(CemConfigurationRevisionResearchDisposition.NoClaimForChangedOrdinal,
            review.ChangedOrdinals[0].Disposition);
    }

    [Fact]
    public void P3Index252IsReviewedWithoutInventingAnOption()
    {
        var review = Triage(
            Comparison(CemConfigurationIndexedFormat.P3F106, 252),
            Catalog(Claim(index: 252, format: CemConfigurationIndexedFormat.P3F106)));
        Assert.Equal(CemConfigurationIndexedFormat.P3F106, review.Format);
        Assert.Equal(252, review.TotalValueCount);
        Assert.Equal(252, review.ChangedOrdinals[0].Index1Based);
        Assert.Equal(CemConfigurationRevisionResearchDisposition.ExactUnverifiedUserClaim,
            review.ChangedOrdinals[0].Disposition);
    }

    [Fact]
    public void MissingOrInvalidUserScopeNeverElevatesAClaim()
    {
        var missing = Triage(Comparison(ordinals: [1]), Catalog(Claim()),
            part: null);
        Assert.Equal(
            CemConfigurationRevisionResearchDisposition.AssertedScopeMissingOrInvalid,
            missing.ChangedOrdinals[0].Disposition);
        var malformed = Triage(Comparison(ordinals: [1]), Catalog(Claim()),
            revision: "bad revision\n");
        Assert.Equal(
            CemConfigurationRevisionResearchDisposition.AssertedScopeMissingOrInvalid,
            malformed.ChangedOrdinals[0].Disposition);
    }

    [Fact]
    public void MoreSpecificClaimsWinWithoutPromotingToVerified()
    {
        var review = Triage(Comparison(ordinals: [1, 3, 5, 140]),
            Catalog(
                Claim(index: 1, revision: "SYNTHETIC-2"),
                Claim(index: 1, part: "00000001"),
                Claim(index: 1),
                Claim(index: 3, revision: "SYNTHETIC-2"),
                Claim(index: 3, part: "00000001"),
                Claim(index: 5, part: "00000001")));
        Assert.Equal(new[]
        {
            CemConfigurationRevisionResearchDisposition.ExactUnverifiedUserClaim,
            CemConfigurationRevisionResearchDisposition.OnlyOtherSoftwareRevisionClaim,
            CemConfigurationRevisionResearchDisposition.OnlyOtherHardwarePartClaim,
            CemConfigurationRevisionResearchDisposition.NoClaimForChangedOrdinal
        }, review.ChangedOrdinals.Select(item => item.Disposition));
    }

    [Fact]
    public void EmptyComparisonHasNoClaimMatchesEvenWhenCatalogContainsRows()
    {
        var review = Triage(Comparison(), Catalog(Claim()));
        Assert.Empty(review.ChangedOrdinals);
        Assert.Equal(0, review.ChangedCount);
        Assert.Contains("Changed raw ordinals: 0", review.CreateRedactedReport(true));
    }

    [Fact]
    public void InvalidWholeCatalogFailsEvenWhenRelevantOrdinalIsPresent()
    {
        var forged = Catalog(Claim(), Claim());
        Assert.Throws<InvalidDataException>(() =>
            Triage(Comparison(ordinals: [1]), forged));
        var unsafeDocument = Catalog(Claim()) with { MayAccessEcu = true };
        Assert.Throws<InvalidDataException>(() =>
            Triage(Comparison(ordinals: [1]), unsafeDocument));
        var badMeaning = Catalog(Claim() with { ClaimedMeaning = "bad\ntext" });
        Assert.Throws<InvalidDataException>(() =>
            Triage(Comparison(ordinals: [1]), badMeaning));
    }

    [Fact]
    public void InvalidOrInconsistentComparisonStructureFailsClosed()
    {
        var claims = Catalog(Claim());
        var invalidFormat = Comparison((CemConfigurationIndexedFormat)999, 1);
        Assert.Throws<InvalidDataException>(() => Triage(invalidFormat, claims));

        var invalidCount = new CemConfigurationOfflineComparisonResult(
            CemConfigurationIndexedFormat.P2B9Fc, 252,
            new string('A', 64), new string('B', 64), [1]);
        Assert.Throws<InvalidDataException>(() => Triage(invalidCount, claims));

        var duplicate = Comparison(ordinals: [1, 1]);
        Assert.Throws<InvalidDataException>(() => Triage(duplicate, claims));
        var reversed = Comparison(ordinals: [140, 1]);
        Assert.Throws<InvalidDataException>(() => Triage(reversed, claims));
        var outOfRange = Comparison(ordinals: [141]);
        Assert.Throws<InvalidDataException>(() => Triage(outOfRange, claims));

        var mismatchedHash = new CemConfigurationOfflineComparisonResult(
            CemConfigurationIndexedFormat.P2B9Fc, 140,
            new string('a', 64), new string('B', 64), [1]);
        Assert.Throws<InvalidDataException>(() => Triage(mismatchedHash, claims));

        var impossibleNoDiff = new CemConfigurationOfflineComparisonResult(
            CemConfigurationIndexedFormat.P2B9Fc, 140,
            new string('A', 64), new string('B', 64), []);
        Assert.Throws<InvalidDataException>(() => Triage(impossibleNoDiff, claims));
    }

    [Fact]
    public void FullP2ChangedIndexCountRespectsBoundedResearchClaims()
    {
        var review = Triage(Comparison(ordinals: Enumerable.Range(1, 140).ToArray()),
            Catalog(Claim()));
        Assert.Equal(140, review.ChangedCount);
        Assert.Equal(
            CemConfigurationRevisionResearchDisposition.NoClaimForChangedOrdinal,
            review.ChangedOrdinals[^1].Disposition);
        var rows = Assert.IsAssignableFrom<IList<CemConfigurationRevisionResearchRow>>(
            review.ChangedOrdinals);
        Assert.Throws<NotSupportedException>(() =>
            rows[0] = new CemConfigurationRevisionResearchRow(1,
                CemConfigurationRevisionResearchDisposition.NoClaimForChangedOrdinal));
    }

    [Fact]
    public void RussianAndEnglishReportsOmitAllPrivateClaimTextAndHashes()
    {
        var review = Triage(Comparison(ordinals: [1, 140]),
            Catalog(Claim(), Claim(index: 140, revision: "SYNTHETIC-2")));
        var en = review.CreateRedactedReport(true);
        var ru = review.CreateRedactedReport(false);
        Assert.Contains("Ordinal 1: exact user claim scope (UNVERIFIED)", en);
        Assert.Contains("Ordinal 140: claim only for a different claimed software revision", en);
        Assert.Contains("Индекс 1: точное совпадение заявления (НЕ ПОДТВЕРЖДЕНО)", ru);
        Assert.Contains("Индекс 140: запись лишь для другой заявленной версии ПО", ru);
        foreach (var report in new[] { en, ru })
        {
            Assert.DoesNotContain("00000000", report);
            Assert.DoesNotContain("SYNTHETIC-1", report);
            Assert.DoesNotContain("SYNTHETIC-2", report);
            Assert.DoesNotContain("https://example.invalid", report);
            Assert.DoesNotContain("Synthetic unverified byte role", report);
            Assert.DoesNotContain(new string('A', 64), report);
            Assert.DoesNotContain(new string('B', 64), report);
        }
    }

    [Fact]
    public void NoHardwareOrDiagnosticAuthorizationWasIntroduced()
    {
        var code = Source("src/Dvinholm.App/CemConfigurationRevisionResearchTriage.cs");
        Assert.Contains("CemConfigurationMappingResearch.ValidPartNumber", code);
        Assert.Contains("CemConfigurationMappingResearch.Validate(catalog.Claims)", code);
        Assert.Contains("NoClaimForChangedOrdinal", code);
        foreach (var forbidden in new[]
        {
            "PassThru", "J2534Session", "UdsClient", "SendAsync(",
            "ReadCemPin(", "SecurityAccess(", "WriteConfiguration(",
            "File.WriteAllBytes(", "FileMode.Create", "FileAccess.Write"
        })
            Assert.DoesNotContain(forbidden, code);
        Assert.Empty(CemConfigurationMappingResearch.ProductionMappings);
        Assert.False(new VolvoP2CemConfigurationReadPreflightAssessment(
            VolvoP2CemSourceSetKind.NoInScopeClaims,
            Array.Empty<VolvoP2ServiceEvidenceGap>(), false, false)
            .MayReadLiveCemConfiguration);
    }

    private static string Source(string relative)
    {
        for (DirectoryInfo? current = new(AppContext.BaseDirectory);
             current is not null; current = current.Parent)
        {
            var path = Path.Combine(current.FullName,
                relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path))
                return File.ReadAllText(path);
        }
        throw new FileNotFoundException(relative);
    }
}
