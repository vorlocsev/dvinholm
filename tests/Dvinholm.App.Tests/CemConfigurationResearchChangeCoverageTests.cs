using System.Text.Json;

namespace Dvinholm.App.Tests;

/// <summary>
/// Shared strict three-file pipeline and whole-format/changed cross-tab.
/// Synthetic input only: no real vehicle/ECU records or hardware calls.
/// </summary>
public sealed class CemConfigurationResearchChangeCoverageTests
{
    private const string Part = "00000000";
    private const string Revision = "SYNTHETIC-1";

    private static CemConfigurationMappingResearchClaim Claim(
        int index, CemConfigurationIndexedFormat format = CemConfigurationIndexedFormat.P2B9Fc,
        string part = Part, string revision = Revision,
        string meaning = "Synthetic claimed meaning A",
        string evidence = "https://example.invalid/source-a") =>
        new(format, index, part, revision, meaning, evidence);

    private static CemConfigurationResearchCatalogDocument Catalog(
        params CemConfigurationMappingResearchClaim[] rows) =>
        new(CemConfigurationResearchCatalogImport.Format, 1,
            true, false, false, Array.AsReadOnly(rows));

    private static CemConfigurationOfflineComparisonResult Diff(
        CemConfigurationIndexedFormat format, params int[] ordinals) =>
        new(format, format == CemConfigurationIndexedFormat.P2B9Fc ? 140 : 252,
            new string('A', 64), ordinals.Length == 0
                ? new string('A', 64) : new string('B', 64), ordinals);

    private static CemConfigurationResearchChangeCoverage Review(
        CemConfigurationIndexedFormat format,
        CemConfigurationResearchCatalogDocument catalog,
        params int[] changes)
    {
        var diff = Diff(format, changes);
        var changed = CemConfigurationRevisionResearchTriage.Assess(
            diff, catalog, Part, Revision);
        var coverage = CemConfigurationResearchCoverage.Assess(
            catalog, format, Part, Revision);
        return CemConfigurationResearchChangeCoverage.Combine(changed, coverage);
    }

    [Fact]
    public void P2ReportsWholeCoverageSeparatelyFromChangedIndexCoverage()
    {
        var result = Review(CemConfigurationIndexedFormat.P2B9Fc,
            Catalog(Claim(1), Claim(3, revision: "SYNTHETIC-2"),
                Claim(5, part: "00000001")), 1, 3, 5, 140);
        Assert.Equal(140, result.TotalRawOrdinals);
        Assert.Equal(4, result.ChangedOrdinalCount);
        Assert.Equal(1, result.FullExactUnverifiedClaimCount);
        Assert.Equal(139 - 2, result.FullNoIndexedClaimCount);
        Assert.Equal(1, result.ChangedExactUnverifiedClaimCount);
        Assert.Equal(1, result.ChangedOtherSoftwareRevisionCount);
        Assert.Equal(1, result.ChangedOtherHardwarePartCount);
        Assert.Equal(1, result.ChangedNoIndexedClaimCount);
        Assert.Equal(3, result.ChangedWithoutExactClaimCount);
        Assert.Equal(new[] { 3, 5, 140 }, result.ChangedWithoutExactClaimOrdinals);
        Assert.Equal(0, result.UnchangedExactUnverifiedClaimCount);
        Assert.Equal(136, result.UnchangedNoIndexedClaimCount);
        Assert.Equal(25.0, result.ChangedExactClaimPercent);
        Assert.Equal(25.0, result.ChangedNoClaimPercent);
        Assert.False(result.HasVerifiedCemIdentity);
        Assert.False(result.HasVerifiedOptionMeanings);
        Assert.False(result.IsRestorableBackup);
        Assert.False(result.MayReadLiveConfiguration);
        Assert.False(result.MayWriteConfiguration);
        Assert.False(result.MayAcquirePin);
    }

