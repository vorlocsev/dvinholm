using System.Text.Json;

namespace Dvinholm.App.Tests;

/// <summary>
/// Checklist boundaries on synthetic CEM research only. Optional session
/// context MUST NOT authenticate the uncorrelated local file pair.
/// </summary>
public sealed class CemConfigurationOfflineEvidenceChecklistTests
{
    private const string Part = "00000000";
    private const string Revision = "SYNTHETIC-1";

    private static CemConfigurationMappingResearchClaim Claim(
        int index, CemConfigurationIndexedFormat format = CemConfigurationIndexedFormat.P2B9Fc,
        string revision = Revision) =>
        new(format, index, Part, revision,
            "SYNTHETIC local-only research",
            "https://example.invalid/synthetic-evidence");

    private static CemConfigurationResearchCatalogDocument Catalog(
        params CemConfigurationMappingResearchClaim[] claims) =>
        new(CemConfigurationResearchCatalogImport.Format, 1, true,
            false, false, Array.AsReadOnly(claims));

    private static CemConfigurationOfflineEvidenceChecklistAssessment Review(
        CemConfigurationIndexedFormat format = CemConfigurationIndexedFormat.P2B9Fc,
        int[]? changed = null,
        CemIdentityEvidenceAssessment? observation = null,
        CemReadOnlyPrerequisiteAssessment? prerequisites = null)
    {
        changed ??= [1, 140];
        var catalog = Catalog(Claim(1));
        var body = new CemConfigurationOfflineComparisonResult(
            format, format == CemConfigurationIndexedFormat.P2B9Fc ? 140 : 252,
            new string('A', 64),
            changed.Length == 0 ? new string('A', 64) : new string('B', 64),
            changed);
        var index = CemConfigurationRevisionResearchTriage.Assess(
            body, catalog, Part, Revision);
        var coverage = CemConfigurationResearchCoverage.Assess(
            catalog, format, Part, Revision);
        var combined = CemConfigurationResearchChangeCoverage.Combine(index, coverage);
        return CemConfigurationOfflineEvidenceChecklist.Assess(
            combined, observation, prerequisites);
    }

    [Fact]
    public void P2StructuralReviewKeepsEveryIndependentEvidenceGateOpen()
    {
        var result = Review();
        Assert.Equal(CemConfigurationIndexedFormat.P2B9Fc, result.Format);
        Assert.Equal(140, result.TotalRawOrdinals);
        Assert.Equal(2, result.ChangedRawOrdinals);
        Assert.Equal(1, result.ChangedWithoutExactResearchClaim);
        Assert.Equal(7, result.OpenGates.Count);
        Assert.Equal(Enum.GetValues<CemConfigurationEvidenceGap>(), result.OpenGates);
        Assert.False(result.HasAuthenticatedPhysicalCem);
        Assert.False(result.HasVerifiedOptionMeanings);
        Assert.False(result.HasVerifiedSameModuleProvenance);
        Assert.False(result.MayReadLiveConfiguration);
        Assert.False(result.MayAcquirePin);
        Assert.False(result.MayWriteConfiguration);
        Assert.False(result.IsRestorableBackup);
    }

    [Fact]
    public void P3IsNotMistakenForP2AndNeverGetsHardwareAuthorization()
    {
        var result = Review(CemConfigurationIndexedFormat.P3F106, [252]);
        Assert.Equal(CemConfigurationIndexedFormat.P3F106, result.Format);
        Assert.Equal(252, result.TotalRawOrdinals);
        Assert.Equal(1, result.ChangedWithoutExactResearchClaim);
        Assert.Equal(7, result.OpenGates.Count);
        Assert.False(result.MayWriteConfiguration);
    }

    [Fact]
    public void EqualBodiesStillNeedSevenIndependentEvidenceGates()
    {
        var result = Review(changed: []);
        Assert.Equal(0, result.ChangedRawOrdinals);
        Assert.Equal(0, result.ChangedWithoutExactResearchClaim);
        Assert.Equal(7, result.OpenGates.Count);
        Assert.False(result.HasVerifiedSameModuleProvenance);
    }

    [Fact]
    public void UnattestedPartObservationIsUncorrelatedAndDoesNotCloseAnyGate()
    {
        var observation = CemIdentityEvidenceReport.Assess(
            30_682_981, guardCurrent: true,
            sessionConnected: true, developmentContext: true);
        var result = Review(observation: observation);
        Assert.True(result.HasUncorrelatedUnattestedPartNumberObservation);
        Assert.Equal(7, result.OpenGates.Count);
        Assert.False(result.HasAuthenticatedPhysicalCem);
        Assert.False(result.MayReadLiveConfiguration);
        Assert.False(result.MayAcquirePin);
    }

    [Fact]
    public void RevokedSessionGuardDoesNotCreateAnObservation()
    {
        var observation = CemIdentityEvidenceReport.Assess(
            30_682_981, guardCurrent: false,
            sessionConnected: true, developmentContext: true);
        var result = Review(observation: observation);
        Assert.False(result.HasUncorrelatedUnattestedPartNumberObservation);
    }

    [Fact]
    public void AllEightSessionPrerequisitesStillCannotProveFileProvenance()
    {
        var prerequisites = CemReadOnlyPrerequisiteReport.Assess(
            new(true, true, true, true, true, true, true, true));
        var result = Review(prerequisites: prerequisites);
        Assert.True(result.HasUncorrelatedSatisfiedSessionPrerequisites);
        Assert.Equal(7, result.OpenGates.Count);
        Assert.False(result.HasVerifiedSameModuleProvenance);
        Assert.False(result.MayReadLiveConfiguration);
        Assert.False(result.MayWriteConfiguration);
    }

