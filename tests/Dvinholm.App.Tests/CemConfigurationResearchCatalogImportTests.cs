using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Dvinholm.App.Tests;

/// <summary>
/// Standalone, synthetic JSON inputs only. No CEM, proprietary SVDB bytes,
/// physical vehicle identity, diagnostic command or configuration write.
/// </summary>
public sealed class CemConfigurationResearchCatalogImportTests
{
    private static string ValidJson(string dataFormat = "P2B9Fc", int index = 1) =>
        JsonSerializer.Serialize(new
        {
            format = CemConfigurationResearchCatalogImport.Format,
            schemaVersion = 1,
            isUnverifiedResearch = true,
            isRestorableBackup = false,
            mayAccessEcu = false,
            claims = new[]
            {
                new
                {
                    dataFormat, index1Based = index,
                    hardwarePartNumber = "30682981",
                    softwareRevision = "SW-A",
                    claimedMeaning = "Synthetic unverified option",
                    evidenceReference = "Private reference from a synthetic test"
                }
            }
        });

    private static JsonObject Mutable(string? json = null) =>
        JsonNode.Parse(json ?? ValidJson())!.AsObject();

    private static void Reject(string json) =>
        Assert.Throws<InvalidDataException>(() =>
            CemConfigurationResearchCatalogImport.InspectJson(json));

    [Fact]
    public void ValidP2AndP3ClaimsPreserveExactRawScope()
    {
        var root = Mutable();
        var claims = root["claims"]!.AsArray();
        var p3 = claims[0]!.DeepClone();
        p3["dataFormat"] = "P3F106";
        p3["index1Based"] = 252;
        claims.Add(p3);

        var doc = CemConfigurationResearchCatalogImport.InspectJson(root.ToJsonString());
        Assert.Equal(1, doc.SchemaVersion);
        Assert.True(doc.IsUnverifiedResearch);
        Assert.Equal(2, doc.Claims.Count);
        Assert.Equal(CemConfigurationIndexedFormat.P2B9Fc, doc.Claims[0].Format);
        Assert.Equal(CemConfigurationIndexedFormat.P3F106, doc.Claims[1].Format);
        Assert.Equal(252, doc.Claims[1].Index1Based);
        Assert.False(doc.HasAuthenticatedCemIdentity);
        Assert.False(doc.HasVerifiedParameterMapping);
        Assert.False(doc.MayReadOrWriteVehicle);
        var rows = Assert.IsAssignableFrom<IList<CemConfigurationMappingResearchClaim>>(doc.Claims);
        Assert.Throws<NotSupportedException>(() => rows[0] = rows[1]);
    }

    [Fact]
    public void ImportedClaimCanOnlyYieldUnverifiedResearchMatch()
    {
        var doc = CemConfigurationResearchCatalogImport.InspectJson(ValidJson());
        var body = new byte[142];
        body[0] = 140;
        body[2] = 0xA5;
        body[1] = 0xA5;
        var snapshot = CemConfigurationIndexedReview.InspectP2B9Fc(body);
        var result = CemConfigurationMappingResearch.Assess(
            snapshot, 1, "30682981", "SW-A", doc.Claims);
        Assert.Equal(CemConfigurationMappingResearchKind.UnverifiedResearchMatch, result.Kind);
        Assert.Equal("A5", result.UnverifiedRawValueHex);
        Assert.False(result.HasVerifiedParameterMapping);
        Assert.False(result.MayReadLiveConfiguration);
        Assert.False(result.MayPrepareOrWriteConfiguration);
    }

