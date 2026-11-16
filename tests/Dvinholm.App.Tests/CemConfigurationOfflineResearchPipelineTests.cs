using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Dvinholm.App.Tests;

/// <summary>
/// Complete synthetic local-only pipeline. Tests never read a vehicle,
/// connect to a pass-through adapter or load proprietary catalog bytes.
/// </summary>
public sealed class CemConfigurationOfflineResearchPipelineTests
{
    private static string SyntheticJson(
        string revision = "SYNTHETIC-1",
        string part = "00000000",
        string dataFormat = "P2B9Fc",
        int index = 1)
    {
        return JsonSerializer.Serialize(new
        {
            format = CemConfigurationResearchCatalogImport.Format,
            schemaVersion = 1,
            isUnverifiedResearch = true,
            isRestorableBackup = false,
            mayAccessEcu = false,
            claims = new[]
            {
                new
                {
                    dataFormat, index1Based = index,
                    hardwarePartNumber = part,
                    softwareRevision = revision,
                    claimedMeaning = "SYNTHETIC ONLY no real CEM option",
                    evidenceReference = "https://example.invalid/synthetic-test"
                }
            }
        });
    }

    private static string TemporaryPath(string extension = ".bin") =>
        Path.Combine(Path.GetTempPath(),
            "Dvinholm-P2P3-E2E-" + Guid.NewGuid().ToString("N") + extension);

    private static byte[] Body(int count)
    {
        var body = new byte[count + 2];
        body[0] = checked((byte)count);
        body[2] = 0x12;
        body[^1] = 0x34;
        Rechecksum(body);
        return body;
    }

    private static void Rechecksum(byte[] body)
    {
        var sum = 0;
        foreach (var b in body.AsSpan(2))
            sum = (sum + b) & 255;
        body[1] = (byte)sum;
    }

    private static async Task<CemConfigurationRevisionResearchAssessment> Review(
        string first, string second, string json,
        string? part = "00000000", string? revision = "SYNTHETIC-1",
        CancellationToken token = default) =>
        await CemConfigurationOfflineResearchPipeline.ReviewFilesAsync(
            first, second, json, part, revision, token);

    [Fact]
    public async Task FullP2FlowFindsChangedFirstAndLastOrdinalsWithoutOptionAuthority()
    {
        var first = TemporaryPath();
        var second = TemporaryPath();
        var json = TemporaryPath(".json");
        try
        {
            var old = Body(140);
            var next = (byte[])old.Clone();
            next[2] = 0x78;
            next[^1] = 0xAC;
            Rechecksum(next);
            File.WriteAllBytes(first, old);
            File.WriteAllBytes(second, next);
            File.WriteAllText(json, SyntheticJson());
            var assessment = await Review(first, second, json);

            Assert.Equal(CemConfigurationIndexedFormat.P2B9Fc, assessment.Format);
            Assert.Equal(140, assessment.TotalValueCount);
            Assert.Equal(2, assessment.ChangedCount);
            Assert.Equal(new[] { 1, 140 },
                assessment.ChangedOrdinals.Select(row => row.Index1Based));
            Assert.Equal(
                CemConfigurationRevisionResearchDisposition.ExactUnverifiedUserClaim,
                assessment.ChangedOrdinals[0].Disposition);
            Assert.Equal(
                CemConfigurationRevisionResearchDisposition.NoClaimForChangedOrdinal,
                assessment.ChangedOrdinals[1].Disposition);
            Assert.False(assessment.HasAuthenticatedPhysicalCem);
            Assert.False(assessment.HasVerifiedParameterMeaning);
            Assert.False(assessment.MayWriteConfiguration);
            Assert.False(assessment.MayReadLiveConfiguration);
            Assert.False(assessment.MayAcquirePin);
        }
        finally { File.Delete(first); File.Delete(second); File.Delete(json); }
    }

    [Fact]
    public async Task P3LastOrdinalIsMatchedOnlyAgainstP3ResearchClaims()
    {
        var first = TemporaryPath();
        var second = TemporaryPath();
        var json = TemporaryPath(".json");
        try
        {
            var old = Body(252);
            var next = (byte[])old.Clone();
            next[^1] = 0xFE;
            Rechecksum(next);
            File.WriteAllBytes(first, old);
            File.WriteAllBytes(second, next);
            File.WriteAllText(json, SyntheticJson(dataFormat: "P3F106", index: 252));
            var assessment = await Review(first, second, json);
            var row = Assert.Single(assessment.ChangedOrdinals);
            Assert.Equal(252, row.Index1Based);
            Assert.Equal(
                CemConfigurationRevisionResearchDisposition.ExactUnverifiedUserClaim,
                row.Disposition);
            Assert.Equal(CemConfigurationIndexedFormat.P3F106, assessment.Format);
        }
        finally { File.Delete(first); File.Delete(second); File.Delete(json); }
    }

