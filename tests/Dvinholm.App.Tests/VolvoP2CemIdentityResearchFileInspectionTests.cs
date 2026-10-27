using System.Text;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight synthetic offline gates for a local JSON research-file inspector.
/// File paths are isolated test fixtures; no Windows picker, adapter, CAN
/// request, CEM, PIN, configuration write or real vehicle is involved.
/// </summary>
public sealed class VolvoP2CemIdentityResearchFileInspectionTests
{
    [Fact]
    public async Task ValidSavedResearchFileRoundTripsOnlyUnverifiedPartNumbers()
    {
        await WithFileAsync(ValidFile(), async path =>
        {
            var record = await VolvoP2CemIdentityResearchFileInspection
                .InspectFileAsync(path);
            Assert.Equal(2007, record.ClaimedModelYear);
            Assert.Equal("HW123", record.ClaimedHardwarePartNumber);
            Assert.Equal("SW456", record.ClaimedSoftwarePartNumber);
            Assert.Equal("DS789", record.ClaimedDiagnosticSoftwarePartNumber);
            Assert.True(record.IsUnverifiedManualClaim);
            Assert.False(record.IsRestorableBackup);
            Assert.False(record.MayAccessEcu);
        });
    }

    [Fact]
    public async Task ReviewDisplaysClaimedSourceButNeverGrantsCompatibility()
    {
        await WithFileAsync(ValidFile(fromVida: true), async path =>
        {
            var record = await VolvoP2CemIdentityResearchFileInspection
                .InspectFileAsync(path);
            var report = VolvoP2CemIdentityResearchFileInspection
                .BuildReadOnlyReview(record);

            Assert.Contains("LOCAL JSON INSPECTION (OFFLINE)", report);
            Assert.Contains("UNVERIFIED USER-TRANSCRIBED CLAIM", report);
            Assert.Contains("not independently verified", report);
            Assert.Contains("No ECU session opened", report);
            Assert.Contains("does NOT update profiles", report);
            Assert.Contains("No CAN/UDS/K-Line requests", report);
            Assert.DoesNotContain(path, report);
        });
    }

    [Fact]
    public async Task EmptyOrOversizedFileRejectedBeforeJsonParsing()
    {
        foreach (var bytes in new byte[][]
        {
            [],
            new byte[VolvoP2CemIdentityResearchExport.MaximumBytes + 1]
        })
        {
            await WithFileAsync(bytes, async path =>
                await Assert.ThrowsAsync<InvalidDataException>(() =>
                    VolvoP2CemIdentityResearchFileInspection.InspectFileAsync(path)));
        }
    }

    [Fact]
    public async Task InvalidOrUnexpectedJsonNeverBecomesManualIdentity()
    {
        var valid = Encoding.UTF8.GetString(ValidFile());
        foreach (var json in new[]
        {
            "{not JSON",
            valid.Replace("\"format\":",
                "\"unexpected\":true,\"format\":"),
            valid.Replace("\"claimedSoftwarePartNumber\":\"SW456\",", ""),
            valid.Replace("\"format\":",
                "\"format\":\"dvinholm-p2-cem-manual-identity-research\",\"format\":")
        })
        {
            await WithFileAsync(Encoding.UTF8.GetBytes(json), async path =>
                await Assert.ThrowsAsync<InvalidDataException>(() =>
                    VolvoP2CemIdentityResearchFileInspection.InspectFileAsync(path)));
        }
    }

    [Fact]
    public async Task TamperedFileClaimingVehiclePrivilegesRejected()
    {
        var valid = Encoding.UTF8.GetString(ValidFile());
        foreach (var json in new[]
        {
            valid.Replace("\"mayAccessEcu\":false", "\"mayAccessEcu\":true"),
            valid.Replace("\"isRestorableBackup\":false", "\"isRestorableBackup\":true"),
            valid.Replace("\"isUnverifiedManualClaim\":true",
                "\"isUnverifiedManualClaim\":false")
        })
        {
            await WithFileAsync(Encoding.UTF8.GetBytes(json), async path =>
                await Assert.ThrowsAsync<InvalidDataException>(() =>
                    VolvoP2CemIdentityResearchFileInspection.InspectFileAsync(path)));
        }
    }

    [Fact]
    public async Task InvalidUtf8NeverUsesReplacementCharacters()
    {
        await WithFileAsync([0x7B, 0x22, 0x80, 0x22, 0x7D], async path =>
        {
            var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
                VolvoP2CemIdentityResearchFileInspection.InspectFileAsync(path));
            Assert.Contains("invalid UTF-8", error.Message);
        });
    }

    [Fact]
    public async Task BomPrefixedFileFailsRatherThanChangingCanonicalFormat()
    {
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF }
            .Concat(ValidFile()).ToArray();
        await WithFileAsync(bytes, async path =>
        {
            var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
                VolvoP2CemIdentityResearchFileInspection.InspectFileAsync(path));
            Assert.Contains("without a BOM", error.Message);
        });
    }

    [Fact]
    public async Task PrecancelledInspectionDoesNotOpenOrReadFile()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        // The file need not even exist: the cancelled operation must
        // stop before trying to open it or observe any content.
        var absent = Path.Combine(Path.GetTempPath(),
            "absent-cem-research-" + Guid.NewGuid().ToString("N") + ".json");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            VolvoP2CemIdentityResearchFileInspection.InspectFileAsync(
                absent, cancelled.Token));
        Assert.False(File.Exists(absent));
    }

    private static byte[] ValidFile(bool fromVida = false) =>
        Encoding.UTF8.GetBytes(VolvoP2CemIdentityResearchExport.CreateJson(
            "2007", "HW123", "SW456", "DS789", fromVida));

    private static async Task WithFileAsync(
        byte[] content, Func<string, Task> assertion)
    {
        var folder = Path.Combine(Path.GetTempPath(),
            "dvinholm-cem-research-inspection-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var path = Path.Combine(folder, "user-selected.json");
            await File.WriteAllBytesAsync(path, content);
            await assertion(path);
            Assert.Single(Directory.EnumerateFiles(folder));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
