using System.Buffers.Binary;

namespace Dvinholm.App;

/// <summary>
/// Observed size/offset structure of the opaque SVCAT001 container.
/// This does not authenticate any encrypted block, decrypt data, or
/// identify the contents as a database or a validated Volvo catalog.
/// </summary>
internal enum SvdbLayoutProblem
{
    None,
    InvalidHeaderLength,
    InvalidMagic,
    UnsupportedHeaderVersion,
    EmptyDeclaredData,
    DeclaredDataTooLarge,
    PhysicalLengthMismatch
}

internal sealed record SvdbLayoutAssessment(
    SvdbLayoutProblem Problem,
    ulong DeclaredDataLength,
    long BlockCount,
    long ExpectedPhysicalLength,
    long ObservedPhysicalLength)
{
    internal bool MatchesObservedBlockLayout => Problem == SvdbLayoutProblem.None;
    internal bool AuthenticatedContents => false;
    internal bool DataDecrypted => false;
    internal bool HasVerifiedCemParameterMapping => false;
    internal bool MayReadOrWriteVehicle => false;
}

/// <summary>
/// Offline structural preflight using ONLY a supplied 64-byte header and file
/// length. The 16-byte overhead per 64-KiB block is observed, not assumed
/// to have a known cryptographic meaning, and is never verified as a tag.
/// </summary>
internal static class SvdbContainerLayoutInspector
{
    internal const int HeaderBytes = 64;
    internal const int DataBlockBytes = 65_536;
    internal const int ObservedOverheadBytes = 16;
    internal const uint ObservedVersion = 0x0001_0000;

    internal static SvdbLayoutAssessment Inspect(
        ReadOnlySpan<byte> header, long physicalLength)
    {
        if (physicalLength < 0)
            throw new ArgumentOutOfRangeException(nameof(physicalLength));

        SvdbLayoutAssessment Fail(SvdbLayoutProblem problem,
            ulong declared = 0, long blocks = 0, long expected = 0) =>
            new(problem, declared, blocks, expected, physicalLength);

        if (header.Length != HeaderBytes)
            return Fail(SvdbLayoutProblem.InvalidHeaderLength);

        if (!header[..8].SequenceEqual("SVCAT001"u8))
            return Fail(SvdbLayoutProblem.InvalidMagic);

        if (BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(8, 4)) != ObservedVersion)
            return Fail(SvdbLayoutProblem.UnsupportedHeaderVersion);

        // Interpretation of bytes 12..19 as a little-endian data length
        // is supported by both supplied specimens, not an OEM specification.
        var declaredLength = BinaryPrimitives.ReadUInt64LittleEndian(header.Slice(12, 8));
        if (declaredLength == 0)
            return Fail(SvdbLayoutProblem.EmptyDeclaredData);

        // Avoid overflow in the rounded block count or in the expected size.
        var blockCount = 1UL + (declaredLength - 1UL) / DataBlockBytes;
        const ulong maxLength = long.MaxValue;
        if (declaredLength > maxLength - HeaderBytes ||
            blockCount > maxLength ||
            blockCount > (maxLength - HeaderBytes - declaredLength) / ObservedOverheadBytes)
            return Fail(SvdbLayoutProblem.DeclaredDataTooLarge, declaredLength);

        var blocks = checked((long)blockCount);
        var expected = checked((long)(HeaderBytes + declaredLength +
            blockCount * ObservedOverheadBytes));
        if (expected != physicalLength)
            return Fail(SvdbLayoutProblem.PhysicalLengthMismatch,
                declaredLength, blocks, expected);

        return new(SvdbLayoutProblem.None,
            declaredLength, blocks, expected, physicalLength);
    }
}