    [Fact]
    public async Task AClaimOfTheWrongGenerationNeverMatches()
    {
        var first = TemporaryPath();
        var second = TemporaryPath();
        var json = TemporaryPath(".json");
        try
        {
            var changed = Body(140);
            changed[2] ^= 0x11;
            Rechecksum(changed);
            File.WriteAllBytes(first, Body(140));
            File.WriteAllBytes(second, changed);
            File.WriteAllText(json, SyntheticJson(dataFormat: "P3F106"));
            var result = await Review(first, second, json);
            Assert.Equal(
                CemConfigurationRevisionResearchDisposition.NoClaimForChangedOrdinal,
                Assert.Single(result.ChangedOrdinals).Disposition);
        }
        finally { File.Delete(first); File.Delete(second); File.Delete(json); }
    }

    [Fact]
    public async Task ClaimedDifferentSoftwareRevisionNeverTurnsIntoExactMatch()
    {
        var first = TemporaryPath();
        var second = TemporaryPath();
        var json = TemporaryPath(".json");
        try
        {
            var changed = Body(140);
            changed[2] ^= 1;
            Rechecksum(changed);
            File.WriteAllBytes(first, Body(140));
            File.WriteAllBytes(second, changed);
            File.WriteAllText(json, SyntheticJson(revision: "SYNTHETIC-2"));
            var result = await Review(first, second, json);
            Assert.Equal(
                CemConfigurationRevisionResearchDisposition.OnlyOtherSoftwareRevisionClaim,
                Assert.Single(result.ChangedOrdinals).Disposition);
        }
        finally { File.Delete(first); File.Delete(second); File.Delete(json); }
    }

    [Fact]
    public async Task ClaimedDifferentPartNumberNeverTurnsIntoExactMatch()
    {
        var first = TemporaryPath();
        var second = TemporaryPath();
        var json = TemporaryPath(".json");
        try
        {
            var changed = Body(140);
            changed[2] ^= 1;
            Rechecksum(changed);
            File.WriteAllBytes(first, Body(140));
            File.WriteAllBytes(second, changed);
            File.WriteAllText(json, SyntheticJson(part: "00000001"));
            var result = await Review(first, second, json);
            Assert.Equal(
                CemConfigurationRevisionResearchDisposition.OnlyOtherHardwarePartClaim,
                Assert.Single(result.ChangedOrdinals).Disposition);
        }
        finally { File.Delete(first); File.Delete(second); File.Delete(json); }
    }

    [Fact]
    public async Task IdenticalBodiesReturnEmptyComparisonNotSyntheticMapping()
    {
        var first = TemporaryPath();
        var second = TemporaryPath();
        var json = TemporaryPath(".json");
        try
        {
            File.WriteAllBytes(first, Body(140));
            File.WriteAllBytes(second, Body(140));
            File.WriteAllText(json, SyntheticJson());
            var result = await Review(first, second, json);
            Assert.Empty(result.ChangedOrdinals);
            Assert.Contains("Changed raw ordinals: 0",
                result.CreateRedactedReport(english: true));
        }
        finally { File.Delete(first); File.Delete(second); File.Delete(json); }
    }

