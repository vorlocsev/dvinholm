using System.Buffers.Binary;

namespace Dvinholm.App.Tests;

/// <summary>
/// Nine deterministic, header-only checks. No original SVDB bytes,
/// secret keys, encryption provider, SQLite data or executable are used.
/// </summary>
public sealed class SvdbContainerLayoutInspectorTests
{
    private static byte[] Header(ulong declared, uint version = 0x0001_0000)
    {
        var header = new byte[SvdbContainerLayoutInspector.HeaderBytes];
        "SVCAT001"u8.CopyTo(header);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(8, 4), version);
        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(12, 8), declared);
        return header;
    }

    private static long Expected(long declared)
    {
        var blocks = 1 + (declared - 1) / 65_536;
        return 64 + declared + blocks * 16;
    }

    [Fact]
    public void FirstSuppliedCatalogHeaderMetadataMatchesObservedLayout()
    {
        const long declared = 66_777_157;
        var report = SvdbContainerLayoutInspector.Inspect(
            Header(declared), 66_793_525);
        Assert.True(report.MatchesObservedBlockLayout);
        Assert.Equal(SvdbLayoutProblem.None, report.Problem);
        Assert.Equal((ulong)declared, report.DeclaredDataLength);
        Assert.Equal(1019L, report.BlockCount);
        Assert.Equal(66_793_525L, report.ExpectedPhysicalLength);
        Assert.False(report.DataDecrypted);
        Assert.False(report.AuthenticatedContents);
    }

    [Fact]
    public void SecondSuppliedCatalogHasItsOwnValidHeaderAndBlockCount()
    {
        const long declared = 13_809_152;
        var report = SvdbContainerLayoutInspector.Inspect(
            Header(declared), 13_812_592);
        Assert.True(report.MatchesObservedBlockLayout);
        Assert.Equal(211L, report.BlockCount);
        Assert.Equal(13_812_592L, report.ExpectedPhysicalLength);
        Assert.False(report.HasVerifiedCemParameterMapping);
        Assert.False(report.MayReadOrWriteVehicle);
    }

    [Fact]
    public void HeaderMustBeExactlySixtyFourBytes()
    {
        Assert.Equal(SvdbLayoutProblem.InvalidHeaderLength,
            SvdbContainerLayoutInspector.Inspect(new byte[63], 128).Problem);
        Assert.Equal(SvdbLayoutProblem.InvalidHeaderLength,
            SvdbContainerLayoutInspector.Inspect(new byte[65], 128).Problem);
    }

    [Fact]
    public void WrongMagicAndUnsupportedVersionFailClosed()
    {
        var header = Header(1);
        header[0] ^= 0x01;
        Assert.Equal(SvdbLayoutProblem.InvalidMagic,
            SvdbContainerLayoutInspector.Inspect(header, 81).Problem);
        Assert.Equal(SvdbLayoutProblem.UnsupportedHeaderVersion,
            SvdbContainerLayoutInspector.Inspect(Header(1, 2), 81).Problem);
    }

    [Fact]
    public void EmptyDeclaredBodyIsNotAcceptedAsAValidCatalog()
    {
        Assert.Equal(SvdbLayoutProblem.EmptyDeclaredData,
            SvdbContainerLayoutInspector.Inspect(Header(0), 64).Problem);
    }

    [Fact]
    public void TruncatedAndExtendedFilesDoNotMatchHeaderClaim()
    {
        const long length = 70_000;
        var properSize = Expected(length);
        Assert.Equal(SvdbLayoutProblem.PhysicalLengthMismatch,
            SvdbContainerLayoutInspector.Inspect(Header(length), properSize - 1).Problem);
        Assert.Equal(SvdbLayoutProblem.PhysicalLengthMismatch,
            SvdbContainerLayoutInspector.Inspect(Header(length), properSize + 1).Problem);
    }

    [Fact]
    public void ExactBoundaryAndPartialBlockHaveSeparateOverheads()
    {
        var one = SvdbContainerLayoutInspector.Inspect(
            Header(65_536), Expected(65_536));
        var two = SvdbContainerLayoutInspector.Inspect(
            Header(65_537), Expected(65_537));
        Assert.Equal(1L, one.BlockCount);
        Assert.Equal(2L, two.BlockCount);
        Assert.True(one.MatchesObservedBlockLayout);
        Assert.True(two.MatchesObservedBlockLayout);
    }

    [Fact]
    public void OversizedClaimsCannotOverflowLengthCalculation()
    {
        var report = SvdbContainerLayoutInspector.Inspect(
            Header(ulong.MaxValue), long.MaxValue);
        Assert.Equal(SvdbLayoutProblem.DeclaredDataTooLarge, report.Problem);
        Assert.False(report.MatchesObservedBlockLayout);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SvdbContainerLayoutInspector.Inspect(Header(1), -1));
    }

    [Fact]
    public void StructuralMatchNeverEnablesDecryptionOrVehicleOperations()
    {
        var report = SvdbContainerLayoutInspector.Inspect(Header(1), 81);
        Assert.True(report.MatchesObservedBlockLayout);
        Assert.False(report.DataDecrypted);
        Assert.False(report.AuthenticatedContents);
        Assert.False(report.HasVerifiedCemParameterMapping);
        Assert.False(report.MayReadOrWriteVehicle);

        var source = Source("src/Dvinholm.App/SvdbContainerLayoutInspector.cs");
        foreach (var forbidden in new[]
        {
            "PassThru", "J2534", "SendAsync(", "Connect(",
            "ReadCemPin(", "SecurityAccess(", "WriteConfiguration(",
            "SqliteConnection", "AesGcm(", "Decrypt("
        })
            Assert.DoesNotContain(forbidden, source);
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
