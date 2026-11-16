using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Dvinholm.App.Tests;

/// <summary>
/// Deterministic synthetic timestamps, local P2 bodies and unverified
/// provenance. The output has NO physical vehicle/PIN/write authority.
/// </summary>
public sealed class CemOfflineEvidenceFreshnessTests
{
    private const string Part = "00000000";
    private const string Revision = "SYNTHETIC-1";
    private static readonly DateTimeOffset FirstUtc =
        new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset SecondUtc =
        new(2026, 1, 2, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ThirdUtc =
        new(2026, 1, 3, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset AuditUtc =
        new(2026, 1, 10, 8, 0, 0, TimeSpan.Zero);
    private static readonly string HashA = new('A', 64);
    private static readonly string HashB = new('B', 64);
    private static readonly string HashC = new('C', 64);

    private static CemOfflineProvenanceDeclaration[] Declarations() =>
    [
        new(CemOfflineProvenanceRole.PreviousBody, "user-source-original",
            "SYNTHETIC-MODULE", Part, Revision, FirstUtc, HashA),
        new(CemOfflineProvenanceRole.CurrentBody, "user-source-current",
            "SYNTHETIC-MODULE", Part, Revision, SecondUtc, HashB),
        new(CemOfflineProvenanceRole.ResearchCatalog, "user-source-research",
            "SYNTHETIC-MODULE", Part, Revision, ThirdUtc, HashC)
    ];

    private static CemOfflineProvenanceAssessment Provenance(
        IReadOnlyList<CemOfflineProvenanceDeclaration> declarations)
    {
        var claim = new CemConfigurationMappingResearchClaim(
            CemConfigurationIndexedFormat.P2B9Fc, 1, Part, Revision,
            "SYNTHETIC ONLY research", "https://example.invalid/synthetic");
        var catalog = new CemConfigurationResearchCatalogDocument(
            CemConfigurationResearchCatalogImport.Format, 1, true, false, false,
            [claim]);
        var changed = CemConfigurationRevisionResearchTriage.Assess(
            new CemConfigurationOfflineComparisonResult(
                CemConfigurationIndexedFormat.P2B9Fc, 140, HashA, HashB, [1]),
            catalog, Part, Revision);
        var whole = CemConfigurationResearchCoverage.Assess(
            catalog, CemConfigurationIndexedFormat.P2B9Fc, Part, Revision);
        var checkpoint = CemConfigurationOfflineEvidenceChecklist.Assess(
            CemConfigurationResearchChangeCoverage.Combine(changed, whole));
        return CemOfflineProvenanceAudit.Assess(
            declarations, HashA, HashB, HashC, Part, Revision, checkpoint);
    }

    private static CemOfflineEvidenceFreshnessAssessment Review(
        IReadOnlyList<CemOfflineEvidenceRecheck>? rechecks = null,
        IReadOnlyList<CemOfflineEvidenceLink>? links = null,
        CemOfflineProvenanceDeclaration[]? declarations = null,
        DateTimeOffset? auditUtc = null,
        TimeSpan? maxAge = null)
    {
        declarations ??= Declarations();
        return CemOfflineEvidenceFreshness.Assess(
            declarations, Provenance(declarations),
            links ?? Array.Empty<CemOfflineEvidenceLink>(),
            rechecks ?? Array.Empty<CemOfflineEvidenceRecheck>(),
            auditUtc ?? AuditUtc, maxAge ?? TimeSpan.FromDays(7));
    }

    [Fact]
    public void NoRechecksMeansAllThreeNeedReviewAndNothingIsAuthenticated()
    {
        var r = Review();
        Assert.Equal(3, r.NeedsNewLocalReviewCount);
        Assert.Equal(0, r.LocallyConsistentUnverifiedCount);
        Assert.All(r.Sources, x => Assert.Equal(
            CemOfflineEvidenceFreshnessKind.NoRepeatInspection, x.Kind));
        Assert.False(r.HasAuthenticatedPhysicalCem);
        Assert.False(r.HasVerifiedSourceIndependence);
        Assert.False(r.HasVerifiedOptionMeaning);
        Assert.False(r.MayReadLiveConfiguration);
        Assert.False(r.MayAcquirePin);
        Assert.False(r.MayWriteConfiguration);
        Assert.False(r.IsRestorableBackup);
        Assert.Equal(7, r.Ledger.OpenPhysicalEvidenceGates.Count);
    }

    [Fact]
    public void MatchingRecentUserClaimsAreStillNotVerified()
    {
        var r = Review(
        [
            new(CemOfflineProvenanceRole.PreviousBody, AuditUtc.AddDays(-1), HashA),
            new(CemOfflineProvenanceRole.CurrentBody, AuditUtc.AddDays(-1), HashB),
            new(CemOfflineProvenanceRole.ResearchCatalog, AuditUtc.AddDays(-1), HashC)
        ]);
        Assert.Equal(3, r.LocallyConsistentUnverifiedCount);
        Assert.Equal(0, r.NeedsNewLocalReviewCount);
        Assert.All(r.Sources, item => Assert.Equal(
            CemOfflineEvidenceFreshnessKind.LocallyConsistentClaimUnverified,
            item.Kind));
        Assert.False(r.HasAuthenticatedPhysicalCem);
        Assert.Equal(7, r.Ledger.OpenPhysicalEvidenceGates.Count);
    }

    [Fact]
    public void InitialHashMismatchCannotBeClearedByUserSuppliedLaterMatch()
    {
        var declarations = Declarations();
        declarations[0] = declarations[0] with {
            ExpectedSha256 = new string('D', 64) };
        var r = Review(
            rechecks: [new(CemOfflineProvenanceRole.PreviousBody,
                AuditUtc.AddDays(-1), new string('D', 64))],
            declarations: declarations);
        Assert.Equal(CemOfflineEvidenceFreshnessKind.InitialLocalDigestMismatch,
            r.Sources[0].Kind);
        Assert.False(r.HasAuthenticatedPhysicalCem);
    }

    [Fact]
    public void LatestLocalDigestDisagreementRequiresReview()
    {
        var r = Review(rechecks: [
            new(CemOfflineProvenanceRole.PreviousBody,
                AuditUtc.AddDays(-1), HashC)]);
        Assert.Equal(CemOfflineEvidenceFreshnessKind.LatestLocalDigestMismatch,
            r.Sources[0].Kind);
        Assert.Equal(1, r.Ledger.RepeatLocalHashMismatchCount);
    }

    [Fact]
    public void OldUserClaimedRecheckIsStaleRelativeToSelectedSevenDayWindow()
    {
        var r = Review(rechecks: [
            new(CemOfflineProvenanceRole.PreviousBody, FirstUtc.AddDays(1), HashA)]);
        Assert.Equal(CemOfflineEvidenceFreshnessKind.LatestClaimedRepeatOlderThanPolicy,
            r.Sources[0].Kind);
        Assert.Equal(3, r.NeedsNewLocalReviewCount);
    }

    [Fact]
    public void ClaimedFutureRecheckIsNotConsideredFresh()
    {
        var r = Review(rechecks: [
            new(CemOfflineProvenanceRole.PreviousBody,
                AuditUtc.AddDays(1), HashA)]);
        Assert.Equal(CemOfflineEvidenceFreshnessKind.ClaimedRepeatAfterReviewTime,
            r.Sources[0].Kind);
        Assert.True(r.HasChronologyConcerns);
    }

    [Fact]
    public void ClaimedRecheckBeforeOriginalObservationIsFlagged()
    {
        var r = Review(rechecks: [
            new(CemOfflineProvenanceRole.ResearchCatalog,
                FirstUtc, HashC)]);
        Assert.Equal(
            CemOfflineEvidenceFreshnessKind.ClaimedRepeatBeforeOriginalObservation,
            r.Sources[2].Kind);
        Assert.True(r.HasChronologyConcerns);
    }

    [Fact]
    public void DigestDriftIsRememberedEvenWhenLatestDigestMatches()
    {
        var r = Review(rechecks: [
            new(CemOfflineProvenanceRole.PreviousBody,
                AuditUtc.AddDays(-3), HashA),
            new(CemOfflineProvenanceRole.PreviousBody,
                AuditUtc.AddDays(-2), HashB),
            new(CemOfflineProvenanceRole.PreviousBody,
                AuditUtc.AddDays(-1), HashA)]);
        Assert.Equal(CemOfflineEvidenceFreshnessKind.LocallyConsistentClaimUnverified,
            r.Sources[0].Kind);
        Assert.Equal(1, r.SourcesWithPriorDigestDrift);
        Assert.True(r.Sources[0].AnyRepeatDigestDrift);
        Assert.Equal(2, r.Ledger.RecheckDigestChangedCount);
    }

    [Fact]
    public void LinkConflictAndReusedSourceLabelsRemainVisible()
    {
        var declarations = Declarations();
        declarations[1] = declarations[1] with {
            ClaimedModuleAssociation = "DIFFERENT",
            SourceReference = declarations[0].SourceReference };
        var r = Review(
            declarations: declarations,
            links: [
                new(CemOfflineProvenanceRole.PreviousBody,
                    CemOfflineProvenanceRole.CurrentBody,
                    CemOfflineEvidenceLinkClaim.SameClaimedModule),
                new(CemOfflineProvenanceRole.PreviousBody,
                    CemOfflineProvenanceRole.CurrentBody,
                    CemOfflineEvidenceLinkClaim.DistinctClaimedSource)
            ]);
        Assert.True(r.HasUserClaimLinkConflicts);
        Assert.Equal(1, r.Ledger.LinkTextConflictCount);
        Assert.Equal(1, r.Ledger.SharedSourceLabelCount);
        Assert.False(r.HasVerifiedSourceIndependence);
    }

    [Fact]
    public void FreshnessWindowMustBeBoundedAndUtc()
    {
        Assert.Throws<InvalidDataException>(() => Review(
            maxAge: TimeSpan.Zero));
        Assert.Throws<InvalidDataException>(() => Review(
            maxAge: TimeSpan.FromDays(31)));
        Assert.Throws<InvalidDataException>(() => Review(
            auditUtc: new DateTimeOffset(2026, 1, 10, 8, 0, 0,
                TimeSpan.FromHours(3))));
    }

    [Fact]
    public void BilingualSummaryAndJournalNeverLeakClaimedSourceData()
    {
        var r = Review(
            rechecks: [new(CemOfflineProvenanceRole.PreviousBody,
                AuditUtc.AddDays(-1), HashA)],
            links: [new(CemOfflineProvenanceRole.PreviousBody,
                CemOfflineProvenanceRole.ResearchCatalog,
                CemOfflineEvidenceLinkClaim.DistinctClaimedSource)]);
        var english = r.CreateRedactedReport(true);
        var russian = r.CreateRedactedReport(false);
        Assert.Contains("Sources requiring another review: 2", english);
        Assert.Contains("Источники, требующие повторной проверки: 2", russian);
        Assert.Contains("OPEN 7/7", english);
        Assert.Contains("ОТКРЫТО 7/7", russian);
        foreach (var output in new[] { english, russian })
        {
            Assert.DoesNotContain("SYNTHETIC-MODULE", output);
            Assert.DoesNotContain(Part, output);
            Assert.DoesNotContain(Revision, output);
            Assert.DoesNotContain("user-source-", output);
            Assert.DoesNotContain("2026-01", output);
            Assert.DoesNotContain("https://example.invalid", output);
            Assert.DoesNotContain(new string('A', 64), output);
            Assert.DoesNotContain(new string('B', 64), output);
            Assert.DoesNotContain(new string('C', 64), output);
            Assert.DoesNotContain("0x12", output);
        }
    }

    [Fact]
    public async Task SafeUtf8ExportCreatesNewRedactedTxtOnly()
    {
        var path = Path.Combine(Path.GetTempPath(),
            "Dvinholm-Redacted-" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            var review = Review(rechecks: [
                new(CemOfflineProvenanceRole.PreviousBody,
                    AuditUtc.AddDays(-1), HashA)]);
            var payload = CemOfflineEvidenceRedactedExport.PrepareUtf8(review, false);
            Assert.InRange(payload.Length, 100, CemOfflineEvidenceRedactedExport.MaximumReportBytes);
            await CemOfflineEvidenceRedactedExport.SaveNewTextFileAsync(
                path, review, english: false);
            Assert.True(payload.SequenceEqual(File.ReadAllBytes(path)));
            var bytes = File.ReadAllBytes(path);
            Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF &&
                bytes[1] == 0xBB && bytes[2] == 0xBF);
            var content = Encoding.UTF8.GetString(bytes);
            Assert.Contains("ОТКРЫТО 7/7", content);
            Assert.DoesNotContain("SYNTHETIC-MODULE", content);
            Assert.DoesNotContain(new string('A', 64), content);
            Assert.DoesNotContain(Part, content);
            Assert.DoesNotContain(Revision, content);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ExportRefusesOverwriteAndPreservesExistingFileBytes()
    {
        var path = Path.Combine(Path.GetTempPath(),
            "Dvinholm-Redacted-" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            var existing = Encoding.UTF8.GetBytes("existing content cannot be replaced");
            File.WriteAllBytes(path, existing);
            await Assert.ThrowsAsync<IOException>(() =>
                CemOfflineEvidenceRedactedExport.SaveNewTextFileAsync(
                    path, Review(), english: true));
            Assert.True(existing.SequenceEqual(File.ReadAllBytes(path)));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ExportRejectsWrongExtensionAndPreCancellationWithoutOutput()
    {
        var path = Path.Combine(Path.GetTempPath(),
            "Dvinholm-Redacted-" + Guid.NewGuid().ToString("N") + ".bin");
        var txt = Path.ChangeExtension(path, ".txt");
        using var cts = new CancellationTokenSource();
        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                CemOfflineEvidenceRedactedExport.SaveNewTextFileAsync(
                    path, Review(), english: true));
            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                CemOfflineEvidenceRedactedExport.SaveNewTextFileAsync(
                    txt, Review(), english: true, cancellationToken: cts.Token));
            Assert.False(File.Exists(path));
            Assert.False(File.Exists(txt));
        }
        finally { File.Delete(path); File.Delete(txt); }
    }

    [Fact]
    public async Task InvalidFreshnessPolicyFailsBeforeOpeningMissingSourceFiles()
    {
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            CemConfigurationOfflineResearchPipeline.ReviewFreshnessAsync(
                "not-here.bin", "not-here2.bin", "not-here.json",
                Part, Revision, Declarations(),
                Array.Empty<CemOfflineEvidenceLink>(),
                Array.Empty<CemOfflineEvidenceRecheck>(),
                AuditUtc, TimeSpan.Zero));
    }

    private static byte[] Body(int value)
    {
        var bytes = new byte[142];
        bytes[0] = 140;
        bytes[2] = checked((byte)value);
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
            claimedMeaning = "SYNTHETIC ONLY research",
            evidenceReference = "https://example.invalid/synthetic"
        }}
    });

    [Fact]
    public async Task ThreeFilePipelineDoesNotGrantPermissionsAfterMatchingClaims()
    {
        var dir = Path.Combine(Path.GetTempPath(),
            "Dvinholm-Freshness-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var firstPath = Path.Combine(dir, "before.bin");
            var secondPath = Path.Combine(dir, "after.bin");
            var jsonPath = Path.Combine(dir, "research.json");
            var first = Body(0x12);
            var second = Body(0x13);
            var json = Encoding.UTF8.GetBytes(ResearchJson());
            File.WriteAllBytes(firstPath, first);
            File.WriteAllBytes(secondPath, second);
            File.WriteAllBytes(jsonPath, json);
            var declarations = Declarations();
            declarations[0] = declarations[0] with {
                ExpectedSha256 = Convert.ToHexString(SHA256.HashData(first)) };
            declarations[1] = declarations[1] with {
                ExpectedSha256 = Convert.ToHexString(SHA256.HashData(second)) };
            declarations[2] = declarations[2] with {
                ExpectedSha256 = Convert.ToHexString(SHA256.HashData(json)) };
            var recheck = await CemOfflineEvidenceLedger.CaptureLocalRecheckAsync(
                firstPath, CemOfflineProvenanceRole.PreviousBody,
                AuditUtc.AddDays(-1));
            var result = await CemConfigurationOfflineResearchPipeline.ReviewFreshnessAsync(
                firstPath, secondPath, jsonPath,
                Part, Revision, declarations,
                Array.Empty<CemOfflineEvidenceLink>(), [recheck],
                AuditUtc, TimeSpan.FromDays(7));
            Assert.Equal(1, result.LocallyConsistentUnverifiedCount);
            Assert.Equal(2, result.NeedsNewLocalReviewCount);
            Assert.False(result.HasAuthenticatedPhysicalCem);
            Assert.False(result.MayReadLiveConfiguration);
            Assert.False(result.MayAcquirePin);
            Assert.False(result.MayWriteConfiguration);
            Assert.Equal(7, result.Ledger.OpenPhysicalEvidenceGates.Count);
            Assert.DoesNotContain(dir, result.CreateRedactedReport(true));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void SourceContractsKeepExportSeparateFromDiagnosticAndRawPayloadPaths()
    {
        var evaluator = Source("src/Dvinholm.App/CemOfflineEvidenceFreshness.cs");
        var exporter = Source("src/Dvinholm.App/CemOfflineEvidenceRedactedExport.cs");
        var pipeline = Source("src/Dvinholm.App/CemConfigurationOfflineResearchPipeline.cs");
        Assert.Contains("CemOfflineEvidenceFreshness.Assess", pipeline);
        Assert.Contains("FileMode.CreateNew", exporter);
        Assert.Contains("MaximumReportBytes", exporter);
        Assert.Contains("PrepareUtf8", exporter);
        Assert.Contains("CreateRedactedReport", exporter);
        foreach (var code in new[] { evaluator, exporter, pipeline })
            foreach (var forbidden in new[]
            {
                "PassThru", "J2534Session", "UdsClient", "SendAsync(",
                "SecurityAccess(", "ReadCemPin(", "WriteConfiguration(",
                "File.WriteAllBytes(", "FileMode.Create,", "FileMode.OpenOrCreate"
            })
                Assert.DoesNotContain(forbidden, code);
        Assert.Empty(CemConfigurationMappingResearch.ProductionMappings);
    }

    private static string Source(string relative)
    {
        for (DirectoryInfo? d = new(AppContext.BaseDirectory);
             d is not null; d = d.Parent)
        {
            var path = Path.Combine(d.FullName,
                relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path)) return File.ReadAllText(path);
        }
        throw new FileNotFoundException(relative);
    }
}
