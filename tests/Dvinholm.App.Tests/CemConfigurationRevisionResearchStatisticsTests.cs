namespace Dvinholm.App.Tests;

/// <summary>
/// Synthetic, local-only research claim distribution. Exactly one category
/// per CHANGED byte ordinal, even when many claims mention that ordinal.
/// </summary>
public sealed class CemConfigurationRevisionResearchStatisticsTests
{
    private static CemConfigurationRevisionResearchRow Row(
        int index, CemConfigurationRevisionResearchDisposition status) =>
        new(index, status);

    private static CemConfigurationRevisionResearchAssessment Assessment(
        params CemConfigurationRevisionResearchRow[] rows) =>
        new(CemConfigurationIndexedFormat.P2B9Fc, 140, rows);

    private static CemConfigurationMappingResearchClaim Claim(
        int index, string part = "00000000",
        string revision = "SYNTHETIC-1") =>
        new(CemConfigurationIndexedFormat.P2B9Fc, index, part,
            revision, "Synthetic unverified claim",
            "https://example.invalid/research-synthetic");

    private static CemConfigurationResearchCatalogDocument Catalog(
        params CemConfigurationMappingResearchClaim[] rows) =>
        new(CemConfigurationResearchCatalogImport.Format, 1,
            true, false, false, Array.AsReadOnly(rows));

    private static CemConfigurationOfflineComparisonResult Changed(
        params int[] indexes) =>
        new(CemConfigurationIndexedFormat.P2B9Fc, 140,
            new string('A', 64), indexes.Length == 0
                ? new string('A', 64) : new string('B', 64), indexes);

    [Fact]
    public void MixedChangedIndicesSumToTotalWithoutDoubleCounting()
    {
        var d = CemConfigurationRevisionResearchDisposition.ExactUnverifiedUserClaim;
        var sw = CemConfigurationRevisionResearchDisposition.OnlyOtherSoftwareRevisionClaim;
        var hw = CemConfigurationRevisionResearchDisposition.OnlyOtherHardwarePartClaim;
        var none = CemConfigurationRevisionResearchDisposition.NoClaimForChangedOrdinal;
        var invalid = CemConfigurationRevisionResearchDisposition.AssertedScopeMissingOrInvalid;
        var assessment = Assessment(
            Row(1, d), Row(2, d), Row(3, sw),
            Row(4, hw), Row(5, hw), Row(6, none), Row(7, invalid));
        var stats = assessment.Statistics;
        Assert.Equal(7, stats.ChangedOrdinalCount);
        Assert.Equal(assessment.ChangedCount, stats.ChangedOrdinalCount);
        Assert.Equal(2, stats.ExactUnverifiedClaimCount);
        Assert.Equal(1, stats.OtherSoftwareRevisionClaimCount);
        Assert.Equal(2, stats.OtherHardwarePartClaimCount);
        Assert.Equal(3, stats.IncompatibleClaimScopeCount);
        Assert.Equal(1, stats.MissingResearchClaimCount);
        Assert.Equal(1, stats.InvalidClaimedScopeCount);
        Assert.False(stats.HasVerifiedOptionMeanings);
        Assert.False(stats.HasAuthenticatedPhysicalCem);
        Assert.False(stats.MayReadOrWriteVehicle);
    }

    [Fact]
    public void EmptyDifferenceProducesFiveZeroCounters()
    {
        var stats = Assessment().Statistics;
        Assert.Equal(0, stats.ChangedOrdinalCount);
        Assert.Equal(0, stats.IncompatibleClaimScopeCount);
        Assert.Equal(0, stats.ExactUnverifiedClaimCount);
        Assert.Equal(0, stats.OtherSoftwareRevisionClaimCount);
        Assert.Equal(0, stats.OtherHardwarePartClaimCount);
        Assert.Equal(0, stats.MissingResearchClaimCount);
        Assert.Equal(0, stats.InvalidClaimedScopeCount);
    }

    [Fact]
    public void MultipleClaimsForSameOrdinalStillProduceOneCount()
    {
        var review = CemConfigurationRevisionResearchTriage.Assess(
            Changed(1), Catalog(
                Claim(1, revision: "SYNTHETIC-2"),
                Claim(1, part: "00000001"),
                Claim(1)),
            "00000000", "SYNTHETIC-1");
        Assert.Single(review.ChangedOrdinals);
        Assert.Equal(1, review.Statistics.ExactUnverifiedClaimCount);
        Assert.Equal(0, review.Statistics.IncompatibleClaimScopeCount);
        Assert.Equal(1, review.Statistics.ChangedOrdinalCount);
    }

    [Fact]
    public void MixedClaimsWithPriorityCountOneDispositionPerOrdinal()
    {
        var review = CemConfigurationRevisionResearchTriage.Assess(
            Changed(1, 3, 5, 140), Catalog(
                Claim(1), Claim(3, revision: "SYNTHETIC-2"),
                Claim(3, part: "00000001"),
                Claim(5, part: "00000001")),
            "00000000", "SYNTHETIC-1");
        var s = review.Statistics;
        Assert.Equal(4, s.ChangedOrdinalCount);
        Assert.Equal(1, s.ExactUnverifiedClaimCount);
        Assert.Equal(1, s.OtherSoftwareRevisionClaimCount);
        Assert.Equal(1, s.OtherHardwarePartClaimCount);
        Assert.Equal(1, s.MissingResearchClaimCount);
        Assert.Equal(0, s.InvalidClaimedScopeCount);
        Assert.Equal(2, s.IncompatibleClaimScopeCount);
    }