    [Fact]
    public void P3FinalIndexWorksWith252Ordinals()
    {
        var result = Review(CemConfigurationIndexedFormat.P3F106,
            Catalog(Claim(252, CemConfigurationIndexedFormat.P3F106)), 252);
        Assert.Equal(CemConfigurationIndexedFormat.P3F106, result.Format);
        Assert.Equal(252, result.TotalRawOrdinals);
        Assert.Equal(1, result.ChangedExactUnverifiedClaimCount);
        Assert.Equal(251, result.FullNoIndexedClaimCount);
        Assert.Equal(251, result.UnchangedNoIndexedClaimCount);
        Assert.Equal(100.0, result.ChangedExactClaimPercent);
    }

    [Fact]
    public void EqualBodiesHaveZeroChangedPercentWithoutDivisionByZero()
    {
        var result = Review(CemConfigurationIndexedFormat.P2B9Fc,
            Catalog(Claim(1)));
        Assert.Equal(0, result.ChangedOrdinalCount);
        Assert.Equal(0.0, result.ChangedExactClaimPercent);
        Assert.Equal(0.0, result.ChangedNoClaimPercent);
        Assert.Equal(1, result.UnchangedExactUnverifiedClaimCount);
        Assert.Equal(139, result.UnchangedNoIndexedClaimCount);
        Assert.Empty(result.ChangedWithoutExactClaimOrdinals);
    }

    [Fact]
    public void CrossSourceTextWarningsIntersectOnlyChangedIndices()
    {
        var result = Review(CemConfigurationIndexedFormat.P2B9Fc,
            Catalog(
                Claim(1, revision: "SYNTHETIC-1", meaning: "Synthetic meaning A",
                    evidence: "https://example.invalid/a"),
                Claim(1, revision: "SYNTHETIC-2", meaning: "Synthetic meaning B",
                    evidence: "https://example.invalid/b"),
                Claim(2, revision: "SYNTHETIC-1", meaning: "Synthetic meaning A",
                    evidence: "https://example.invalid/a"),
                Claim(2, revision: "SYNTHETIC-2", meaning: "Synthetic meaning B",
                    evidence: "https://example.invalid/b")), 2, 140);
        Assert.Equal(new[] { 2 }, result.ChangedWithPotentialSourceTextDifferences);
        Assert.Equal(1, result.ChangedWithPotentialSourceTextDifferenceCount);
        Assert.Equal(new[] { 140 }, result.ChangedWithoutExactClaimOrdinals);
    }

    [Fact]
    public void NoClaimForChangedIndexAndNoClaimForWholeFormatDiffer()
    {
        var result = Review(CemConfigurationIndexedFormat.P2B9Fc,
            Catalog(Claim(1)), 1);
        Assert.Equal(139, result.FullNoIndexedClaimCount);
        Assert.Equal(0, result.ChangedNoIndexedClaimCount);
        Assert.Equal(139, result.UnchangedNoIndexedClaimCount);
    }

    [Fact]
    public void CompleteChangedP2BodyRetainsStrict140IndexBoundary()
    {
        var result = Review(CemConfigurationIndexedFormat.P2B9Fc,
            Catalog(Claim(1)), Enumerable.Range(1, 140).ToArray());
        Assert.Equal(140, result.ChangedOrdinalCount);
        Assert.Equal(139, result.ChangedNoIndexedClaimCount);
        Assert.Equal(0, result.UnchangedNoIndexedClaimCount);
        Assert.Equal(139, result.ChangedWithoutExactClaimCount);
        Assert.Equal(140, result.ChangedWithoutExactClaimOrdinals[^1]);
    }

    [Fact]
    public void DifferentGenerationCannotBeJoined()
    {
        var cat = Catalog(Claim(1));
        var changes = CemConfigurationRevisionResearchTriage.Assess(
            Diff(CemConfigurationIndexedFormat.P2B9Fc, 1), cat, Part, Revision);
        var full = CemConfigurationResearchCoverage.Assess(
            cat, CemConfigurationIndexedFormat.P3F106, Part, Revision);
        Assert.Throws<InvalidDataException>(() =>
            CemConfigurationResearchChangeCoverage.Combine(changes, full));
    }

