using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Dvinholm.App.Tests;

/// <summary>
/// Tests use small, wholly synthetic local .svdb files, never proprietary
/// catalog bytes or a real diagnostic installation.
/// </summary>
public sealed class SvdbLocalFileInspectionTests
{
    private static byte[] Synthetic(ulong length = 1, bool validMagic = true)
    {
        var blockCount = 1 + (length - 1) / 65_536;
        var total = checked((int)(64UL + length + blockCount * 16UL));
        var bytes = new byte[total];
        if (validMagic) "SVCAT001"u8.CopyTo(bytes);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8, 4), 0x0001_0000);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(12, 8), length);
        return bytes;
    }

    private static string TempFile(byte[] bytes, string suffix = ".svdb")
    {
        var path = Path.Combine(Path.GetTempPath(),
            "Dvinholm-Svdb-Offline-" + Guid.NewGuid().ToString("N") + suffix);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    [Fact]
    public async Task ExactSyntheticHeaderIsReadWithoutReadingOrChangingBody()
    {
        var bytes = Synthetic(65_537);
        var path = TempFile(bytes);
        try
        {
            var before = SHA256.HashData(File.ReadAllBytes(path));
            var result = await SvdbLocalFileInspection.InspectFileAsync(path);
            var after = SHA256.HashData(File.ReadAllBytes(path));

            Assert.Equal(SvdbLayoutProblem.None, result.Problem);
            Assert.Equal(2L, result.BlockCount);
            Assert.Equal(bytes.Length, result.ExpectedPhysicalLength);
            Assert.True(before.SequenceEqual(after));
            Assert.False(result.DataDecrypted);
            Assert.False(result.AuthenticatedContents);
            Assert.False(result.MayReadOrWriteVehicle);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ASeparateMinimalSyntheticContainerIsAccepted()
    {
        var path = TempFile(Synthetic());
        try
        {
            var result = await SvdbLocalFileInspection.InspectFileAsync(path);
            Assert.True(result.MatchesObservedBlockLayout);
            Assert.Equal(1L, result.BlockCount);
            Assert.Equal(81L, result.ExpectedPhysicalLength);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task TruncatedAndEmptyFilesReturnHeaderLengthFailure()
    {
        foreach (var size in new[] { 0, 1, 63 })
        {
            var path = TempFile(new byte[size]);
            try
            {
                var result = await SvdbLocalFileInspection.InspectFileAsync(path);
                Assert.Equal(SvdbLayoutProblem.InvalidHeaderLength, result.Problem);
                Assert.False(result.MatchesObservedBlockLayout);
            }
            finally { File.Delete(path); }
        }
    }

    [Fact]
    public async Task InvalidMagicIsNotMistakenForReadableCatalog()
    {
        var path = TempFile(Synthetic(1, validMagic: false));
        try
        {
            var result = await SvdbLocalFileInspection.InspectFileAsync(path);
            Assert.Equal(SvdbLayoutProblem.InvalidMagic, result.Problem);
            Assert.False(result.HasVerifiedCemParameterMapping);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task IncorrectPhysicalSizeIsReportedNotSilentlyRepaired()
    {
        var data = Synthetic(1);
        var path = TempFile(data[..^1]);
        try
        {
            var result = await SvdbLocalFileInspection.InspectFileAsync(path);
            Assert.Equal(SvdbLayoutProblem.PhysicalLengthMismatch, result.Problem);
            Assert.Equal(81L, result.ExpectedPhysicalLength);
            Assert.Equal(80L, result.ObservedPhysicalLength);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task WrongExtensionIsRejectedBeforeOpening()
    {
        var path = TempFile(Synthetic(), ".sqlite");
        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                SvdbLocalFileInspection.InspectFileAsync(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task UppercaseSvdbExtensionIsAccepted()
    {
        var path = TempFile(Synthetic(), ".SVDB");
        try
        {
            var result = await SvdbLocalFileInspection.InspectFileAsync(path);
            Assert.True(result.MatchesObservedBlockLayout);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task CancellationPreventsNewFileOpening()
    {
        var path = TempFile(Synthetic());
        try
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                SvdbLocalFileInspection.InspectFileAsync(path, cancellation.Token));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void InvalidAndOversizedLengthsAreRejectedWithoutCreatingGiantFiles()
    {
        Assert.Throws<InvalidDataException>(() =>
            SvdbLocalFileInspection.ValidateFileLength(-1));
        Assert.Throws<InvalidDataException>(() =>
            SvdbLocalFileInspection.ValidateFileLength(
                SvdbLocalFileInspection.MaximumFileBytes + 1));
        SvdbLocalFileInspection.ValidateFileLength(66_793_525);
        SvdbLocalFileInspection.ValidateFileLength(13_812_592);
    }

    [Fact]
    public void InputScannerHasNoPayloadReadOrVehicleOperations()
    {
        var source = Source("src/Dvinholm.App/SvdbLocalFileInspection.cs");
        Assert.Contains("FileMode.Open, FileAccess.Read, FileShare.Read", source);
        Assert.Contains("var header = new byte[bytesToRead];", source);
        Assert.Contains("input.ReadExactlyAsync(header.AsMemory(), cancellationToken)", source);
        Assert.DoesNotContain("File.ReadAllBytes(", source);
        Assert.DoesNotContain("CopyToAsync(", source);
        Assert.DoesNotContain("FileMode.Create", source);
        Assert.DoesNotContain("FileAccess.Write", source);
        Assert.DoesNotContain("ReadCemPin(", source);
        Assert.DoesNotContain("PassThru", source);
        Assert.DoesNotContain("SqliteConnection", source);
        Assert.DoesNotContain("Decrypt(", source);
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
