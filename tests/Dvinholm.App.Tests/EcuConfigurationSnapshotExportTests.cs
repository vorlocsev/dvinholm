using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight isolated tests of explicit-consent, no-telemetry diagnostic export.
/// Data is synthetic. These do not test ECU programming or PIN retrieval.
/// </summary>
public sealed class EcuConfigurationSnapshotExportTests
{
    [Fact]
    public void RefusesSensitiveExportWithoutExplicitConsent()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            EcuConfigurationSnapshotExport.CreateJson(Snapshot(), false));
        Assert.Contains("consent", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SortsDidsAndSignsExactCanonicalPayloadWithSha256()
    {
        var json = EcuConfigurationSnapshotExport.CreateJson(
            Snapshot((0xF190, "595631"), (0xF187, "00ab")), true);
        using var envelope = JsonDocument.Parse(json);

        var payload = envelope.RootElement.GetProperty("snapshot");
        var entries = payload.GetProperty("values").EnumerateArray().ToArray();
        Assert.Equal(2, entries.Length);
        Assert.Equal((ushort)0xF187, entries[0].GetProperty("dataIdentifier").GetUInt16());
        Assert.Equal("00AB", entries[0].GetProperty("valueHex").GetString());
        Assert.Equal((ushort)0xF190, entries[1].GetProperty("dataIdentifier").GetUInt16());
        var checksum = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload.GetRawText())));
        Assert.Equal(checksum, envelope.RootElement.GetProperty("payloadSha256").GetString());
        Assert.Equal(json, EcuConfigurationSnapshotExport.CreateJson(
            Snapshot((0xF187, "00AB"), (0xF190, "595631")), true));
    }

    [Fact]
    public void MarksDocumentAsSensitiveNonRestorableAndVersioned()
    {
        using var json = JsonDocument.Parse(EcuConfigurationSnapshotExport.CreateJson(Snapshot(), true));
        Assert.Equal("dvinholm-diagnostic-did-snapshot",
            json.RootElement.GetProperty("format").GetString());
        var payload = json.RootElement.GetProperty("snapshot");
        Assert.Equal(1, payload.GetProperty("schemaVersion").GetInt32());
        Assert.True(payload.GetProperty("containsPotentiallySensitiveData").GetBoolean());
        Assert.False(payload.GetProperty("isRestorableBackup").GetBoolean());
        Assert.Equal("7E0", payload.GetProperty("requestCanId").GetString());
        Assert.Equal("7E8", payload.GetProperty("responseCanId").GetString());
        Assert.False(payload.GetProperty("profileMarkedVerified").GetBoolean());
    }

    [Fact]
    public void RejectsMalformedDuplicateOrOversizedDidValues()
    {
        var invalid = new[]
        {
            Snapshot((0xF187, "")),
            Snapshot((0xF187, "ABC")),
            Snapshot((0xF187, "ZZ")),
            Snapshot((0xF187, "AA"), (0xF187, "BB")),
            Snapshot((0xF187, new string('A', 16_386))),
            Snapshot()
        };

        foreach (var item in invalid)
            Assert.Throws<InvalidDataException>(() =>
                EcuConfigurationSnapshotExport.CreateJson(item, true));
    }

    [Fact]
    public void RejectsMoreThan128Dids()
    {
        var entries = Enumerable.Range(0, 129)
            .Select(i => ((ushort)(0xF100 + i), "AA")).ToArray();
        Assert.Throws<InvalidDataException>(() =>
            EcuConfigurationSnapshotExport.CreateJson(Snapshot(entries), true));
    }

    [Fact]
    public void RejectsInvalidProfileAndCanAddressMetadata()
    {
        var original = Snapshot();
        var invalid = new[]
        {
            original with { ProfileId = "" },
            original with { ProfileName = "" },
            original with { RequestId = 0x800 },
            original with { ResponseId = original.RequestId },
            original with { ProfileId = new string('a', 121) }
        };

        foreach (var item in invalid)
            Assert.Throws<InvalidDataException>(() =>
                EcuConfigurationSnapshotExport.CreateJson(item, true));
    }

    [Fact]
    public async Task ExplicitUserSaveWritesVerifiedUtf8JsonOutsideAppDirectory()
    {
        using var paths = new TemporaryPaths();
        var target = Path.Combine(paths.UserDirectory, "snapshot.json");
        File.WriteAllText(target, "previous snapshot");

        await EcuConfigurationSnapshotExport.SaveToUserSelectedPathAsync(
            Snapshot(), target, paths.AppDirectory, true);

        var bytes = File.ReadAllBytes(target);
        Assert.NotEmpty(bytes);
        Assert.False(bytes.Length >= 3 &&
            bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
        using var json = JsonDocument.Parse(bytes);
        Assert.True(json.RootElement.GetProperty("snapshot")
            .GetProperty("containsPotentiallySensitiveData").GetBoolean());
        Assert.Empty(Directory.EnumerateFiles(paths.UserDirectory, "*.tmp"));
    }

    [Fact]
    public async Task BlockedAndInvalidSavesPreserveExistingFile()
    {
        using var paths = new TemporaryPaths();
        var appTarget = Path.Combine(paths.AppDirectory, "cannot-save.json");
        File.WriteAllText(appTarget, "original app file");
        await Assert.ThrowsAsync<IOException>(() =>
            EcuConfigurationSnapshotExport.SaveToUserSelectedPathAsync(
                Snapshot(), appTarget, paths.AppDirectory, true));
        Assert.Equal("original app file", File.ReadAllText(appTarget));

        var target = Path.Combine(paths.UserDirectory, "previous.json");
        File.WriteAllText(target, "last complete backup");
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            EcuConfigurationSnapshotExport.SaveToUserSelectedPathAsync(
                Snapshot() with { Values = [] }, target, paths.AppDirectory, true));
        Assert.Equal("last complete backup", File.ReadAllText(target));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            EcuConfigurationSnapshotExport.SaveToUserSelectedPathAsync(
                Snapshot(), target, paths.AppDirectory, false));
        Assert.Equal("last complete backup", File.ReadAllText(target));
    }

    private static EcuConfigurationReadSnapshot Snapshot(
        params (ushort did, string hex)[]? entries)
    {
        entries ??= [(0xF187, "004142")];
        return new("synthetic.ecm", "Synthetic ECM", 0x7E0, 0x7E8, false,
            false, new DateTimeOffset(2026, 10, 27, 0, 0, 0, TimeSpan.Zero),
            entries.Select(x => new EcuDidReadback(x.did, x.hex)).ToArray());
    }

    private sealed class TemporaryPaths : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(),
            "dvinholm-ecu-export-" + Guid.NewGuid().ToString("N"));

        internal string AppDirectory => Path.Combine(_root, "app");
        internal string UserDirectory => Path.Combine(_root, "user");

        internal TemporaryPaths()
        {
            Directory.CreateDirectory(AppDirectory);
            Directory.CreateDirectory(UserDirectory);
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
    }
}