    [Fact]
    public void TwoAssessmentsFromDifferentClaimScopesFailInsteadOfMisleadingUser()
    {
        var cat = Catalog(Claim(1), Claim(2, revision: "SYNTHETIC-2"));
        var comparison = Diff(CemConfigurationIndexedFormat.P2B9Fc, 1);
        var changed = CemConfigurationRevisionResearchTriage.Assess(
            comparison, cat, Part, Revision);
        var coverage = CemConfigurationResearchCoverage.Assess(
            cat, CemConfigurationIndexedFormat.P2B9Fc, Part, "SYNTHETIC-2");
        Assert.Throws<InvalidDataException>(() =>
            CemConfigurationResearchChangeCoverage.Combine(changed, coverage));
    }

    [Fact]
    public void DuplicateReorderedOutOfRangeOrInvalidChangedStatusIsRejected()
    {
        var cat = Catalog(Claim(1));
        var full = CemConfigurationResearchCoverage.Assess(
            cat, CemConfigurationIndexedFormat.P2B9Fc, Part, Revision);
        foreach (var indices in new[] { new[] { 1, 1 }, new[] { 140, 1 }, new[] { 141 } })
        {
            var rows = indices.Select(index =>
                new CemConfigurationRevisionResearchRow(index,
                    CemConfigurationRevisionResearchDisposition.NoClaimForChangedOrdinal)).ToArray();
            var forged = new CemConfigurationRevisionResearchAssessment(
                CemConfigurationIndexedFormat.P2B9Fc, 140, rows);
            Assert.Throws<InvalidDataException>(() =>
                CemConfigurationResearchChangeCoverage.Combine(forged, full));
        }
        var bad = new CemConfigurationRevisionResearchAssessment(
            CemConfigurationIndexedFormat.P2B9Fc, 140,
            [new CemConfigurationRevisionResearchRow(1,
                CemConfigurationRevisionResearchDisposition.AssertedScopeMissingOrInvalid)]);
        Assert.Throws<InvalidDataException>(() =>
            CemConfigurationResearchChangeCoverage.Combine(bad, full));
    }

    [Fact]
    public void RedactedReportsAreLocalizedAndNeverContainInputs()
    {
        var result = Review(CemConfigurationIndexedFormat.P2B9Fc,
            Catalog(Claim(1)), 1, 140);
        var english = result.CreateRedactedReport(true);
        var russian = result.CreateRedactedReport(false);
        Assert.Contains("Changed indices with exact unverified claims: 1 (50.0%)", english);
        Assert.Contains("Changed indices without research claims: 1 (50.0%)", english);
        Assert.Contains("Изменённые индексы без исследовательских записей: 1 (50.0%)", russian);
        Assert.Contains("Changed indices lacking an exact user claim: 140", english);
        foreach (var text in new[] { english, russian })
        {
            Assert.DoesNotContain(Part, text);
            Assert.DoesNotContain(Revision, text);
            Assert.DoesNotContain("https://example.invalid", text);
            Assert.DoesNotContain("Synthetic claimed meaning", text);
            Assert.DoesNotContain(new string('A', 64), text);
            Assert.DoesNotContain(new string('B', 64), text);
            Assert.DoesNotContain("0x12", text);
            Assert.DoesNotContain("VIN: ", text);
        }
    }

    private static byte[] Body(int count)
    {
        var body = new byte[count + 2];
        body[0] = checked((byte)count);
        body[2] = 0x12;
        body[^1] = 0x34;
        var checksum = 0;
        foreach (var value in body.AsSpan(2))
            checksum = (checksum + value) & 0xFF;
        body[1] = (byte)checksum;
        return body;
    }

