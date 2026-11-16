using System.Security.Cryptography;
using System.Text.Json;

namespace Dvinholm.App.Tests;

/// <summary>
/// Three synthetic local artifacts and user-transcribed provenance claims.
/// SHA-256 is equality evidence only, never physical CEM attestation.
/// </summary>
public sealed class CemOfflineProvenanceAuditTests
{
    private const string Part = "00000000";
    private const string Rev = "SYNTHETIC-1";
    private const string Group = "CLAIMED-CEM-001";
    private static readonly DateTimeOffset FirstTime =
        new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset SecondTime =
        new(2026, 1, 2, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CatalogTime =
        new(2026, 1, 3, 8, 0, 0, TimeSpan.Zero);

    private static CemOfflineProvenanceDeclaration[] Claims(
        string before, string after, string catalog) =>
        [
            new(CemOfflineProvenanceRole.PreviousBody, "user-file-1",
                Group, Part, Rev, FirstTime, before),
            new(CemOfflineProvenanceRole.CurrentBody, "user-file-2",
                Group, Part, Rev, SecondTime, after),
            new(CemOfflineProvenanceRole.ResearchCatalog, "user-research-3",
                Group, Part, Rev, CatalogTime, catalog)
        ];

    private static CemConfigurationOfflineEvidenceChecklistAssessment Checkpoint()
    {
        var research = new CemConfigurationMappingResearchClaim(
            CemConfigurationIndexedFormat.P2B9Fc, 1, Part, Rev,
            "SYNTHETIC research claim", "https://example.invalid/provenance");
        var catalog = new CemConfigurationResearchCatalogDocument(
            CemConfigurationResearchCatalogImport.Format, 1, true, false, false,
            [research]);
        var diff = new CemConfigurationOfflineComparisonResult(
            CemConfigurationIndexedFormat.P2B9Fc, 140,
            new string('A', 64), new string('B', 64), [1]);
        var changed = CemConfigurationRevisionResearchTriage.Assess(
            diff, catalog, Part, Rev);
        var coverage = CemConfigurationResearchCoverage.Assess(
            catalog, CemConfigurationIndexedFormat.P2B9Fc, Part, Rev);
        var combined = CemConfigurationResearchChangeCoverage.Combine(changed, coverage);
        return CemConfigurationOfflineEvidenceChecklist.Assess(combined);
    }

    private static CemOfflineProvenanceAssessment Audit(
        CemOfflineProvenanceDeclaration[]? declarations = null)
    {
        var a = new string('A', 64);
        var b = new string('B', 64);
        var c = new string('C', 64);
        return CemOfflineProvenanceAudit.Assess(declarations ?? Claims(a, b, c),
            a, b, c, Part, Rev, Checkpoint());
    }

    [Fact]
    public void AllLocalDigestsMayMatchButSevenPhysicalGatesStayOpen()
    {
        var result = Audit();
        Assert.True(result.AllThreeDeclaredFingerprintsMatch);
        Assert.Equal(3, result.LocallyMatchingFingerprintCount);
        Assert.True(result.ClaimedModuleAssociationTextsAgree);
        Assert.True(result.ClaimedHardwareSoftwareTextsAgree);
        Assert.True(result.ClaimedObservationOrderPlausible);
        Assert.Equal(7, result.OpenPhysicalEvidenceGates.Count);
        Assert.False(result.HasIndependentlyAuthenticatedOrigin);
        Assert.False(result.HasVerifiedSamePhysicalCem);
        Assert.False(result.HasVerifiedFirmwareRevision);
        Assert.False(result.HasVerifiedOptionMeanings);
        Assert.False(result.MayReadLiveConfiguration);
        Assert.False(result.MayAcquirePin);
        Assert.False(result.MayWriteConfiguration);
        Assert.False(result.IsRestorableBackup);
    }

    [Fact]
    public void DeclaredMismatchReturnsFailedLocalDigestWithoutAuthentication()
    {
        var a = new string('A', 64);
        var b = new string('B', 64);
        var c = new string('C', 64);
        var declared = Claims(a, b, c);
        declared[2] = declared[2] with { ExpectedSha256 = new string('D', 64) };
        var result = Audit(declared);
        Assert.Equal(2, result.LocallyMatchingFingerprintCount);
        Assert.False(result.AllThreeDeclaredFingerprintsMatch);
        Assert.False(result.HasIndependentlyAuthenticatedOrigin);
        Assert.Contains("DOES NOT MATCH", result.CreateRedactedReport(true));
    }

    [Fact]
    public void DuplicateAndMissingRolesAreRejectedBeforeAnyFileRead()
    {
        var claims = Claims(new string('A', 64), new string('B', 64), new string('C', 64));
        claims[2] = claims[0];
        Assert.Throws<InvalidDataException>(() =>
            CemOfflineProvenanceAudit.ValidateDeclarations(claims));
        Assert.Throws<InvalidDataException>(() =>
            CemOfflineProvenanceAudit.ValidateDeclarations(claims[..2]));
    }

    [Fact]
    public void MalformedHashesAndUnsafeSourceLabelsAreRejected()
    {
        var claims = Claims(new string('A', 64), new string('B', 64), new string('C', 64));
        claims[0] = claims[0] with { ExpectedSha256 = new string('a', 64) };
        Assert.Throws<InvalidDataException>(() =>
            CemOfflineProvenanceAudit.ValidateDeclarations(claims));
        claims[0] = claims[0] with {
            ExpectedSha256 = new string('A', 64), SourceReference = "bad\nsource" };
        Assert.Throws<InvalidDataException>(() =>
            CemOfflineProvenanceAudit.ValidateDeclarations(claims));
    }

    [Fact]
    public void NonUtcOrImplausibleClaimedTimestampIsRejected()
    {
        var claims = Claims(new string('A', 64), new string('B', 64), new string('C', 64));
        claims[0] = claims[0] with {
            ClaimedObservedUtc = new DateTimeOffset(2026, 1, 1, 8, 0, 0,
                TimeSpan.FromHours(3)) };
        Assert.Throws<InvalidDataException>(() =>
            CemOfflineProvenanceAudit.ValidateDeclarations(claims));
        claims[0] = claims[0] with {
            ClaimedObservedUtc = new DateTimeOffset(1999, 1, 1, 8, 0, 0, TimeSpan.Zero) };
        Assert.Throws<InvalidDataException>(() =>
            CemOfflineProvenanceAudit.ValidateDeclarations(claims));
    }

    [Fact]
    public void DifferentModuleLabelOrHardwareSoftwareClaimCannotProveSameCem()
    {
        var claims = Claims(new string('A', 64), new string('B', 64), new string('C', 64));
        claims[1] = claims[1] with { ClaimedModuleAssociation = "DIFFERENT" };
        claims[2] = claims[2] with { ClaimedSoftwareRevision = "SYNTHETIC-2" };
        var result = Audit(claims);
        Assert.False(result.ClaimedModuleAssociationTextsAgree);
        Assert.False(result.ClaimedHardwareSoftwareTextsAgree);
        Assert.False(result.HasVerifiedSamePhysicalCem);
    }

    [Fact]
    public void ReversedClaimedCaptureOrderIsFlaggedNotAcceptedAsChronology()
    {
        var claims = Claims(new string('A', 64), new string('B', 64), new string('C', 64));
        claims[1] = claims[1] with {
            ClaimedObservedUtc = FirstTime.AddDays(-1) };
        Assert.False(Audit(claims).ClaimedObservationOrderPlausible);
    }

    [Fact]
    public void ReportRedactsEveryIdentifierAndTimestampInBothLanguages()
    {
        var assessment = Audit();
        var ru = assessment.CreateRedactedReport(false);
        var en = assessment.CreateRedactedReport(true);
        Assert.Contains("OPEN 7/7", en);
        Assert.Contains("ОТКРЫТО 7/7", ru);
        Assert.Contains("3/3", en);
        Assert.Contains("3/3", ru);
        foreach (var output in new[] { ru, en })
        {
            Assert.DoesNotContain(Group, output);
            Assert.DoesNotContain(Part, output);
            Assert.DoesNotContain(Rev, output);
            Assert.DoesNotContain("user-file-1", output);
            Assert.DoesNotContain("2026-01-01", output);
            Assert.DoesNotContain(new string('A', 64), output);
            Assert.DoesNotContain(new string('B', 64), output);
            Assert.DoesNotContain(new string('C', 64), output);
            Assert.DoesNotContain("https://example.invalid", output);
        }
    }

    private static byte[] Body()
    {
        var body = new byte[142];
        body[0] = 140;
        body[2] = 0x10;
        body[^1] = 0x20;
        var sum = 0;
        foreach (var b in body.AsSpan(2)) sum = (sum + b) & 255;
        body[1] = (byte)sum;
        return body;
    }

    private static string ResearchJson() => JsonSerializer.Serialize(new {
        format = CemConfigurationResearchCatalogImport.Format,
        schemaVersion = 1, isUnverifiedResearch = true,
        isRestorableBackup = false, mayAccessEcu = false,
        claims = new[] { new {
            dataFormat = "P2B9Fc", index1Based = 1,
            hardwarePartNumber = Part, softwareRevision = Rev,
            claimedMeaning = "SYNTHETIC research claim",
            evidenceReference = "https://example.invalid/provenance"
        }}
    });

    [Fact]
    public async Task EndToEndThreeFileProvenanceChecksActualBytesButNotCemOrigin()
    {
        var root = Path.Combine(Path.GetTempPath(),
            "Dvinholm-Prov-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var first = Path.Combine(root, "previous.bin");
            var second = Path.Combine(root, "current.bin");
            var json = Path.Combine(root, "claims.json");
            var old = Body();
            var updated = (byte[])old.Clone();
            updated[2] ^= 0x01;
            var sum = 0;
            foreach (var b in updated.AsSpan(2)) sum = (sum + b) & 255;
            updated[1] = (byte)sum;
            var catalog = System.Text.Encoding.UTF8.GetBytes(ResearchJson());
            File.WriteAllBytes(first, old);
            File.WriteAllBytes(second, updated);
            File.WriteAllBytes(json, catalog);

            var declaration = Claims(Convert.ToHexString(SHA256.HashData(old)),
                Convert.ToHexString(SHA256.HashData(updated)),
                Convert.ToHexString(SHA256.HashData(catalog)));
            var result = await CemConfigurationOfflineResearchPipeline.ReviewProvenanceAsync(
                first, second, json, Part, Rev, declaration);
            Assert.True(result.AllThreeDeclaredFingerprintsMatch);
            Assert.Equal(1, result.ChangedRawIndices);
            Assert.Equal(7, result.OpenPhysicalEvidenceGates.Count);
            Assert.False(result.HasVerifiedSamePhysicalCem);
            Assert.DoesNotContain(root, result.CreateRedactedReport(true));
            Assert.True(old.SequenceEqual(File.ReadAllBytes(first)));
            Assert.True(updated.SequenceEqual(File.ReadAllBytes(second)));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task InvalidDeclarationFailsBeforeMissingFilesAreOpened()
    {
        var claims = Claims(new string('A', 64), new string('B', 64), new string('C', 64));
        claims[0] = claims[0] with { ClaimedModuleAssociation = "bad module!" };
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            CemConfigurationOfflineResearchPipeline.ReviewProvenanceAsync(
                "not-present.bin", "also-missing.bin", "not-present.json",
                Part, Rev, claims));
    }

    [Fact]
    public void PipelineDoesNotCreateAServiceOrPersistRawEvidence()
    {
        var source = Source("src/Dvinholm.App/CemOfflineProvenanceAudit.cs");
        var pipeline = Source("src/Dvinholm.App/CemConfigurationOfflineResearchPipeline.cs");
        Assert.Contains("ReadLocalSha256Async", source);
        Assert.Contains("ReviewProvenanceAsync", pipeline);
        Assert.Contains("CemConfigurationOfflineEvidenceChecklist.Assess", pipeline);
        foreach (var code in new[] { source, pipeline })
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
