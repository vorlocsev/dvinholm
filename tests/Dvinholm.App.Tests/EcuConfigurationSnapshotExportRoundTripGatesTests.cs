using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight synthetic, offline-only export/inspection contract tests.
/// No CEM, J2534 device, PIN or writable vehicle service is involved.
/// </summary>
public sealed class EcuConfigurationSnapshotExportRoundTripGatesTests
{
    [Fact]
    public void DefaultCaptureTimeFailsBeforeAnyJsonExport()
    {
        var snapshot = Snapshot() with { CapturedAtUtc = default };
        Assert.Throws<InvalidDataException>(() =>
            EcuConfigurationSnapshotExport.CreateJson(snapshot, true));
    }

    [Fact]
    public void NonUtcCaptureTimeFailsRatherThanCreatingUnreadableFile()
    {
        var offset = new DateTimeOffset(2026, 10, 30, 12, 0, 0,
            TimeSpan.FromHours(2));
        Assert.Throws<InvalidDataException>(() =>
            EcuConfigurationSnapshotExport.CreateJson(
                Snapshot() with { CapturedAtUtc = offset }, true));
    }

    [Fact]
    public void ValidUtcSnapshotIsImmediatelyAcceptedByStrictInspector()
    {
        var json = EcuConfigurationSnapshotExport.CreateJson(Snapshot(), true);
        var inspected = EcuConfigurationSnapshotInspection.InspectJson(json);
        Assert.Equal(new DateTimeOffset(2026, 10, 30, 10, 0, 0,
            TimeSpan.Zero), inspected.CapturedAtUtc);
        Assert.Equal("7E0", inspected.RequestCanId);
        Assert.Equal("7E8", inspected.ResponseCanId);
        Assert.False(inspected.IsRestorableBackup);
        Assert.True(inspected.ContainsPotentiallySensitiveData);
        Assert.False(inspected.ProfileMarkedVerified);
    }

    [Fact]
    public void ExactClassicCanUdsDidValueCeilingExportsAndRoundTrips()
    {
        string value = new('A', EcuConfigurationReadback.MaximumDidValueBytes * 2);
        var json = EcuConfigurationSnapshotExport.CreateJson(
            Snapshot(new EcuDidReadback(0xF187, value)), true);
        var inspected = EcuConfigurationSnapshotInspection.InspectJson(json);
        Assert.Single(inspected.Values);
        Assert.Equal(value, inspected.Values[0].ValueHex);
    }

    [Fact]
    public void SingleBytePastClassicCanDidCeilingCannotBeExported()
    {
        string value = new('A',
            (EcuConfigurationReadback.MaximumDidValueBytes + 1) * 2);
        var invalid = Snapshot(new EcuDidReadback(0xF187, value));
        Assert.Throws<InvalidDataException>(() =>
            EcuConfigurationSnapshotExport.CreateJson(invalid, true));
    }

    [Fact]
    public void LargerLegacySnapshotRemainsInspectOnlyAndNeverRestorable()
    {
        // The previous exporter accepted 4093-byte values. Do not silently
        // change the existing 16,384-character historical inspection limit.
        string value = new('B',
            (EcuConfigurationReadback.MaximumDidValueBytes + 1) * 2);
        var legacyPayload = new EcuConfigurationExportDocument(
            1, false, true, "synthetic.ecm", "Synthetic ECU",
            "7E0", "7E8", false, false,
            new DateTimeOffset(2026, 10, 30, 10, 0, 0, TimeSpan.Zero),
            [new EcuDidReadback(0xF187, value)]);
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        string payload = JsonSerializer.Serialize(legacyPayload, options);
        var sha = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
        var legacy = JsonSerializer.Serialize(
            new EcuConfigurationExportEnvelope(
                "dvinholm-diagnostic-did-snapshot", legacyPayload, sha), options);

        var inspected = EcuConfigurationSnapshotInspection.InspectJson(legacy);
        Assert.Single(inspected.Values);
        Assert.Equal(value, inspected.Values[0].ValueHex);
        Assert.False(inspected.IsRestorableBackup);
        Assert.Throws<InvalidDataException>(() =>
            EcuConfigurationSnapshotExport.CreateJson(
                Snapshot(new EcuDidReadback(0xF187, value)), true));
    }

    [Fact]
    public async Task SavedSnapshotCanBeReopenedWithNoExtraFiles()
    {
        string folder = Path.Combine(Path.GetTempPath(),
            "dvinholm-did-roundtrip-" + Guid.NewGuid().ToString("N"));
        string appFolder = Path.Combine(folder, "app");
        string userFolder = Path.Combine(folder, "user");
        Directory.CreateDirectory(appFolder);
        Directory.CreateDirectory(userFolder);
        try
        {
            var target = Path.Combine(userFolder, "diagnostic.json");
            await EcuConfigurationSnapshotExport.SaveToUserSelectedPathAsync(
                Snapshot(), target, appFolder, true);
            var inspected = await EcuConfigurationSnapshotInspection.InspectFileAsync(target);
            Assert.Single(inspected.Values);
            Assert.Equal("00AB", inspected.Values[0].ValueHex);
            Assert.False(inspected.IsRestorableBackup);
            Assert.Single(Directory.EnumerateFiles(userFolder));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task InvalidTimestampOrOverlengthValueDoesNotReplaceExistingFile()
    {
        string folder = Path.Combine(Path.GetTempPath(),
            "dvinholm-did-safe-save-" + Guid.NewGuid().ToString("N"));
        string appFolder = Path.Combine(folder, "app");
        string userFolder = Path.Combine(folder, "user");
        Directory.CreateDirectory(appFolder);
        Directory.CreateDirectory(userFolder);
        try
        {
            var target = Path.Combine(userFolder, "existing.json");
            File.WriteAllText(target, "original data");
            var invalidTime = Snapshot() with { CapturedAtUtc = default };
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                EcuConfigurationSnapshotExport.SaveToUserSelectedPathAsync(
                    invalidTime, target, appFolder, true));
            Assert.Equal("original data", File.ReadAllText(target));

            var tooLong = Snapshot(new EcuDidReadback(0xF187,
                new string('A',
                    (EcuConfigurationReadback.MaximumDidValueBytes + 1) * 2)));
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                EcuConfigurationSnapshotExport.SaveToUserSelectedPathAsync(
                    tooLong, target, appFolder, true));
            Assert.Equal("original data", File.ReadAllText(target));
            Assert.Single(Directory.EnumerateFiles(userFolder));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private static EcuConfigurationReadSnapshot Snapshot(
        params EcuDidReadback[] values) =>
        new("synthetic.ecm", "Synthetic ECU", 0x7E0, 0x7E8,
            false, false,
            new DateTimeOffset(2026, 10, 30, 10, 0, 0, TimeSpan.Zero),
            values.Length == 0 ? [new EcuDidReadback(0xF187, "00AB")] : values);
}
