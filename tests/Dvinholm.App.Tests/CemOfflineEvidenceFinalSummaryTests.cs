using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Dvinholm.App.Tests;

/// <summary>
/// Three explicitly selected synthetic files only. A consolidated report
/// must not authenticate a module, leak original data or overwrite files.
/// </summary>
public sealed class CemOfflineEvidenceFinalSummaryTests
{
    private const string Part = "00000000";
    private const string Revision = "SYNTHETIC-1";
    private static readonly string FirstHash = new('A', 64);
    private static readonly string SecondHash = new('B', 64);
    private static readonly string JsonHash = new('C', 64);
    private static readonly DateTimeOffset FirstUtc =
        new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset AuditUtc =
        new(2026, 1, 10, 8, 0, 0, TimeSpan.Zero);

    private static CemOfflineProvenanceDeclaration[] Declarations() =>
    [
        new(CemOfflineProvenanceRole.PreviousBody,
            "synthetic-source-original", "CLAIMED-MODULE",
            Part, Revision, FirstUtc, FirstHash),
        new(CemOfflineProvenanceRole.CurrentBody,
            "synthetic-source-current", "CLAIMED-MODULE",
            Part, Revision, FirstUtc.AddDays(1), SecondHash),
        new(CemOfflineProvenanceRole.ResearchCatalog,
            "synthetic-source-research", "CLAIMED-MODULE",
            Part, Revision, FirstUtc.AddDays(2), JsonHash)
    ];

    private static (CemOfflineEvidenceFinalSummary Summary,
        CemOfflineProvenanceAssessment Provenance) Build(
        CemConfigurationIndexedFormat format = CemConfigurationIndexedFormat.P2B9Fc,
        params int[] changes)
    {
        var index = format == CemConfigurationIndexedFormat.P3F106 ? 252 : 1;
        var claim = new CemConfigurationMappingResearchClaim(
            format, index, Part, Revision,
            "SYNTHETIC PRIVATE CLAIM", "https://example.invalid/research");
        var catalog = new CemConfigurationResearchCatalogDocument(
            CemConfigurationResearchCatalogImport.Format, 1, true, false, false,
            [claim]);
        var beforeHash = FirstHash;
        var afterHash = changes.Length == 0 ? FirstHash : SecondHash;
        var comparison = new CemConfigurationOfflineComparisonResult(
            format, format == CemConfigurationIndexedFormat.P2B9Fc ? 140 : 252,
            beforeHash, afterHash, changes);
        var changed = CemConfigurationRevisionResearchTriage.Assess(
            comparison, catalog, Part, Revision);
        var coverage = CemConfigurationResearchCoverage.Assess(
            catalog, format, Part, Revision);
        var research = CemConfigurationResearchChangeCoverage.Combine(
            changed, coverage);
        var checklist = CemConfigurationOfflineEvidenceChecklist.Assess(research);
        var claims = Declarations();
        claims[1] = claims[1] with { ExpectedSha256 = afterHash };
        var provenance = CemOfflineProvenanceAudit.Assess(
            claims, beforeHash, afterHash, JsonHash, Part, Revision, checklist);
        var freshness = CemOfflineEvidenceFreshness.Assess(
            claims, provenance, Array.Empty<CemOfflineEvidenceLink>(),
            Array.Empty<CemOfflineEvidenceRecheck>(),
            AuditUtc, TimeSpan.FromDays(7));
        return (new CemOfflineEvidenceFinalSummary(
            research, freshness, provenance), provenance);
    }

    [Fact]
    public void FullP2ReportIncludesResearchAndRecheckDetailsFromSameResult()
    {
        var summary = Build(CemConfigurationIndexedFormat.P2B9Fc, 1, 140).Summary;
        Assert.Equal(2, summary.ChangedRawIndices);
        Assert.Equal(1, summary.UnverifiedChangedExactClaims);
        Assert.Equal(1, summary.ChangedWithoutExactClaim);
        Assert.Equal(3, summary.LocalSourcesNeedingRecheck);
        Assert.Equal(7, summary.IndependentPhysicalGatesOpen);
        Assert.Contains("Total raw indices: 140", summary.CreateRedactedReport(true));
        Assert.Contains("Changed indices with exact unverified claims: 1 (50.0%)",
            summary.CreateRedactedReport(true));
        Assert.Contains("Sources requiring another review: 3",
            summary.CreateRedactedReport(true));
    }