    [Fact]
    public void DifferentFormatOrRevisionCannotSilentlyMatch()
    {
        var doc = CemConfigurationResearchCatalogImport.InspectJson(ValidJson());
        var body = new byte[254];
        body[0] = 252;
        var snapshot = CemConfigurationIndexedReview.InspectP3F106(body);
        Assert.Equal(CemConfigurationMappingResearchKind.NoExactResearchMatch,
            CemConfigurationMappingResearch.Assess(snapshot, 1, "30682981", "SW-A", doc.Claims).Kind);
        var p2 = new byte[142];
        p2[0] = 140;
        Assert.Equal(CemConfigurationMappingResearchKind.NoExactResearchMatch,
            CemConfigurationMappingResearch.Assess(
                CemConfigurationIndexedReview.InspectP2B9Fc(p2), 1,
                "30682982", "SW-A", doc.Claims).Kind);
    }

    [Fact]
    public void UnknownDuplicateAndMissingRootPropertiesAreRejected()
    {
        Reject(ValidJson().TrimEnd('}') + ",\"vin\":\"FAKE-VIN\"}");
        Reject(ValidJson().TrimEnd('}') + ",\"format\":\"spoofed\"}");
        var root = Mutable();
        root.Remove("claims");
        Reject(root.ToJsonString());
        root = Mutable();
        root["pin"] = "not allowed";
        Reject(root.ToJsonString());
    }

    [Fact]
    public void NestedDuplicateAndUnexpectedExecutableFieldsAreRejected()
    {
        Reject(ValidJson().Replace("\"index1Based\":1",
            "\"index1Based\":1,\"index1Based\":2", StringComparison.Ordinal));
        var root = Mutable();
        root["claims"]![0]!["requestCanId"] = "7E0";
        Reject(root.ToJsonString());
        root = Mutable();
        root["claims"]![0]!.AsObject().Remove("evidenceReference");
        Reject(root.ToJsonString());
    }

    [Fact]
    public void HeaderFlagsVersionAndTypesAreStrict()
    {
        foreach (var mutate in new Action<JsonObject>[]
        {
            root => root["schemaVersion"] = 2,
            root => root["schemaVersion"] = "1",
            root => root["isUnverifiedResearch"] = false,
            root => root["isRestorableBackup"] = true,
            root => root["mayAccessEcu"] = true,
            root => root["format"] = "another-format",
            root => root["claims"] = "not-an-array"
        })
        {
            var root = Mutable();
            mutate(root);
            Reject(root.ToJsonString());
        }
    }

    [Fact]
    public void IncorrectIndexAndFormatStringCannotPass()
    {
        Reject(ValidJson(dataFormat: "P2", index: 1));
        Reject(ValidJson(dataFormat: "P2B9Fc", index: 0));
        Reject(ValidJson(dataFormat: "P2B9Fc", index: 141));
        Reject(ValidJson(dataFormat: "P3F106", index: 253));
        var root = Mutable();
        root["claims"]![0]!["index1Based"] = "1";
        Reject(root.ToJsonString());
    }

    [Fact]
    public void DuplicateScopeOrInvalidEvidenceRejectsEntireCatalog()
    {
        var root = Mutable();
        var claims = root["claims"]!.AsArray();
        claims.Add(claims[0]!.DeepClone());
        Reject(root.ToJsonString());

        root = Mutable();
        root["claims"]![0]!["evidenceReference"] = "too short";
        Reject(root.ToJsonString());

        root = Mutable();
        root["claims"]![0]!["claimedMeaning"] = "Unsafe\ntext";
        Reject(root.ToJsonString());
    }

    [Fact]
    public void EmptyOrExcessivelyLargeClaimSetsAndJsonAreRejected()
    {
        var root = Mutable();
        root["claims"] = new JsonArray();
        Reject(root.ToJsonString());

        root = Mutable();
        var original = root["claims"]![0]!.DeepClone();
        var claims = root["claims"]!.AsArray();
        for (var i = 2; i <= 65; i++)
        {
            var next = original.DeepClone();
            next["index1Based"] = i;
            claims.Add(next);
        }
        Reject(root.ToJsonString());
        Reject(new string('x', CemConfigurationResearchCatalogImport.MaximumFileBytes + 1));
        Reject(" ");
        Reject(ValidJson() + "{}");
    }

