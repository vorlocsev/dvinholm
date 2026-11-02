using System.Text.Json;
using System.Text.RegularExpressions;

namespace Dvinholm.App;

/// <summary>
/// Single fail-closed read-only gate over release metadata, locally checked
/// ZIP, snapshot and publisher trust. This is NOT an installer or an
/// authorization to replace the running application.
/// </summary>
internal sealed record UnifiedX64InstallReadiness(
    string? ReleaseTag, IReadOnlyList<string> Blockers)
{
    // Deliberately no install capability until a separately reviewed production
    // helper, signed release pipeline and crash recovery are accepted.
    internal bool CanInstall => false;
}

internal static class UnifiedX64InstallReadinessGate
{
    internal static UnifiedX64InstallReadiness Inspect(
        UnifiedX64UpdateCandidate? candidate,
        UnifiedX64StagedPackage? staged,
        UnifiedX64BackupSnapshot.Receipt? backup,
        byte[]? detachedSignature,
        string installedDirectory,
        string backupRoot)
    {
        var blockers = new List<string>();

        if (candidate is null || !UnifiedX64StageDownloader.ValidateCandidate(candidate))
            blockers.Add("RELEASE_METADATA_NOT_TRUSTED");

        if (staged is null)
        {
            blockers.Add("X64_PACKAGE_NOT_STAGED");
        }
        else if (candidate is not null)
        {
            if (staged.ZipBytes != candidate.PackageBytes ||
                !Path.GetFileName(staged.ZipPath).Equals(
                    UnifiedX64ReleaseFeed.PackageAsset, StringComparison.Ordinal) ||
                !Path.GetDirectoryName(Path.GetFullPath(staged.ZipPath))!.Equals(
                    Path.GetFullPath(staged.Directory), StringComparison.OrdinalIgnoreCase))
                blockers.Add("STAGING_IDENTITY_MISMATCH");

            try
            {
                // Check the physical bytes again. An earlier successful
                // download is not evidence that the file is unchanged now.
                var manifest = staged.Sha256 + "  " +
                    UnifiedX64ReleaseFeed.PackageAsset + "\n";
                var verified = UnifiedX64PackageVerifier.Verify(
                    staged.ZipPath, manifest, staged.ZipBytes);
                if (verified.Sha256 != staged.Sha256 ||
                    verified.EntryCount != staged.EntryCount ||
                    verified.ExpandedBytes != staged.ExpandedBytes)
                    blockers.Add("STAGED_ARCHIVE_CHANGED");
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or
                UnauthorizedAccessException or ArgumentException)
            {
                blockers.Add("STAGED_ARCHIVE_CHANGED");
            }
        }

        if (backup is null)
        {
            blockers.Add("VERIFIED_BACKUP_MISSING");
        }
        else
        {
            try
            {
                var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(backupRoot));
                var folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(backup.Directory));
                var name = Path.GetFileName(folder);
                if (!Path.GetDirectoryName(folder)!.Equals(root,
                        StringComparison.OrdinalIgnoreCase) ||
                    !Regex.IsMatch(name, @"^backup-[0-9a-f]{32}$",
                        RegexOptions.CultureInvariant))
                    blockers.Add("BACKUP_PATH_UNTRUSTED");
                else
                {
                    var verified = UnifiedX64BackupSnapshot.Verify(folder);
                    if (verified != backup)
                        blockers.Add("BACKUP_RECEIPT_MISMATCH");
                    if (staged is not null)
                    {
                        // Current inventory may have changed since backup.
                        var dryRun = UnifiedX64RecoveryPreflight.Inspect(
                            installedDirectory, staged, root);
                        if (!dryRun.CanPlanBackup || dryRun.FilesToBackUp != backup.Files ||
                            dryRun.BytesToBackUp != backup.Bytes)
                            blockers.Add("BACKUP_NO_LONGER_MATCHES_INSTALL");
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or
                UnauthorizedAccessException or ArgumentException or JsonException)
            {
                blockers.Add("BACKUP_VERIFICATION_FAILED");
            }
        }

        if (!UnifiedX64PublisherTrust.HasTrustedPublisherKey)
            blockers.Add("PUBLISHER_KEY_NOT_PROVISIONED");
        if (candidate is null || staged is null ||
            !UnifiedX64PublisherTrust.Verify(
                candidate.Tag, staged.Sha256, detachedSignature))
            blockers.Add("SIGNED_RELEASE_NOT_VERIFIED");

        // No production helper or accepted transaction journal exists yet.
        // Keep this as an unconditional release blocker.
        blockers.Add("PRODUCTION_INSTALL_HANDOFF_UNAVAILABLE");
        return new UnifiedX64InstallReadiness(candidate?.Tag, blockers.Distinct().ToArray());
    }
}