    [Fact]
    public void InvalidUserRevisionDoesNotCountAsMissingCatalogOrMatch()
    {
        var review = CemConfigurationRevisionResearchTriage.Assess(
            Changed(1, 2), Catalog(Claim(1)),
            null, "SYNTHETIC-1");
        Assert.Equal(2, review.Statistics.InvalidClaimedScopeCount);
        Assert.Equal(0, review.Statistics.ExactUnverifiedClaimCount);
        Assert.Equal(0, review.Statistics.MissingResearchClaimCount);
        Assert.Equal(0, review.Statistics.IncompatibleClaimScopeCount);
    }

    [Fact]
    public void UnknownStatusFailsClosedInsteadOfDisappearingFromTotals()
    {
        Assert.Throws<InvalidDataException>(() => Assessment(
            Row(1, (CemConfigurationRevisionResearchDisposition)777)));
    }

    [Fact]
    public void CompleteP2ChangeSetCanExceedBoundedCatalogWithoutOvercount()
    {
        var review = CemConfigurationRevisionResearchTriage.Assess(
            Changed(Enumerable.Range(1, 140).ToArray()),
            Catalog(Claim(1)),
            "00000000", "SYNTHETIC-1");
        Assert.Equal(140, review.Statistics.ChangedOrdinalCount);
        Assert.Equal(1, review.Statistics.ExactUnverifiedClaimCount);
        Assert.Equal(139, review.Statistics.MissingResearchClaimCount);
        Assert.Equal(0, review.Statistics.IncompatibleClaimScopeCount);
    }

    [Fact]
    public void RedactedReportsShowOnlyLocalizedNumbersAndStatuses()
    {
        var review = CemConfigurationRevisionResearchTriage.Assess(
            Changed(1, 2, 3, 4), Catalog(
                Claim(1), Claim(2, revision: "SYNTHETIC-2"),
                Claim(3, part: "00000001")),
            "00000000", "SYNTHETIC-1");
        var english = review.CreateRedactedReport(true);
        var russian = review.CreateRedactedReport(false);
        Assert.Contains("Exact user-claim scopes (UNVERIFIED): 1", english);
        Assert.Contains("Other claimed software revisions: 1", english);
        Assert.Contains("Other claimed hardware parts: 1", english);
        Assert.Contains("Incompatible claimed scopes (total): 2", english);
        Assert.Contains("Changed ordinals without research claims: 1", english);
        Assert.Contains("Точные совпадения заявлений (НЕ ПОДТВЕРЖДЕНО): 1", russian);
        Assert.Contains("Несовместимые заявленные ревизии (всего): 2", russian);
        Assert.Contains("Индексы без исследовательских записей: 1", russian);
        foreach (var output in new[] { english, russian })
        {
            Assert.DoesNotContain("00000000", output);
            Assert.DoesNotContain("SYNTHETIC-1", output);
            Assert.DoesNotContain("SYNTHETIC-2", output);
            Assert.DoesNotContain("https://example.invalid", output);
            Assert.DoesNotContain("Synthetic unverified claim", output);
            Assert.DoesNotContain(new string('A', 64), output);
            Assert.DoesNotContain(new string('B', 64), output);
        }
    }

    [Fact]
    public void ChangedRowsAreImmutableAfterStatisticsHaveBeenComputed()
    {
        var original = new List<CemConfigurationRevisionResearchRow>
        {
            Row(1, CemConfigurationRevisionResearchDisposition.NoClaimForChangedOrdinal)
        };
        var review = new CemConfigurationRevisionResearchAssessment(
            CemConfigurationIndexedFormat.P2B9Fc, 140, original);
        original[0] = Row(1,
            CemConfigurationRevisionResearchDisposition.ExactUnverifiedUserClaim);
        Assert.Equal(1, review.Statistics.MissingResearchClaimCount);
        Assert.Equal(0, review.Statistics.ExactUnverifiedClaimCount);
        var readonlyRows =
            Assert.IsAssignableFrom<IList<CemConfigurationRevisionResearchRow>>(
                review.ChangedOrdinals);
        Assert.Throws<NotSupportedException>(() =>
            readonlyRows[0] = original[0]);
    }

    [Fact]
    public void SourceContainsNoVehicleAccessOrExportPath()
    {
        var statsCode = Source("src/Dvinholm.App/CemConfigurationRevisionResearchStatistics.cs");
        var reportCode = Source("src/Dvinholm.App/CemConfigurationRevisionResearchTriage.cs");
        Assert.Contains("Count(ChangedOrdinals)", reportCode);
        Assert.Contains("Statistics.IncompatibleClaimScopeCount", reportCode);
        foreach (var forbidden in new[]
        {
            "PassThru", "J2534Session", "UdsClient", "SendAsync(",
            "WriteConfiguration(", "SecurityAccess(", "ReadCemPin(",
            "File.WriteAllBytes(", "FileMode.Create", "FileAccess.Write"
        })
        {
            Assert.DoesNotContain(forbidden, statsCode);
            Assert.DoesNotContain(forbidden, reportCode);
        }
        Assert.Empty(CemConfigurationMappingResearch.ProductionMappings);
    }

    private static string Source(string relative)
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory);
             directory is not null; directory = directory.Parent)
        {
            var file = Path.Combine(directory.FullName,
                relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(file)) return File.ReadAllText(file);
        }
        throw new FileNotFoundException(relative);
    }
}
