using System.Security.Cryptography;

namespace Dvinholm.App.Tests;

/// <summary>
/// Only tiny synthetic .bin files. Never selects an ECU, sends commands,
/// writes vehicle data, or imports real factory configuration records.
/// </summary>
public sealed class CemConfigurationOfflineComparisonTests
{
    private static byte[] Body(int count)
    {
        var body = new byte[count + 2];
        body[0] = checked((byte)count);
        body[2] = 0x12;
        body[^1] = 0x34;
        RecomputeChecksum(body);
        return body;
    }

    private static void RecomputeChecksum(byte[] body)
    {
        var sum = 0;
        foreach (var value in body.AsSpan(2))
            sum = (sum + value) & 0xFF;
        body[1] = (byte)sum;
    }

    private static string TempPath(string extension = ".bin") =>
        Path.Combine(Path.GetTempPath(),
            "Dvinholm-Cem-Compare-" + Guid.NewGuid().ToString("N") + extension);

    [Fact]
    public async Task EqualP2BodiesGiveNoChangesAndNoVerifiedStatus()
    {
        var file = TempPath();
        try
        {
            File.WriteAllBytes(file, Body(140));
            var result = await CemConfigurationOfflineComparison.CompareFilesAsync(file, file);
            Assert.Equal(CemConfigurationIndexedFormat.P2B9Fc, result.Format);
            Assert.Equal(140, result.RawValueCount);
            Assert.Equal(0, result.ChangedCount);
            Assert.True(result.RawValuesEqual);
            Assert.Equal(result.FirstBodySha256, result.SecondBodySha256);
            Assert.False(result.HasVerifiedCemIdentity);
            Assert.False(result.HasVerifiedOptionMeanings);
            Assert.False(result.IsRestorableBackup);
            Assert.False(result.MayReadLiveConfiguration);
            Assert.False(result.MayWriteConfiguration);
            Assert.False(result.MayAcquirePin);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public async Task P2ChangesAtFirstAndLastPositionsKeepOneBasedOrdering()
    {
        var first = TempPath();
        var second = TempPath();
        try
        {
            var oldBody = Body(140);
            var newBody = (byte[])oldBody.Clone();
            newBody[2] = 0xEF;
            newBody[^1] = 0xAB;
            RecomputeChecksum(newBody);
            File.WriteAllBytes(first, oldBody);
            File.WriteAllBytes(second, newBody);
            var result = await CemConfigurationOfflineComparison.CompareFilesAsync(first, second);
            Assert.Equal(2, result.ChangedCount);
            Assert.Equal(new[] { 1, 140 }, result.ChangedOrdinals);
            Assert.NotEqual(result.FirstBodySha256, result.SecondBodySha256);
            Assert.False(result.RawValuesEqual);
        }
        finally { File.Delete(first); File.Delete(second); }
    }

    [Fact]
    public async Task P3FinalValueChangesOnlyAtIndex252()
    {
        var first = TempPath();
        var second = TempPath();
        try
        {
            var oldBody = Body(252);
            var next = (byte[])oldBody.Clone();
            next[^1] ^= 0x40;
            RecomputeChecksum(next);
            File.WriteAllBytes(first, oldBody);
            File.WriteAllBytes(second, next);
            var result = await CemConfigurationOfflineComparison.CompareFilesAsync(first, second);
            Assert.Equal(CemConfigurationIndexedFormat.P3F106, result.Format);
            Assert.Equal(252, result.RawValueCount);
            Assert.Equal(new[] { 252 }, result.ChangedOrdinals);
        }
        finally { File.Delete(first); File.Delete(second); }
    }

    [Fact]
    public async Task MixedP2AndP3BodiesCannotBeAligned()
    {
        var first = TempPath();
        var second = TempPath();
        try
        {
            File.WriteAllBytes(first, Body(140));
            File.WriteAllBytes(second, Body(252));
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                CemConfigurationOfflineComparison.CompareFilesAsync(first, second));
        }
        finally { File.Delete(first); File.Delete(second); }
    }

    [Fact]
    public async Task InvalidChecksumFailsBeforeAnyComparisonResult()
    {
        var first = TempPath();
        var second = TempPath();
        try
        {
            File.WriteAllBytes(first, Body(140));
            var damaged = Body(140);
            damaged[1] ^= 0x01;
            File.WriteAllBytes(second, damaged);
            await Assert.ThrowsAsync<FormatException>(() =>
                CemConfigurationOfflineComparison.CompareFilesAsync(first, second));
        }
        finally { File.Delete(first); File.Delete(second); }
    }

    [Fact]
    public async Task WrongLengthAndExtensionAreRejected()
    {
        var first = TempPath();
        var shortFile = TempPath();
        var wrongExtension = TempPath(".txt");
        try
        {
            File.WriteAllBytes(first, Body(140));
            File.WriteAllBytes(shortFile, new byte[141]);
            File.WriteAllBytes(wrongExtension, Body(140));
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                CemConfigurationOfflineComparison.CompareFilesAsync(first, shortFile));
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                CemConfigurationOfflineComparison.CompareFilesAsync(first, wrongExtension));
        }
        finally
        {
            File.Delete(first);
            File.Delete(shortFile);
            File.Delete(wrongExtension);
        }
    }

    [Fact]
    public async Task CancellationBeforeReadingDoesNotProduceReport()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CemConfigurationOfflineComparison.CompareFilesAsync(
                "never-open-first.bin", "never-open-second.bin", source.Token));
    }

    [Fact]
    public async Task ComparisonLeavesOriginalFilesUnchangedAndDoesNotPersistOutput()
    {
        var first = TempPath();
        var second = TempPath();
        try
        {
            File.WriteAllBytes(first, Body(140));
            File.WriteAllBytes(second, Body(140));
            var oldHash = SHA256.HashData(File.ReadAllBytes(first));
            var newHash = SHA256.HashData(File.ReadAllBytes(second));
            await CemConfigurationOfflineComparison.CompareFilesAsync(first, second);
            Assert.True(oldHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(first))));
            Assert.True(newHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(second))));
        }
        finally { File.Delete(first); File.Delete(second); }
    }

    [Fact]
    public async Task RedactedReportsHidePathsHashesBytesAndClaimedMeanings()
    {
        var first = TempPath();
        var second = TempPath();
        try
        {
            var a = Body(140);
            var b = Body(140);
            b[2] = 0xEF;
            RecomputeChecksum(b);
            File.WriteAllBytes(first, a);
            File.WriteAllBytes(second, b);
            var review = await CemConfigurationOfflineComparison.CompareFilesAsync(first, second);
            var english = review.CreateRedactedReport(english: true);
            var russian = review.CreateRedactedReport(english: false);
            Assert.Contains("Changed one-based ordinals: 1", english);
            Assert.Contains("Изменённые индексы (с 1): 1", russian);
            Assert.Contains("NOT a restorable backup", english);
            foreach (var text in new[] { english, russian })
            {
                Assert.DoesNotContain(first, text);
                Assert.DoesNotContain(second, text);
                Assert.DoesNotContain(review.FirstBodySha256, text);
                Assert.DoesNotContain(review.SecondBodySha256, text);
                Assert.DoesNotContain("0xEF", text);
                Assert.DoesNotContain("0x12", text);
                Assert.DoesNotContain("SYNTHETIC ONLY", text);
            }
        }
        finally { File.Delete(first); File.Delete(second); }
    }

    [Fact]
    public async Task ReturnedOrdinalCollectionIsImmutable()
    {
        var first = TempPath();
        var second = TempPath();
        try
        {
            File.WriteAllBytes(first, Body(252));
            var changed = Body(252);
            changed[2] ^= 0x01;
            RecomputeChecksum(changed);
            File.WriteAllBytes(second, changed);
            var review = await CemConfigurationOfflineComparison.CompareFilesAsync(first, second);
            var ordinals = Assert.IsAssignableFrom<IList<int>>(review.ChangedOrdinals);
            Assert.Throws<NotSupportedException>(() => ordinals[0] = 200);
        }
        finally { File.Delete(first); File.Delete(second); }
    }

    [Fact]
    public void BackendHasNoDiagnosticRequestOrFileExportPath()
    {
        var code = Source("src/Dvinholm.App/CemConfigurationOfflineComparison.cs");
        Assert.Contains("CemConfigurationOfflineBlockInspection.InspectFileAsync", code);
        Assert.Contains("CemConfigurationIndexedReview.Compare", code);
        Assert.Contains("CreateRedactedReport", code);
        foreach (var forbidden in new[]
        {
            "PassThru", "J2534Session", "UdsClient", "SecurityAccess(",
            "SendAsync(", "WriteConfiguration(", "ReadCemPin(",
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
