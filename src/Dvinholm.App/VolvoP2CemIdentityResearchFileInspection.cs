using System.Globalization;
using System.Text;

namespace Dvinholm.App;

/// <summary>
/// Inspects a user-selected local CEM research file without creating profiles,
/// allocating a vehicle session, transferring data to an ECU or persisting a
/// copy. The input is a self-authored claim, never hardware proof.
/// </summary>
internal static class VolvoP2CemIdentityResearchFileInspection
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal static async Task<VolvoP2CemIdentityResearchDocument> InspectFileAsync(
        string userSelectedPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userSelectedPath);
        cancellationToken.ThrowIfCancellationRequested();

        await using var stream = new FileStream(userSelectedPath,
            FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 4096,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);

        if (stream.Length is < 1 or > VolvoP2CemIdentityResearchExport.MaximumBytes)
            throw new InvalidDataException(
                "The CEM identity research file is empty or exceeds 4 KiB.");

        var bytes = new byte[(int)stream.Length];
        await stream.ReadExactlyAsync(bytes, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        // This writer emits strict UTF-8 without a BOM. Do not silently
        // accept UTF-16, a leading BOM or lossy replacement characters.
        if (bytes.AsSpan().StartsWith([0xEF, 0xBB, 0xBF]))
            throw new InvalidDataException(
                "The CEM identity research JSON must use UTF-8 without a BOM.");

        string json;
        try
        {
            json = StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException ex)
        {
            throw new InvalidDataException(
                "The CEM identity research file contains invalid UTF-8.", ex);
        }

        var record = VolvoP2CemIdentityResearchExport.InspectJson(json);
        cancellationToken.ThrowIfCancellationRequested();
        return record;
    }

    internal static string BuildReadOnlyReview(
        VolvoP2CemIdentityResearchDocument record)
    {
        ArgumentNullException.ThrowIfNull(record);
        // Imported metadata never becomes independently verified evidence.
        // Reuse the already strict worksheet assessment even when callers
        // construct an object instead of reading it through the file parser.
        if (record.Format != VolvoP2CemIdentityResearchExport.Format ||
            record.SchemaVersion != 1 ||
            !record.IsUnverifiedManualClaim ||
            record.IsRestorableBackup ||
            record.MayAccessEcu)
            throw new InvalidDataException("Unsafe manual CEM research record.");

        var report = VolvoP2CemVidaIdentityWorksheet.Assess(
            record.ClaimedModelYear.ToString(CultureInfo.InvariantCulture),
            record.ClaimedHardwarePartNumber,
            record.ClaimedSoftwarePartNumber,
            record.ClaimedDiagnosticSoftwarePartNumber,
            record.UserClaimsVidaSource);

        if (report.Kind != VolvoP2CemVidaWorksheetKind.ManualClaimUnverified)
            throw new InvalidDataException("Invalid manual CEM research record.");

        return "LOCAL JSON INSPECTION (OFFLINE). No ECU session opened." +
            Environment.NewLine +
            "Imported file content is user-editable and NOT an authenticated VIDA/ECU reading." +
            Environment.NewLine +
            "The report does NOT update profiles, establish compatibility or allow service access." +
            Environment.NewLine + Environment.NewLine + report.Summary;
    }
}
