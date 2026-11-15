namespace Dvinholm.App.Tests;

/// <summary>
/// Synthetic index-only P2/P3 review; no ECU input, transport, PIN or writes.
/// </summary>
public sealed class CemConfigurationIndexedReviewTests
{
    private static byte[] Body(int count)
    {
        var result = new byte[count + 2];
        result[0] = (byte)count;
        for (var i = 0; i < count; i++)
            result[i + 2] = (byte)(i * 11 + 7);
        Recheck(result);
        return result;
    }

    private static void Recheck(byte[] result)
    {
        var checksum = 0;
        foreach (var value in result.AsSpan(2))
            checksum = (checksum + value) & 0xFF;
        result[1] = (byte)checksum;
    }

    [Fact]
    public void P2RawIndicesAreOneBasedAndNeverCatalogOptionNames()
    {
        var result = CemConfigurationIndexedReview.InspectP2B9Fc(Body(140));
        Assert.Equal(CemConfigurationIndexedFormat.P2B9Fc, result.Format);
        Assert.Equal(140, result.Values.Count);
        Assert.Equal(1, result.Values[0].Index1Based);
        Assert.Equal(140, result.Values[^1].Index1Based);
        Assert.Equal("07", result.Values[0].ValueHex);
        Assert.False(result.HasVerifiedParameterMeanings);
        Assert.False(result.HasVerifiedCemIdentity);
        Assert.False(result.IsRestorableBackup);
    }

    [Fact]
    public void P3RawIndicesAreOneBasedThrough252()
    {
        var result = CemConfigurationIndexedReview.InspectP3F106(Body(252));
        Assert.Equal(CemConfigurationIndexedFormat.P3F106, result.Format);
        Assert.Equal(252, result.Values.Count);
        Assert.Equal(1, result.Values[0].Index1Based);
        Assert.Equal(252, result.Values[^1].Index1Based);
        Assert.False(result.MayReadOrWriteVehicle);
    }

    [Fact]
    public void IndexViewPreservesEveryByteIncludingUnknownOptions()
    {
        var body = Body(140);
        for (var index = 2; index < body.Length; index++)
            body[index] = (byte)(index % 2 == 0 ? 0xFF : 0x00);
        Recheck(body);
        var result = CemConfigurationIndexedReview.InspectP2B9Fc(body);
        Assert.Equal("FF", result.Values[0].ValueHex);
        Assert.Equal("00", result.Values[1].ValueHex);
        Assert.True(result.Values.Select(entry => entry.Value)
            .SequenceEqual(body.AsSpan(2).ToArray()));
    }

    [Fact]
    public void InspectRejectsWrongFormatAndChecksum()
    {
        Assert.Throws<FormatException>(() =>
            CemConfigurationIndexedReview.InspectP2B9Fc(Body(252)));
        Assert.Throws<FormatException>(() =>
            CemConfigurationIndexedReview.InspectP3F106(Body(140)));
        var invalid = Body(252);
        invalid[1] ^= 1;
        Assert.Throws<FormatException>(() =>
            CemConfigurationIndexedReview.InspectP3F106(invalid));
    }

    [Fact]
    public void BodyDigestIsDeterministicAndDescribesBytesNotIdentity()
    {
        var before = Body(140);
        var one = CemConfigurationIndexedReview.InspectP2B9Fc(before);
        var two = CemConfigurationIndexedReview.InspectP2B9Fc(before);
        Assert.Equal(one.BodySha256, two.BodySha256);
        Assert.Equal(64, one.BodySha256.Length);
        before[2] ^= 1;
        Recheck(before);
        var changed = CemConfigurationIndexedReview.InspectP2B9Fc(before);
        Assert.NotEqual(one.BodySha256, changed.BodySha256);
        Assert.False(changed.HasVerifiedCemIdentity);
    }

    [Fact]
    public void EqualBodiesProduceZeroRawOrdinalDifferences()
    {
        var first = CemConfigurationIndexedReview.InspectP2B9Fc(Body(140));
        var second = CemConfigurationIndexedReview.InspectP2B9Fc(Body(140));
        Assert.Empty(CemConfigurationIndexedReview.Compare(first, second));
    }