    [Fact]
    public void P3Index252RetainsCorrectGenerationAndNoOemAuthority()
    {
        var summary = Build(CemConfigurationIndexedFormat.P3F106, 252).Summary;
        Assert.Equal(252, summary.Research.TotalRawOrdinals);
        Assert.Equal(1, summary.ChangedRawIndices);
        Assert.Equal(1, summary.UnverifiedChangedExactClaims);
        Assert.Contains("P3 F106", summary.CreateRedactedReport(true));
        Assert.False(summary.HasVerifiedSamePhysicalCem);
        Assert.False(summary.HasVerifiedSourceProvenance);
        Assert.False(summary.HasVerifiedOptionMeaning);
        Assert.False(summary.MayReadLiveConfiguration);
        Assert.False(summary.MayAcquirePin);
        Assert.False(summary.MayWriteConfiguration);
        Assert.False(summary.IsRestorableBackup);
    }

    [Fact]
    public void EqualBodiesHaveZeroChangesButGatesAreStillOpen()
    {
        var summary = Build().Summary;
        Assert.Equal(0, summary.ChangedRawIndices);
        Assert.Equal(0, summary.UnverifiedChangedExactClaims);
        Assert.Contains("FINAL GATE: OPEN 7/7",
            summary.CreateRedactedReport(true));
    }

    [Fact]
    public void ConflictingChangeCountsBetweenSectionsFailClosed()
    {
        var changed = Build(CemConfigurationIndexedFormat.P2B9Fc, 1).Summary;
        var empty = Build().Summary;
        var emptyProvenance = Build().Provenance;
        Assert.Throws<InvalidDataException>(() =>
            new CemOfflineEvidenceFinalSummary(
                changed.Research, empty.Freshness, emptyProvenance));
    }

    [Fact]
    public void BothLanguagesRedactAllUserSourceDataAndHashes()
    {
        var summary = Build(CemConfigurationIndexedFormat.P2B9Fc, 1).Summary;
        var english = summary.CreateRedactedReport(true);
        var russian = summary.CreateRedactedReport(false);
        Assert.Contains("CONSOLIDATED REDACTED REPORT", english);
        Assert.Contains("ИТОГОВЫЙ ОБЕЗЛИЧЕННЫЙ", russian);
        Assert.Contains("OPEN 7/7", english);
        Assert.Contains("ОТКРЫТО 7/7", russian);
        foreach (var report in new[] { english, russian })
        {
            Assert.DoesNotContain(Part, report);
            Assert.DoesNotContain(Revision, report);
            Assert.DoesNotContain("CLAIMED-MODULE", report);
            Assert.DoesNotContain("synthetic-source-", report);
            Assert.DoesNotContain("2026-01-01", report);
            Assert.DoesNotContain("https://example.invalid", report);
            Assert.DoesNotContain("SYNTHETIC PRIVATE CLAIM", report);
            Assert.DoesNotContain(FirstHash, report);
            Assert.DoesNotContain(SecondHash, report);
            Assert.DoesNotContain(JsonHash, report);
            Assert.DoesNotContain("0x12", report);
        }
    }

    [Fact]
    public void Utf8FinalReportIsBoundedAndUsesOnlyRedactedText()
    {
        var summary = Build(CemConfigurationIndexedFormat.P2B9Fc, 1).Summary;
        foreach (var english in new[] { true, false })
        {
            var bytes = CemOfflineEvidenceRedactedExport.PrepareUtf8(
                summary, english);
            Assert.InRange(bytes.Length, 100,
                CemOfflineEvidenceRedactedExport.MaximumReportBytes);
            var decoded = Encoding.UTF8.GetString(bytes);
            Assert.Equal(summary.CreateRedactedReport(english), decoded);
            Assert.DoesNotContain(Revision, decoded);
            Assert.DoesNotContain(FirstHash, decoded);
        }
    }