    [Fact]
    public void UnknownIdentityKindFailsClosed()
    {
        Assert.Throws<InvalidDataException>(() => Review(
            observation: new CemIdentityEvidenceAssessment(
                (CemIdentityEvidenceKind)999)));
    }

    [Fact]
    public void ForgedPrerequisiteChecklistFailsClosed()
    {
        var duplicateChecks = Array.AsReadOnly(Enumerable.Repeat(
            new CemReadOnlyPrerequisiteCheck(
                CemReadOnlyPrerequisiteKind.Connected, true), 8).ToArray());
        Assert.Throws<InvalidDataException>(() => Review(
            prerequisites: new CemReadOnlyPrerequisiteAssessment(duplicateChecks)));
    }

    [Fact]
    public void RuEnReportsDoNotLeakPartNumbersPathsOrResearchText()
    {
        var observation = CemIdentityEvidenceReport.Assess(
            30_682_981, true, true, true);
        var review = Review(observation: observation);
        var en = review.CreateRedactedReport(english: true);
        var ru = review.CreateRedactedReport(english: false);
        Assert.Contains("OPEN 7/7", en);
        Assert.Contains("ОТКРЫТО 7/7", ru);
        Assert.Contains("UNCORRELATED", en);
        Assert.Contains("НЕ СВЯЗАН", ru);
        Assert.Contains("No proof that the two files", en);
        Assert.Contains("НЕ резервная копия", ru);
        foreach (var report in new[] { en, ru })
        {
            Assert.DoesNotContain(Part, report);
            Assert.DoesNotContain(Revision, report);
            Assert.DoesNotContain("30682981", report);
            Assert.DoesNotContain("https://example.invalid", report);
            Assert.DoesNotContain("SYNTHETIC local-only", report);
            Assert.DoesNotContain(new string('A', 64), report);
            Assert.DoesNotContain("0x27", report);
        }
    }

    private static byte[] Body(int count)
    {
        var value = new byte[count + 2];
        value[0] = (byte)count;
        value[2] = 0x12;
        value[^1] = 0x34;
        var sum = 0;
        foreach (var byteValue in value.AsSpan(2))
            sum = (sum + byteValue) & 255;
        value[1] = (byte)sum;
        return value;
    }

    private static string SyntheticJson() => JsonSerializer.Serialize(new
    {
        format = CemConfigurationResearchCatalogImport.Format,
        schemaVersion = 1,
        isUnverifiedResearch = true,
        isRestorableBackup = false,
        mayAccessEcu = false,
        claims = new[] { new
        {
            dataFormat = "P2B9Fc", index1Based = 1,
            hardwarePartNumber = Part, softwareRevision = Revision,
            claimedMeaning = "SYNTHETIC local-only research",
            evidenceReference = "https://example.invalid/synthetic-evidence"
        }}
    });

    [Fact]
    public async Task ThreeFilePipelineBuildsRedactedUncorrelatedEvidenceChecklist()
    {
        var folder = Path.Combine(Path.GetTempPath(),
            "Dvinholm-Cem-Check-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var first = Path.Combine(folder, "old.bin");
            var second = Path.Combine(folder, "new.bin");
            var research = Path.Combine(folder, "research.json");
            var prior = Body(140);
            var next = (byte[])prior.Clone();
            next[2] ^= 0x10;
            var sum = 0;
            foreach (var b in next.AsSpan(2))
                sum = (sum + b) & 255;
            next[1] = (byte)sum;
            File.WriteAllBytes(first, prior);
            File.WriteAllBytes(second, next);
            File.WriteAllText(research, SyntheticJson());

            var result = await CemConfigurationOfflineResearchPipeline
                .ReviewEvidenceChecklistAsync(first, second, research, Part, Revision);
            Assert.Equal(1, result.ChangedRawOrdinals);
            Assert.Equal(0, result.ChangedWithoutExactResearchClaim);
            Assert.Equal(7, result.OpenGates.Count);
            Assert.False(result.HasUncorrelatedUnattestedPartNumberObservation);
            Assert.False(result.HasUncorrelatedSatisfiedSessionPrerequisites);
            var report = result.CreateRedactedReport(true);
            Assert.DoesNotContain(folder, report);
            Assert.DoesNotContain(Part, report);
            Assert.Contains("No diagnostic request was sent", report);
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    [Fact]
    public void PipelineHasNoExternalDiagnosticOrWritePath()
    {
        var checklist = Source("src/Dvinholm.App/CemConfigurationOfflineEvidenceChecklist.cs");
        var pipeline = Source("src/Dvinholm.App/CemConfigurationOfflineResearchPipeline.cs");
        Assert.Contains("ReviewEvidenceChecklistAsync", pipeline);
        Assert.Contains("ReviewCoverageAndChangesAsync", pipeline);
        Assert.Contains("CemConfigurationOfflineEvidenceChecklist.Assess", pipeline);
        Assert.Contains("CemIdentityEvidenceReport.Format", checklist);
        Assert.Contains("CemReadOnlyPrerequisiteReport.Format", checklist);
        foreach (var code in new[] { checklist, pipeline })
            foreach (var prohibited in new[] {
                "PassThru", "J2534Session", "UdsClient", "SendAsync(",
                "SecurityAccess(", "ReadCemPin(", "WriteConfiguration(",
                "File.WriteAllBytes(", "FileMode.Create", "FileAccess.Write"
            })
                Assert.DoesNotContain(prohibited, code);
        Assert.Empty(CemConfigurationMappingResearch.ProductionMappings);
    }

    private static string Source(string relative)
    {
        for (DirectoryInfo? current = new(AppContext.BaseDirectory);
             current is not null; current = current.Parent)
        {
            var path = Path.Combine(current.FullName,
                relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path)) return File.ReadAllText(path);
        }
        throw new FileNotFoundException(relative);
    }
}