    [Fact]
    public void ValidChangesReportOnlyModifiedOneBasedIndices()
    {
        var before = Body(140);
        var after = (byte[])before.Clone();
        after[2] = 0xAA;
        after[141] = 0xBB;
        Recheck(after);
        var differences = CemConfigurationIndexedReview.Compare(
            CemConfigurationIndexedReview.InspectP2B9Fc(before),
            CemConfigurationIndexedReview.InspectP2B9Fc(after));
        Assert.Equal(new[] { 1, 140 }, differences.Select(item => item.Index1Based));
        Assert.Equal(before[2], differences[0].Before);
        Assert.Equal((byte)0xAA, differences[0].After);
        Assert.Equal(before[141], differences[1].Before);
        Assert.Equal((byte)0xBB, differences[1].After);
    }

    [Fact]
    public void P3IndexedComparisonCoversFinalValue()
    {
        var before = Body(252);
        var after = (byte[])before.Clone();
        after[^1] ^= 0x20;
        Recheck(after);
        var difference = Assert.Single(CemConfigurationIndexedReview.Compare(
            CemConfigurationIndexedReview.InspectP3F106(before),
            CemConfigurationIndexedReview.InspectP3F106(after)));
        Assert.Equal(252, difference.Index1Based);
    }

    [Fact]
    public void MixedFormatsFailWithoutAttemptingToAlignIndices()
    {
        Assert.Throws<InvalidDataException>(() =>
            CemConfigurationIndexedReview.Compare(
                CemConfigurationIndexedReview.InspectP2B9Fc(Body(140)),
                CemConfigurationIndexedReview.InspectP3F106(Body(252))));
    }

    [Fact]
    public void ReturnedValuesAndDiffsAreReadOnly()
    {
        var first = CemConfigurationIndexedReview.InspectP2B9Fc(Body(140));
        var rows = Assert.IsAssignableFrom<IList<CemConfigurationIndexedValue>>(first.Values);
        Assert.Throws<NotSupportedException>(() =>
            rows[0] = new CemConfigurationIndexedValue(1, 12));
        var nextBody = Body(140);
        nextBody[2] ^= 0x80;
        Recheck(nextBody);
        var second = CemConfigurationIndexedReview.InspectP2B9Fc(nextBody);
        var diffs = CemConfigurationIndexedReview.Compare(first, second);
        var list = Assert.IsAssignableFrom<IList<CemConfigurationIndexedDifference>>(diffs);
        Assert.Throws<NotSupportedException>(() =>
            list[0] = new CemConfigurationIndexedDifference(1, 0, 0));
    }

    [Fact]
    public void OriginalInputMutationCannotChangeIndexedSnapshot()
    {
        var body = Body(140);
        var snapshot = CemConfigurationIndexedReview.InspectP2B9Fc(body);
        var first = snapshot.Values[0].Value;
        var digest = snapshot.BodySha256;
        body[2] ^= 0xFF;
        Assert.Equal(first, snapshot.Values[0].Value);
        Assert.Equal(digest, snapshot.BodySha256);
    }

    [Fact]
    public void UnverifiedProductionCatalogNeverMapsIndexedValues()
    {
        var snapshot = CemConfigurationIndexedReview.InspectP2B9Fc(Body(140));
        Assert.False(snapshot.HasVerifiedParameterMeanings);
        Assert.False(snapshot.MayReadOrWriteVehicle);
        Assert.Empty(VolvoP2CemSupportMatrix.ProductionEntries);
        Assert.Empty(VolvoP2CemParameterEvidenceCatalog.ProductionRecords);
        Assert.All(VolvoP2CemConfigOptionManifest.Entries, option =>
            Assert.False(option.HasVerifiedConfigurationReadMapping));
        Assert.False(new VolvoP2CemConfigurationReadPreflightAssessment(
            VolvoP2CemSourceSetKind.NoInScopeClaims,
            Array.Empty<VolvoP2ServiceEvidenceGap>(), false, false)
            .MayReadLiveCemConfiguration);
    }

    [Fact]
    public void IndexedReviewHasNoJ2534RequestOrPinOperations()
    {
        var code = Source("src/Dvinholm.App/CemConfigurationIndexedReview.cs");
        Assert.DoesNotContain("PassThru", code);
        Assert.DoesNotContain("SendAsync", code);
        Assert.DoesNotContain("J2534", code);
        Assert.DoesNotContain("ReadCemPin", code);
        Assert.DoesNotContain("WriteConfiguration", code);
        Assert.DoesNotContain("UdsClient", code);
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