    [Fact]
    public async Task AtomicNoOverwriteExportPublishesCompleteRedactedText()
    {
        var dir = Path.Combine(Path.GetTempPath(),
            "Dvinholm-Atomic-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "review.txt");
            var summary = Build(CemConfigurationIndexedFormat.P2B9Fc, 1).Summary;
            var expected = CemOfflineEvidenceRedactedExport.PrepareUtf8(
                summary, english: true);
            await CemOfflineEvidenceRedactedExport.SaveNewTextFileAsync(
                path, summary, english: true);
            Assert.True(File.Exists(path));
            Assert.True(expected.SequenceEqual(File.ReadAllBytes(path)));
            Assert.DoesNotContain(Directory.GetFiles(dir),
                x => Path.GetFileName(x).StartsWith(".dvinholm-", StringComparison.Ordinal));
            Assert.False(expected.Length >= 3 &&
                expected[0] == 0xEF && expected[1] == 0xBB && expected[2] == 0xBF);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task DestinationAlreadyPresentIsNeverOverwrittenOrRemoved()
    {
        var dir = Path.Combine(Path.GetTempPath(),
            "Dvinholm-Atomic-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "review.txt");
            var previous = Encoding.UTF8.GetBytes("Keep pre-existing user text.");
            File.WriteAllBytes(path, previous);
            await Assert.ThrowsAsync<IOException>(() =>
                CemOfflineEvidenceRedactedExport.SaveNewTextFileAsync(
                    path, Build().Summary, english: false));
            Assert.True(previous.SequenceEqual(File.ReadAllBytes(path)));
            Assert.Single(Directory.GetFiles(dir));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task ExportWithCancelledTokenCreatesNeitherFinalNorTemporaryFile()
    {
        var dir = Path.Combine(Path.GetTempPath(),
            "Dvinholm-Atomic-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        try
        {
            var path = Path.Combine(dir, "report.txt");
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                CemOfflineEvidenceRedactedExport.SaveNewTextFileAsync(
                    path, Build().Summary, true, cts.Token));
            Assert.Empty(Directory.GetFiles(dir));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task OutputRequiresExplicitAbsoluteTxtPath()
    {
        var summary = Build().Summary;
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            CemOfflineEvidenceRedactedExport.SaveNewTextFileAsync(
                "relative-report.txt", summary, true));
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            CemOfflineEvidenceRedactedExport.SaveNewTextFileAsync(
                Path.Combine(Path.GetTempPath(), "configuration.bin"),
                summary, true));
    }

    private static byte[] Body(int count, byte firstValue)
    {
        var bytes = new byte[count + 2];
        bytes[0] = checked((byte)count);
        bytes[2] = firstValue;
        bytes[^1] = 0x20;
        var sum = 0;
        foreach (var b in bytes.AsSpan(2)) sum = (sum + b) & 255;
        bytes[1] = (byte)sum;
        return bytes;
    }

    private static string ResearchJson() => JsonSerializer.Serialize(new
    {
        format = CemConfigurationResearchCatalogImport.Format,
        schemaVersion = 1, isUnverifiedResearch = true,
        isRestorableBackup = false, mayAccessEcu = false,
        claims = new[] { new
        {
            dataFormat = "P2B9Fc", index1Based = 1,
            hardwarePartNumber = Part, softwareRevision = Revision,
            claimedMeaning = "SYNTHETIC PRIVATE CLAIM",
            evidenceReference = "https://example.invalid/research"
        }}
    });

    [Fact]
    public async Task UnifiedPipelineUsesOneValidatedInputSetAndNoVehicleAccess()
    {
        var dir = Path.Combine(Path.GetTempPath(),
            "Dvinholm-Summary-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var firstPath = Path.Combine(dir, "before.bin");
            var secondPath = Path.Combine(dir, "after.bin");
            var catalogPath = Path.Combine(dir, "research.json");
            var first = Body(140, 0x12);
            var second = Body(140, 0x13);
            var catalog = Encoding.UTF8.GetBytes(ResearchJson());
            File.WriteAllBytes(firstPath, first);
            File.WriteAllBytes(secondPath, second);
            File.WriteAllBytes(catalogPath, catalog);
            var declarations = Declarations();
            declarations[0] = declarations[0] with {
                ExpectedSha256 = Convert.ToHexString(SHA256.HashData(first)) };
            declarations[1] = declarations[1] with {
                ExpectedSha256 = Convert.ToHexString(SHA256.HashData(second)) };
            declarations[2] = declarations[2] with {
                ExpectedSha256 = Convert.ToHexString(SHA256.HashData(catalog)) };
            var result = await CemConfigurationOfflineResearchPipeline.ReviewFinalSummaryAsync(
                firstPath, secondPath, catalogPath, Part, Revision,
                declarations, Array.Empty<CemOfflineEvidenceLink>(),
                Array.Empty<CemOfflineEvidenceRecheck>(),
                AuditUtc, TimeSpan.FromDays(7));
            Assert.Equal(1, result.ChangedRawIndices);
            Assert.Equal(1, result.UnverifiedChangedExactClaims);
            Assert.Equal(7, result.IndependentPhysicalGatesOpen);
            Assert.Equal(3, result.LocalSourcesNeedingRecheck);
            Assert.False(result.MayReadLiveConfiguration);
            Assert.False(result.MayAcquirePin);
            Assert.False(result.MayWriteConfiguration);
            Assert.DoesNotContain(dir, result.CreateRedactedReport(true));
            Assert.True(first.SequenceEqual(File.ReadAllBytes(firstPath)));
            Assert.True(second.SequenceEqual(File.ReadAllBytes(secondPath)));
            Assert.True(catalog.SequenceEqual(File.ReadAllBytes(catalogPath)));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task UnsafeCatalogIsRejectedBeforeMissingBinaryFiles()
    {
        var dir = Path.Combine(Path.GetTempPath(),
            "Dvinholm-Summary-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var catalog = Path.Combine(dir, "unsafe.json");
            File.WriteAllText(catalog, "{\"format\":\"unknown\",\"pin\":\"1234\"}");
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                CemConfigurationOfflineResearchPipeline.ReviewFinalSummaryAsync(
                    Path.Combine(dir, "no-before.bin"), Path.Combine(dir, "no-after.bin"),
                    catalog, Part, Revision, Declarations(),
                    Array.Empty<CemOfflineEvidenceLink>(),
                    Array.Empty<CemOfflineEvidenceRecheck>(),
                    AuditUtc, TimeSpan.FromDays(7)));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void NewPipelineAndExporterHaveNoDiagnosticOrBinaryDumpOutput()
    {
        var pipeline = Source("src/Dvinholm.App/CemConfigurationOfflineResearchPipeline.cs");
        var report = Source("src/Dvinholm.App/CemOfflineEvidenceFinalSummary.cs");
        var exporter = Source("src/Dvinholm.App/CemOfflineEvidenceRedactedExport.cs");
        Assert.Contains("ReviewProvenanceAndCoverageAsync", pipeline);
        Assert.Contains("ReviewFinalSummaryAsync", pipeline);
        Assert.Contains("CemOfflineEvidenceFreshness.Assess", pipeline);
        Assert.Contains("CemConfigurationResearchChangeCoverage.Combine", pipeline);
        Assert.Contains("CreateRedactedReport", exporter);
        Assert.Contains("FileMode.CreateNew", exporter);
        Assert.Contains("File.Move(temporary, selectedOutputPath, overwrite: false)", exporter);
        foreach (var code in new[] { pipeline, report, exporter })
            foreach (var forbidden in new[] {
                "PassThru", "J2534Session", "UdsClient", "SendAsync(",
                "SecurityAccess(", "ReadCemPin(", "WriteConfiguration(",
                "File.WriteAllBytes(", "FileMode.Create,", "FileMode.OpenOrCreate"
            })
                Assert.DoesNotContain(forbidden, code);
        Assert.Empty(CemConfigurationMappingResearch.ProductionMappings);
    }

    private static string Source(string relative)
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory);
             directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName,
                relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path)) return File.ReadAllText(path);
        }
        throw new FileNotFoundException(relative);
    }
}
