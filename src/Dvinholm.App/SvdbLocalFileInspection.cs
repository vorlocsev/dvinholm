namespace Dvinholm.App;

/// <summary>
/// Inspect only the first 64 bytes of a user-selected, opaque SVCAT001 file.
/// This never loads the protected body, installs a provider, or writes a file.
/// Structural layout matching is NOT cryptographic authentication.
/// </summary>
internal static class SvdbLocalFileInspection
{
    internal const long MaximumFileBytes = 512L * 1024 * 1024;

    internal static async Task<SvdbLayoutAssessment> InspectFileAsync(
        string userSelectedPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userSelectedPath);
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.Equals(Path.GetExtension(userSelectedPath), ".svdb",
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Only a selected .svdb file can be inspected.");

        // A read-only handle and shared-reader lock prevent the scanner from
        // modifying the input. No extraction or storage of its payload occurs.
        await using var input = new FileStream(userSelectedPath,
            FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 4096,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);

        var initialLength = input.Length;
        ValidateFileLength(initialLength);

        // Use a bounded, single read of the on-disk header. Short files
        // become explicit InvalidHeaderLength assessments, not exceptions
        // accidentally mistaken for a decryptor or a database failure.
        var bytesToRead = (int)Math.Min(initialLength,
            SvdbContainerLayoutInspector.HeaderBytes);
        var header = new byte[bytesToRead];
        await input.ReadExactlyAsync(header.AsMemory(), cancellationToken);

        if (input.Length != initialLength)
            throw new InvalidDataException("Selected SVDB length changed during inspection.");

        cancellationToken.ThrowIfCancellationRequested();
        return SvdbContainerLayoutInspector.Inspect(header, initialLength);
    }

    internal static void ValidateFileLength(long fileLength)
    {
        if (fileLength < 0 || fileLength > MaximumFileBytes)
            throw new InvalidDataException(
                "Selected SVDB length is invalid or exceeds the offline inspection bound.");
    }
}