    [Fact]
    public void FileImportIsReadOnlyAndRedactedReportLeaksNoSourceData()
    {
        var path = TempPath();
        try
        {
            var bytes = new UTF8Encoding(false).GetBytes(ValidJson());
            File.WriteAllBytes(path, bytes);
            var originalSha = SHA256.HashData(File.ReadAllBytes(path));
            var doc = CemConfigurationResearchCatalogImport.InspectFileAsync(path)
                .GetAwaiter().GetResult();
            var finalSha = SHA256.HashData(File.ReadAllBytes(path));
            Assert.True(originalSha.SequenceEqual(finalSha));
            var report = CemConfigurationResearchCatalogImport.BuildRedactedReport(doc);
            Assert.Contains("OFFLINE", report);
            Assert.Contains("Total claims: 1", report);
            Assert.Contains("PIN, configuration reads and writes: DISABLED", report);
            Assert.DoesNotContain("30682981", report);
            Assert.DoesNotContain("SW-A", report);
            Assert.DoesNotContain("Synthetic unverified option", report);
            Assert.DoesNotContain("Private reference", report);
            Assert.DoesNotContain(path, report);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task BadExtensionBomAndInvalidUtf8AreRejected()
    {
        var wrong = TempPath(".svdb");
        var bom = TempPath();
        var invalid = TempPath();
        try
        {
            File.WriteAllText(wrong, ValidJson());
            File.WriteAllBytes(bom, [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(ValidJson())]);
            File.WriteAllBytes(invalid, [0xC3, 0x28]);
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                CemConfigurationResearchCatalogImport.InspectFileAsync(wrong));
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                CemConfigurationResearchCatalogImport.InspectFileAsync(bom));
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                CemConfigurationResearchCatalogImport.InspectFileAsync(invalid));
        }
        finally { File.Delete(wrong); File.Delete(bom); File.Delete(invalid); }
    }

    [Fact]
    public async Task OversizedFileIsRejectedBeforeAllocationAndCanceledImportDoesNotStart()
    {
        var path = TempPath();
        try
        {
            File.WriteAllBytes(path,
                new byte[CemConfigurationResearchCatalogImport.MaximumFileBytes + 1]);
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                CemConfigurationResearchCatalogImport.InspectFileAsync(path));
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                CemConfigurationResearchCatalogImport.InspectFileAsync(path, cts.Token));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void NoSourceDataIsEmbeddedAndNoEcuExecutionPathExists()
    {
        var source = Source("src/Dvinholm.App/CemConfigurationResearchCatalogImport.cs");
        Assert.Contains("MaximumFileBytes = 32 * 1024", source);
        Assert.Contains("FileMode.Open, FileAccess.Read, FileShare.Read", source);
        Assert.Contains("CemConfigurationMappingResearch.Validate(claims)", source);
        foreach (var forbidden in new[]
        {
            "PassThru", "J2534", "SendAsync(", "Connect(",
            "ReadCemPin(", "SecurityAccess(", "WriteConfiguration(",
            "SqliteConnection", "Decrypt(", "FileMode.Create",
            "File.ReadAllBytes(", "FileAccess.Write"
        })
            Assert.DoesNotContain(forbidden, source);
        Assert.Empty(CemConfigurationMappingResearch.ProductionMappings);
        Assert.False(new VolvoP2CemConfigurationReadPreflightAssessment(
            VolvoP2CemSourceSetKind.NoInScopeClaims,
            Array.Empty<VolvoP2ServiceEvidenceGap>(), false, false)
            .MayReadLiveCemConfiguration);
    }

    private static string TempPath(string extension = ".json") =>
        Path.Combine(Path.GetTempPath(),
            "Dvinholm-Cem-Research-" + Guid.NewGuid().ToString("N") + extension);

    private static string Source(string relative)
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName,
                relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path)) return File.ReadAllText(path);
        }
        throw new FileNotFoundException(relative);
    }
}