    private static string Json() => JsonSerializer.Serialize(new
    {
        format = CemConfigurationResearchCatalogImport.Format,
        schemaVersion = 1, isUnverifiedResearch = true,
        isRestorableBackup = false, mayAccessEcu = false,
        claims = new[] { new
        {
            dataFormat = "P2B9Fc", index1Based = 1,
            hardwarePartNumber = Part, softwareRevision = Revision,
            claimedMeaning = "Synthetic claimed meaning A",
            evidenceReference = "https://example.invalid/source-a"
        }}
    });

    [Fact]
    public async Task ExactHeadLocalFilePipelineUsesSameCatalogAndTwoValidBodies()
    {
        var folder = Path.Combine(Path.GetTempPath(),
            "Dvinholm-Cem-Cross-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var first = Path.Combine(folder, "first.bin");
            var second = Path.Combine(folder, "second.bin");
            var catalog = Path.Combine(folder, "research.json");
            var old = Body(140);
            var changed = (byte[])old.Clone();
            changed[2] ^= 0x10;
            changed[^1] ^= 0x20;
            var sum = 0;
            foreach (var item in changed.AsSpan(2))
                sum = (sum + item) & 0xFF;
            changed[1] = (byte)sum;
            File.WriteAllBytes(first, old);
            File.WriteAllBytes(second, changed);
            File.WriteAllText(catalog, Json());

            var result = await CemConfigurationOfflineResearchPipeline
                .ReviewCoverageAndChangesAsync(first, second, catalog, Part, Revision);
            Assert.Equal(2, result.ChangedOrdinalCount);
            Assert.Equal(1, result.ChangedExactUnverifiedClaimCount);
            Assert.Equal(1, result.ChangedNoIndexedClaimCount);
            Assert.Equal(new[] { 140 }, result.ChangedWithoutExactClaimOrdinals);
        }
        finally { Directory.Delete(folder, true); }
    }

    [Fact]
    public async Task MissingScopeOrMalformedCatalogFailsClosedBeforeReport()
    {
        var folder = Path.Combine(Path.GetTempPath(),
            "Dvinholm-Cem-Cross-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var catalog = Path.Combine(folder, "research.json");
            File.WriteAllText(catalog, Json());
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                CemConfigurationOfflineResearchPipeline.ReviewCoverageAndChangesAsync(
                    "missing1.bin", "missing2.bin", catalog, null, Revision));

            File.WriteAllText(catalog, "{\"unexpected\":true}");
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                CemConfigurationOfflineResearchPipeline.ReviewCoverageAndChangesAsync(
                    "missing1.bin", "missing2.bin", catalog, Part, Revision));
        }
        finally { Directory.Delete(folder, true); }
    }

    [Fact]
    public void SourceGuardHasNoLiveAdapterOrWritePath()
    {
        var merged = Source("src/Dvinholm.App/CemConfigurationResearchChangeCoverage.cs");
        var pipeline = Source("src/Dvinholm.App/CemConfigurationOfflineResearchPipeline.cs");
        Assert.Contains("CemConfigurationResearchCoverage.Assess", pipeline);
        Assert.Contains("CemConfigurationRevisionResearchTriage.Assess", pipeline);
        Assert.Contains("CemConfigurationResearchChangeCoverage.Combine", pipeline);
        Assert.Contains("CemConfigurationResearchCatalogImport.InspectFileAsync", pipeline);
        Assert.Contains("CemConfigurationOfflineComparison.CompareFilesAsync", pipeline);
        foreach (var code in new[] { merged, pipeline })
            foreach (var forbidden in new[] {
                "PassThru", "J2534Session", "UdsClient", "SendAsync(",
                "SecurityAccess(", "ReadCemPin(", "WriteConfiguration(",
                "File.WriteAllBytes(", "FileMode.Create", "FileAccess.Write"
            })
                Assert.DoesNotContain(forbidden, code);
        Assert.Empty(CemConfigurationMappingResearch.ProductionMappings);
    }

    private static string Source(string relative)
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
            dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName,
                relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path)) return File.ReadAllText(path);
        }
        throw new FileNotFoundException(relative);
    }
}
