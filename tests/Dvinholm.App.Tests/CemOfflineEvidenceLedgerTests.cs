using System.Security.Cryptography;
using System.Text.Json;

namespace Dvinholm.App.Tests;

/// <summary>
/// Only synthetic P2 bodies and explicit user-claim metadata. This journal
/// never grants ECU identity, source authentication or CAN/UDS access.
/// </summary>
public sealed class CemOfflineEvidenceLedgerTests
{
    private const string Part = "00000000";
    private const string Revision = "SYNTHETIC-1";
    private static readonly DateTimeOffset OriginalUtc =
        new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset LaterUtc =
        new(2026, 1, 2, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset LatestUtc =
        new(2026, 1, 3, 10, 0, 0, TimeSpan.Zero);
    private static readonly string HashA = new('A', 64);
    private static readonly string HashB = new('B', 64);
    private static readonly string HashC = new('C', 64);

    private static CemOfflineProvenanceDeclaration[] Declarations() =>
    [
        new(CemOfflineProvenanceRole.PreviousBody,
            "user-source-previous", "SYNTHETIC-GROUP", Part, Revision, OriginalUtc, HashA),
        new(CemOfflineProvenanceRole.CurrentBody,
            "user-source-current", "SYNTHETIC-GROUP", Part, Revision, LaterUtc, HashB),
        new(CemOfflineProvenanceRole.ResearchCatalog,
            "user-source-catalog", "SYNTHETIC-GROUP", Part, Revision, LatestUtc, HashC)
    ];

    private static CemOfflineProvenanceAssessment Provenance(
        IReadOnlyList<CemOfflineProvenanceDeclaration>? declarations = null)
    {
        var claim = new CemConfigurationMappingResearchClaim(
            CemConfigurationIndexedFormat.P2B9Fc, 1, Part, Revision,
            "SYNTHETIC ONLY research", "https://example.invalid/research");
        var catalog = new CemConfigurationResearchCatalogDocument(
            CemConfigurationResearchCatalogImport.Format, 1, true, false, false, [claim]);
        var comparison = new CemConfigurationOfflineComparisonResult(
            CemConfigurationIndexedFormat.P2B9Fc, 140, HashA, HashB, [1]);
        var changed = CemConfigurationRevisionResearchTriage.Assess(
            comparison, catalog, Part, Revision);
        var coverage = CemConfigurationResearchCoverage.Assess(
            catalog, CemConfigurationIndexedFormat.P2B9Fc, Part, Revision);
        var combined = CemConfigurationResearchChangeCoverage.Combine(changed, coverage);
        var checkpoint = CemConfigurationOfflineEvidenceChecklist.Assess(combined);
        return CemOfflineProvenanceAudit.Assess(
            declarations ?? Declarations(), HashA, HashB, HashC, Part, Revision, checkpoint);
    }

    private static CemOfflineEvidenceLedgerAssessment Review(
        IReadOnlyList<CemOfflineEvidenceLink>? links = null,
        IReadOnlyList<CemOfflineEvidenceRecheck>? rechecks = null,
        CemOfflineProvenanceDeclaration[]? declarations = null) =>
        CemOfflineEvidenceLedger.Assess(
            declarations ?? Declarations(),
            Provenance(declarations),
            links ?? Array.Empty<CemOfflineEvidenceLink>(),
            rechecks ?? Array.Empty<CemOfflineEvidenceRecheck>());

    [Fact]
    public void EmptyLedgerHasNoAutomaticTrustOrVerifiedLinks()
    {
        var review = Review();
        Assert.Equal(3, review.InitiallyMatchingLocalDigests);
        Assert.Empty(review.LinkResults);
        Assert.Empty(review.RecheckJournal);
        Assert.Equal(7, review.OpenPhysicalEvidenceGates.Count);
        Assert.False(review.HasIndependentSourceAuthentication);
        Assert.False(review.HasVerifiedSamePhysicalCem);
        Assert.False(review.HasVerifiedFirmwareOrOptionMeaning);
        Assert.False(review.MayReadLiveConfiguration);
        Assert.False(review.MayAcquirePin);
        Assert.False(review.MayWriteConfiguration);
        Assert.False(review.IsRestorableBackup);
    }

    [Fact]
    public void SameClaimedModuleAndSoftwareRemainUnverified()
    {
        var review = Review(
        [
            new(CemOfflineProvenanceRole.PreviousBody,
                CemOfflineProvenanceRole.CurrentBody,
                CemOfflineEvidenceLinkClaim.SameClaimedModule),
            new(CemOfflineProvenanceRole.PreviousBody,
                CemOfflineProvenanceRole.ResearchCatalog,
                CemOfflineEvidenceLinkClaim.SameClaimedHardwareAndSoftware)
        ]);
        Assert.Equal(2, review.LinkResults.Count);
        Assert.All(review.LinkResults, item =>
            Assert.Equal(CemOfflineEvidenceLinkFinding.UserClaimTextAgreesUnverified,
                item.Finding));
        Assert.False(review.HasVerifiedSamePhysicalCem);
    }

    [Fact]
    public void ConflictingModuleLabelIsFlaggedForExplicitLink()
    {
        var declared = Declarations();
        declared[1] = declared[1] with {
            ClaimedModuleAssociation = "DIFFERENT-CEM" };
        var result = Review(
            links: [new(CemOfflineProvenanceRole.PreviousBody,
                CemOfflineProvenanceRole.CurrentBody,
                CemOfflineEvidenceLinkClaim.SameClaimedModule)],
            declarations: declared);
        Assert.Equal(1, result.LinkTextConflictCount);
        Assert.Equal(CemOfflineEvidenceLinkFinding.UserClaimTextConflicts,
            result.LinkResults[0].Finding);
    }

    [Fact]
    public void ConflictingSoftwareClaimIsNotAProvenVehicleMismatch()
    {
        var declared = Declarations();
        declared[1] = declared[1] with {
            ClaimedSoftwareRevision = "SYNTHETIC-2" };
        var result = Review(
            links: [new(CemOfflineProvenanceRole.PreviousBody,
                CemOfflineProvenanceRole.CurrentBody,
                CemOfflineEvidenceLinkClaim.SameClaimedHardwareAndSoftware)],
            declarations: declared);
        Assert.Equal(1, result.LinkTextConflictCount);
        Assert.False(result.HasVerifiedFirmwareOrOptionMeaning);
    }

    [Fact]
    public void IdenticalClaimedSourceLabelIsNotIndependentEvidence()
    {
        var declared = Declarations();
        declared[1] = declared[1] with {
            SourceReference = declared[0].SourceReference };
        var result = Review(
            links: [new(CemOfflineProvenanceRole.PreviousBody,
                CemOfflineProvenanceRole.CurrentBody,
                CemOfflineEvidenceLinkClaim.DistinctClaimedSource)],
            declarations: declared);
        Assert.Equal(1, result.SharedSourceLabelCount);
        Assert.Equal(CemOfflineEvidenceLinkFinding.SameUserSourceLabelNotIndependent,
            result.LinkResults[0].Finding);
        Assert.False(result.HasIndependentSourceAuthentication);
    }

    [Fact]
    public void DifferentSourceLabelsStillDoNotProveIndependence()
    {
        var review = Review(
            links: [new(CemOfflineProvenanceRole.PreviousBody,
                CemOfflineProvenanceRole.ResearchCatalog,
                CemOfflineEvidenceLinkClaim.DistinctClaimedSource)]);
        Assert.Equal(CemOfflineEvidenceLinkFinding.DistinctUserSourceLabelsUnattested,
            Assert.Single(review.LinkResults).Finding);
        Assert.False(review.HasIndependentSourceAuthentication);
    }

    [Fact]
    public void ReversedDuplicateLinkAndSelfLinkFailClosed()
    {
        var first = new CemOfflineEvidenceLink(
            CemOfflineProvenanceRole.PreviousBody,
            CemOfflineProvenanceRole.CurrentBody,
            CemOfflineEvidenceLinkClaim.SameClaimedModule);
        var reversed = first with {
            FirstRole = first.SecondRole, SecondRole = first.FirstRole };
        Assert.Throws<InvalidDataException>(() => Review(links: [first, reversed]));
        Assert.Throws<InvalidDataException>(() => Review(links: [first with {
            SecondRole = first.FirstRole }]));
    }

    [Fact]
    public void UnknownRoleOrLinkClaimIsRejected()
    {
        Assert.Throws<InvalidDataException>(() =>
            Review(links: [new((CemOfflineProvenanceRole)777,
                CemOfflineProvenanceRole.CurrentBody,
                CemOfflineEvidenceLinkClaim.SameClaimedModule)]));
        Assert.Throws<InvalidDataException>(() =>
            Review(links: [new(CemOfflineProvenanceRole.PreviousBody,
                CemOfflineProvenanceRole.CurrentBody,
                (CemOfflineEvidenceLinkClaim)777)]));
        Assert.Throws<InvalidDataException>(() =>
            Review(rechecks: [new((CemOfflineProvenanceRole)777, LaterUtc, HashA)]));
    }

    [Fact]
    public void RecheckJournalRecordsMatchingThenMismatchingDigestAndDrift()
    {
        var review = Review(rechecks:
        [
            new(CemOfflineProvenanceRole.PreviousBody, LaterUtc, HashA),
            new(CemOfflineProvenanceRole.CurrentBody, LatestUtc, HashB),
            new(CemOfflineProvenanceRole.PreviousBody, LatestUtc, HashC)
        ]);
        Assert.Equal(3, review.RecheckJournal.Count);
        Assert.Equal(2, review.RecheckJournal[^1].OrdinalForRole);
        Assert.True(review.RecheckJournal[0].MatchesOriginallyDeclaredSha256);
        Assert.False(review.RecheckJournal[^1].MatchesOriginallyDeclaredSha256);
        Assert.True(review.RecheckJournal[^1].DigestDiffersFromPriorRecheck);
        Assert.Equal(1, review.RepeatLocalHashMismatchCount);
        Assert.Equal(1, review.RecheckDigestChangedCount);
        Assert.False(review.HasVerifiedSamePhysicalCem);
    }

    [Fact]
    public void RecheckClaimBeforeOriginalTimeIsAFlagNotARealClockProof()
    {
        var result = Review(rechecks: [
            new(CemOfflineProvenanceRole.CurrentBody, OriginalUtc, HashB)]);
        Assert.Equal(1, result.ClaimedRecheckChronologyIssueCount);
        Assert.False(result.RecheckJournal[0].ClaimedTimeAfterOriginalObservation);
    }

    [Fact]
    public void RepeatedUtcTimestampOrDescendingHistoryIsRejected()
    {
        Assert.Throws<InvalidDataException>(() => Review(rechecks:
        [
            new(CemOfflineProvenanceRole.PreviousBody, LaterUtc, HashA),
            new(CemOfflineProvenanceRole.PreviousBody, LaterUtc, HashB)
        ]));
        Assert.Throws<InvalidDataException>(() => Review(rechecks:
        [
            new(CemOfflineProvenanceRole.PreviousBody, LatestUtc, HashA),
            new(CemOfflineProvenanceRole.PreviousBody, LaterUtc, HashB)
        ]));
    }

    [Fact]
    public void MalformedDigestOrNonUtcRecheckIsRejected()
    {
        Assert.Throws<InvalidDataException>(() =>
            Review(rechecks: [new(CemOfflineProvenanceRole.PreviousBody,
                LaterUtc, new string('a', 64))]));
        Assert.Throws<InvalidDataException>(() =>
            Review(rechecks: [new(CemOfflineProvenanceRole.PreviousBody,
                new DateTimeOffset(2026, 1, 2, 10, 0, 0, TimeSpan.FromHours(3)),
                HashA)]));
    }

    [Fact]
    public void MoreThan64RechecksOrTwelveLinksIsRejectedBeforeReview()
    {
        var tooMany = Enumerable.Range(0, 65).Select(i =>
            new CemOfflineEvidenceRecheck(
                CemOfflineProvenanceRole.PreviousBody,
                LaterUtc.AddSeconds(i), HashA)).ToArray();
        Assert.Throws<InvalidDataException>(() => Review(rechecks: tooMany));
        var link = new CemOfflineEvidenceLink(
            CemOfflineProvenanceRole.PreviousBody,
            CemOfflineProvenanceRole.CurrentBody,
            CemOfflineEvidenceLinkClaim.SameClaimedModule);
        Assert.Throws<InvalidDataException>(() =>
            Review(links: Enumerable.Repeat(link, 13).ToArray()));
    }

    [Fact]
    public void BilingualJournalRedactsRawClaimAndHashes()
    {
        var review = Review(
            links: [new(CemOfflineProvenanceRole.PreviousBody,
                CemOfflineProvenanceRole.ResearchCatalog,
                CemOfflineEvidenceLinkClaim.DistinctClaimedSource)],
            rechecks: [new(CemOfflineProvenanceRole.PreviousBody, LaterUtc, HashC)]);
        var en = review.CreateRedactedReport(true);
        var ru = review.CreateRedactedReport(false);
        Assert.Contains("Repeat local checks: 1", en);
        Assert.Contains("declared local hash MISMATCH", en);
        Assert.Contains("Повторных локальных проверок: 1", ru);
        Assert.Contains("ОТКРЫТО 7/7", ru);
        foreach (var report in new[] { ru, en })
        {
            Assert.DoesNotContain(Part, report);
            Assert.DoesNotContain(Revision, report);
            Assert.DoesNotContain("SYNTHETIC-GROUP", report);
            Assert.DoesNotContain("user-source-", report);
            Assert.DoesNotContain("2026-01-02", report);
            Assert.DoesNotContain(new string('A', 64), report);
            Assert.DoesNotContain(new string('B', 64), report);
            Assert.DoesNotContain(new string('C', 64), report);
            Assert.DoesNotContain("https://example.invalid", report);
        }
    }

    private static byte[] Body(int value)
    {
        var body = new byte[142];
        body[0] = 140;
        body[2] = checked((byte)value);
        body[^1] = 0x20;
        var checksum = 0;
        foreach (var b in body.AsSpan(2))
            checksum = (checksum + b) & 255;
        body[1] = (byte)checksum;
        return body;
    }

    private static string SyntheticJson() => JsonSerializer.Serialize(new
    {
        format = CemConfigurationResearchCatalogImport.Format,
        schemaVersion = 1, isUnverifiedResearch = true,
        isRestorableBackup = false, mayAccessEcu = false,
        claims = new[] { new {
            dataFormat = "P2B9Fc", index1Based = 1,
            hardwarePartNumber = Part, softwareRevision = Revision,
            claimedMeaning = "SYNTHETIC ONLY research",
            evidenceReference = "https://example.invalid/research"
        }}
    });

    [Fact]
    public async Task FreshLocalRecheckHashesValidatedP2BodyAndRejectsBadChecksum()
    {
        var path = Path.Combine(Path.GetTempPath(),
            "Dvinholm-Recheck-" + Guid.NewGuid().ToString("N") + ".bin");
        try
        {
            var good = Body(0x12);
            File.WriteAllBytes(path, good);
            var captured = await CemOfflineEvidenceLedger.CaptureLocalRecheckAsync(
                path, CemOfflineProvenanceRole.PreviousBody, LaterUtc);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(good)),
                captured.LocallyObservedSha256);
            var bad = (byte[])good.Clone();
            bad[1] ^= 1;
            File.WriteAllBytes(path, bad);
            await Assert.ThrowsAsync<FormatException>(() =>
                CemOfflineEvidenceLedger.CaptureLocalRecheckAsync(
                    path, CemOfflineProvenanceRole.PreviousBody, LatestUtc));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ResearchJsonRecheckParsesStrictCatalogAndRejectsUnknownClaims()
    {
        var path = Path.Combine(Path.GetTempPath(),
            "Dvinholm-Recheck-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(path, SyntheticJson());
            var captured = await CemOfflineEvidenceLedger.CaptureLocalRecheckAsync(
                path, CemOfflineProvenanceRole.ResearchCatalog, LatestUtc);
            Assert.Equal(64, captured.LocallyObservedSha256.Length);
            File.WriteAllText(path, "{\"unsafe\":true}");
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                CemOfflineEvidenceLedger.CaptureLocalRecheckAsync(
                    path, CemOfflineProvenanceRole.ResearchCatalog, LatestUtc));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ThreeFileEndToEndLedgerIsStillOnlyLocalEvidence()
    {
        var dir = Path.Combine(Path.GetTempPath(),
            "Dvinholm-Ledger-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var previous = Path.Combine(dir, "previous.bin");
            var current = Path.Combine(dir, "current.bin");
            var json = Path.Combine(dir, "research.json");
            var p = Body(0x12);
            var c = Body(0x13);
            var j = System.Text.Encoding.UTF8.GetBytes(SyntheticJson());
            File.WriteAllBytes(previous, p);
            File.WriteAllBytes(current, c);
            File.WriteAllBytes(json, j);
            var declarations = Declarations();
            declarations[0] = declarations[0] with {
                ExpectedSha256 = Convert.ToHexString(SHA256.HashData(p)) };
            declarations[1] = declarations[1] with {
                ExpectedSha256 = Convert.ToHexString(SHA256.HashData(c)) };
            declarations[2] = declarations[2] with {
                ExpectedSha256 = Convert.ToHexString(SHA256.HashData(j)) };
            var recheck = await CemOfflineEvidenceLedger.CaptureLocalRecheckAsync(
                previous, CemOfflineProvenanceRole.PreviousBody, LaterUtc);
            var result = await CemConfigurationOfflineResearchPipeline.ReviewEvidenceLedgerAsync(
                previous, current, json, Part, Revision, declarations,
                [new(CemOfflineProvenanceRole.PreviousBody,
                    CemOfflineProvenanceRole.CurrentBody,
                    CemOfflineEvidenceLinkClaim.SameClaimedModule)],
                [recheck]);
            Assert.Equal(3, result.InitiallyMatchingLocalDigests);
            Assert.Equal(1, result.RecheckJournal.Count);
            Assert.Equal(0, result.RepeatLocalHashMismatchCount);
            Assert.False(result.HasVerifiedSamePhysicalCem);
            Assert.Equal(7, result.OpenPhysicalEvidenceGates.Count);
            Assert.DoesNotContain(dir, result.CreateRedactedReport(false));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void SourceContractHasNoDiagnosticOperationOrJournalDiskWrite()
    {
        var ledger = Source("src/Dvinholm.App/CemOfflineEvidenceLedger.cs");
        var pipeline = Source("src/Dvinholm.App/CemConfigurationOfflineResearchPipeline.cs");
        Assert.Contains("ReviewEvidenceLedgerAsync", pipeline);
        Assert.Contains("CemOfflineEvidenceLedger.Assess", pipeline);
        Assert.Contains("CemOfflineProvenanceAudit.ReadLocalSha256Async", ledger);
        Assert.Contains("CemConfigurationOfflineBlockInspection.InspectFileAsync", ledger);
        Assert.Contains("CemConfigurationResearchCatalogImport.InspectFileAsync", ledger);
        foreach (var code in new[] { ledger, pipeline })
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
            var file = Path.Combine(dir.FullName,
                relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(file)) return File.ReadAllText(file);
        }
        throw new FileNotFoundException(relative);
    }
}