    [Fact]
    public async Task MalformedCatalogIsRejectedBeforeOpeningMissingBinaryFiles()
    {
        var json = TemporaryPath(".json");
        try
        {
            File.WriteAllText(json, "{\"format\":\"unsafe\",\"pin\":\"1234\"}");
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                Review("missing-old.bin", "missing-new.bin", json));
        }
        finally { File.Delete(json); }
    }

    [Fact]
    public async Task DuplicateResearchScopesRejectWholeCatalog()
    {
        var json = TemporaryPath(".json");
        try
        {
            var first = SyntheticJson();
            using var parsed = JsonDocument.Parse(first);
            var root = parsed.RootElement;
            var claim = root.GetProperty("claims")[0].GetRawText();
            var duplicate = first.Replace("[" + claim + "]",
                "[" + claim + "," + claim + "]", StringComparison.Ordinal);
            File.WriteAllText(json, duplicate);
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                Review("missing-old.bin", "missing-new.bin", json));
        }
        finally { File.Delete(json); }
    }

    [Fact]
    public async Task MixedP2P3BodiesCannotBeComparedEvenWithMatchingCatalog()
    {
        var first = TemporaryPath();
        var second = TemporaryPath();
        var json = TemporaryPath(".json");
        try
        {
            File.WriteAllBytes(first, Body(140));
            File.WriteAllBytes(second, Body(252));
            File.WriteAllText(json, SyntheticJson());
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                Review(first, second, json));
        }
        finally { File.Delete(first); File.Delete(second); File.Delete(json); }
    }

    [Fact]
    public async Task InvalidBodyChecksumNeverProducesAResearchAssessment()
    {
        var first = TemporaryPath();
        var second = TemporaryPath();
        var json = TemporaryPath(".json");
        try
        {
            File.WriteAllBytes(first, Body(140));
            var damaged = Body(140);
            damaged[1] ^= 1;
            File.WriteAllBytes(second, damaged);
            File.WriteAllText(json, SyntheticJson());
            await Assert.ThrowsAsync<FormatException>(() =>
                Review(first, second, json));
        }
        finally { File.Delete(first); File.Delete(second); File.Delete(json); }
    }

    [Fact]
    public async Task CancelledRunNeverReturnsAReport()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Review("missing-old.bin", "missing-new.bin", "missing-catalog.json",
                token: cts.Token));
    }

    [Fact]
    public async Task RedactedRuAndEnReportsNeverLeakSourceBytesOrPaths()
    {
        var first = TemporaryPath();
        var second = TemporaryPath();
        var json = TemporaryPath(".json");
        try
        {
            var previous = Body(140);
            var updated = Body(140);
            updated[2] = 0xFA;
            Rechecksum(updated);
            File.WriteAllBytes(first, previous);
            File.WriteAllBytes(second, updated);
            File.WriteAllText(json, SyntheticJson());
            var originals = new[]
            {
                SHA256.HashData(File.ReadAllBytes(first)),
                SHA256.HashData(File.ReadAllBytes(second)),
                SHA256.HashData(File.ReadAllBytes(json))
            };
            var result = await Review(first, second, json);
            var en = result.CreateRedactedReport(true);
            var ru = result.CreateRedactedReport(false);
            Assert.Contains("Ordinal 1: exact user claim scope (UNVERIFIED)", en);
            Assert.Contains("Индекс 1: точное совпадение заявления (НЕ ПОДТВЕРЖДЕНО)", ru);
            foreach (var report in new[] { en, ru })
            {
                Assert.DoesNotContain(first, report);
                Assert.DoesNotContain(second, report);
                Assert.DoesNotContain(json, report);
                Assert.DoesNotContain("00000000", report);
                Assert.DoesNotContain("SYNTHETIC-1", report);
                Assert.DoesNotContain("https://example.invalid", report);
                Assert.DoesNotContain("SYNTHETIC ONLY", report);
                Assert.DoesNotContain("0xFA", report);
                Assert.DoesNotContain("0x12", report);
            }
            Assert.True(originals[0].SequenceEqual(SHA256.HashData(File.ReadAllBytes(first))));
            Assert.True(originals[1].SequenceEqual(SHA256.HashData(File.ReadAllBytes(second))));
            Assert.True(originals[2].SequenceEqual(SHA256.HashData(File.ReadAllBytes(json))));
        }
        finally { File.Delete(first); File.Delete(second); File.Delete(json); }
    }

    [Fact]
    public void PipelineHasOnlyLocalInspectionDependencies()
    {
        var source = Source("src/Dvinholm.App/CemConfigurationOfflineResearchPipeline.cs");
        Assert.Contains("CemConfigurationResearchCatalogImport.InspectFileAsync", source);
        Assert.Contains("CemConfigurationOfflineComparison.CompareFilesAsync", source);
        Assert.Contains("CemConfigurationRevisionResearchTriage.Assess", source);
        Assert.True(source.IndexOf(
            "CemConfigurationResearchCatalogImport.InspectFileAsync",
            StringComparison.Ordinal) < source.IndexOf(
            "CemConfigurationOfflineComparison.CompareFilesAsync",
            StringComparison.Ordinal));
        foreach (var forbidden in new[]
        {
            "PassThru", "J2534Session", "UdsClient", "SendAsync(",
            "ReadCemPin(", "SecurityAccess(", "WriteConfiguration(",
            "File.WriteAllBytes(", "FileMode.Create", "FileAccess.Write"
        })
            Assert.DoesNotContain(forbidden, source);
        Assert.Empty(CemConfigurationMappingResearch.ProductionMappings);
    }

    private static string Source(string relative)
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName,
                relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path))
                return File.ReadAllText(path);
        }
        throw new FileNotFoundException(relative);
    }
}
