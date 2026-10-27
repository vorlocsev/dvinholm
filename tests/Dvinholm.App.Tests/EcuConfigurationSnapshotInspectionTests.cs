using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline-only tests for reading previously exported diagnostic JSON.
/// A matching SHA-256 is corruption detection, never ECU identity/authentication.
/// </summary>
public sealed class EcuConfigurationSnapshotInspectionTests
{
    [Fact]
    public void ValidExportRoundTripsWithImmutableReadOnlyValues()
    {
        var result = EcuConfigurationSnapshotInspection.InspectJson(Export(
            (0xF189, "0102"), (0xF187, "00AB")));

        Assert.Equal(1, result.SchemaVersion);
        Assert.False(result.IsRestorableBackup);
        Assert.True(result.ContainsPotentiallySensitiveData);
        Assert.Equal("synthetic.ecm", result.ProfileId);
        Assert.Equal("7E0", result.RequestCanId);
        Assert.Equal("7E8", result.ResponseCanId);
        Assert.Equal(new ushort[] { 0xF187, 0xF189 },
            result.Values.Select(x => x.DataIdentifier));

        var list = Assert.IsAssignableFrom<IList<EcuDidReadback>>(result.Values);
        Assert.True(list.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => list.Add(new EcuDidReadback(0xF190, "AA")));
    }

    [Fact]
    public void ChangedDidBytesAreRejectedWhenSha256DoesNotMatch()
    {
        var original = Export((0xF187, "00AB"));
        var edited = original.Replace("\"valueHex\":\"00AB\"",
            "\"valueHex\":\"00AC\"", StringComparison.Ordinal);
        Assert.NotEqual(original, edited);
        var ex = Assert.Throws<InvalidDataException>(() =>
            EcuConfigurationSnapshotInspection.InspectJson(edited));
        Assert.Contains("SHA-256", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsDuplicateUnexpectedOrMissingJsonProperties()
    {
        var original = Export();
        var broken = new[]
        {
            original.Replace("\"format\":", "\"format\":\"double\",\"format\":",
                StringComparison.Ordinal),
            original.Replace("\"schemaVersion\":1",
                "\"schemaVersion\":1,\"schemaVersion\":1", StringComparison.Ordinal),
            original.Replace("\"schemaVersion\":1",
                "\"schemaVersion\":1,\"surprise\":true", StringComparison.Ordinal),
            original.Replace("\"containsPotentiallySensitiveData\":true,",
                "", StringComparison.Ordinal),
            original.Replace("\"dataIdentifier\":61831,",
                "\"dataIdentifier\":61831,\"dataIdentifier\":61831,",
                StringComparison.Ordinal)
        };

        foreach (var json in broken)
        {
            Assert.NotEqual(original, json);
            Assert.Throws<InvalidDataException>(() =>
                EcuConfigurationSnapshotInspection.InspectJson(json));
        }
    }

    [Fact]
    public void MatchingChecksumStillRejectsUnsafeFlagsOrUnsupportedSchema()
    {
        var original = Export();
        var mutations = new[]
        {
            original.Replace("\"schemaVersion\":1", "\"schemaVersion\":2", StringComparison.Ordinal),
            original.Replace("\"isRestorableBackup\":false",
                "\"isRestorableBackup\":true", StringComparison.Ordinal),
            original.Replace("\"containsPotentiallySensitiveData\":true",
                "\"containsPotentiallySensitiveData\":false", StringComparison.Ordinal)
        };

        foreach (var changed in mutations)
            Assert.Throws<InvalidDataException>(() =>
                EcuConfigurationSnapshotInspection.InspectJson(Rehash(changed)));
    }

    [Fact]
    public void MatchingChecksumStillRejectsWrongCanAndDuplicateOrUnsortedDids()
    {
        var original = Export((0xF187, "00AB"), (0xF189, "0102"));
        var mutations = new[]
        {
            original.Replace("\"requestCanId\":\"7E0\"",
                "\"requestCanId\":\"800\"", StringComparison.Ordinal),
            original.Replace("\"dataIdentifier\":61833",
                "\"dataIdentifier\":61831", StringComparison.Ordinal),
            original.Replace("\"dataIdentifier\":61831",
                "\"dataIdentifier\":61834", StringComparison.Ordinal),
            original.Replace("\"valueHex\":\"00AB\"",
                "\"valueHex\":\"00ab\"", StringComparison.Ordinal)
        };

        foreach (var changed in mutations)
            Assert.Throws<InvalidDataException>(() =>
                EcuConfigurationSnapshotInspection.InspectJson(Rehash(changed)));
    }

    [Fact]
    public void ExcessiveInputAndRehashedOversizedDidAreRejected()
    {
        Assert.Throws<InvalidDataException>(() =>
            EcuConfigurationSnapshotInspection.InspectJson(
                new string('A', EcuConfigurationSnapshotInspection.MaximumFileBytes + 1)));

        var original = Export((0xF187, "00AB"));
        var tooLong = original.Replace("\"valueHex\":\"00AB\"",
            "\"valueHex\":\"" + new string('A', 16_386) + "\"",
            StringComparison.Ordinal);
        Assert.Throws<InvalidDataException>(() =>
            EcuConfigurationSnapshotInspection.InspectJson(Rehash(tooLong)));
    }

    [Fact]
    public async Task InspectFileOnlyReadsAndNeverCreatesNewFiles()
    {
        var dir = Path.Combine(Path.GetTempPath(),
            "dvinholm-ecu-inspect-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "diagnostic.json");
            var content = Export();
            File.WriteAllText(path, content, new UTF8Encoding(false));
            var result = await EcuConfigurationSnapshotInspection.InspectFileAsync(path);

            Assert.Equal("synthetic.ecm", result.ProfileId);
            Assert.Equal(content, File.ReadAllText(path));
            Assert.Single(Directory.EnumerateFiles(dir));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task FileInspectorRejectsInvalidUtf8OversizeAndCancellation()
    {
        var dir = Path.Combine(Path.GetTempPath(),
            "dvinholm-ecu-inspect-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "bad.json");
            File.WriteAllBytes(path, [0xC3, 0x28]);
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                EcuConfigurationSnapshotInspection.InspectFileAsync(path));

            using (var stream = File.Open(path, FileMode.Create))
                stream.SetLength(EcuConfigurationSnapshotInspection.MaximumFileBytes + 1);
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                EcuConfigurationSnapshotInspection.InspectFileAsync(path));

            using var cts = new CancellationTokenSource();
            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                EcuConfigurationSnapshotInspection.InspectFileAsync(path, cts.Token));

            Assert.Single(Directory.EnumerateFiles(dir));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static string Export(params (ushort did, string value)[] values)
    {
        if (values.Length == 0)
            values = [(0xF187, "00AB")];
        var snapshot = new EcuConfigurationReadSnapshot(
            "synthetic.ecm", "Synthetic ECM", 0x7E0, 0x7E8, false, false,
            new DateTimeOffset(2026, 10, 27, 5, 0, 0, TimeSpan.Zero),
            values.Select(x => new EcuDidReadback(x.did, x.value)).ToArray());

        return EcuConfigurationSnapshotExport.CreateJson(snapshot, true);
    }

    private static string Rehash(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var payload = doc.RootElement.GetProperty("snapshot").GetRawText();
        var oldHash = doc.RootElement.GetProperty("payloadSha256").GetString()!;
        var newHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
        return json.Replace(oldHash, newHash, StringComparison.Ordinal);
    }
}
