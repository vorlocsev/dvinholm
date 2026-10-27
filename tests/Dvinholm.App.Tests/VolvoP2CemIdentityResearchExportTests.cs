using System.Text;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight synthetic offline JSON research-export gates. The input part
/// numbers are invented fixtures; this does not test a real XC90 or CEM.
/// </summary>
public sealed class VolvoP2CemIdentityResearchExportTests
{
    [Fact]
    public void RoundTripKeepsOnlyNormalizedManualUnverifiedFields()
    {
        var json = Export("2007", " hw-123 ", " sw.456 ", " diag/789 ", true);
        var loaded = VolvoP2CemIdentityResearchExport.InspectJson(json);
        Assert.Equal("dvinholm-p2-cem-manual-identity-research", loaded.Format);
        Assert.Equal(1, loaded.SchemaVersion);
        Assert.Equal(2007, loaded.ClaimedModelYear);
        Assert.Equal("HW-123", loaded.ClaimedHardwarePartNumber);
        Assert.Equal("SW.456", loaded.ClaimedSoftwarePartNumber);
        Assert.Equal("DIAG/789", loaded.ClaimedDiagnosticSoftwarePartNumber);
        Assert.True(loaded.IsUnverifiedManualClaim);
        Assert.True(loaded.UserClaimsVidaSource);
        Assert.False(loaded.MayAccessEcu);
        Assert.False(loaded.IsRestorableBackup);
    }

    [Fact]
    public void NoVinSerialPinRawPayloadOrOwnerFieldsAreExported()
    {
        var json = Export();
        foreach (var forbidden in new[] { "vin", "serial", "pin", "password",
            "rawDid", "owner", "securityAccess", "canId", "seed", "key" })
            Assert.DoesNotContain(forbidden, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("IsVerifiedPhysicalCem", json);
        Assert.Contains("\"isUnverifiedManualClaim\":true", json);
        Assert.Contains("\"mayAccessEcu\":false", json);
    }

    [Fact]
    public void RejectsMissingUnexpectedAndDuplicatedJsonFields()
    {
        var json = Export();
        var invalid = new[]
        {
            json.Replace("\"claimedSoftwarePartNumber\":\"SW456\",", ""),
            json.Replace("\"format\":", "\"extra\":0,\"format\":"),
            json.Replace("\"format\":",
                "\"format\":\"dvinholm-p2-cem-manual-identity-research\",\"format\":"),
            "[]"
        };
        foreach (var file in invalid)
        {
            Assert.NotEqual(json, file);
            Assert.Throws<InvalidDataException>(() =>
                VolvoP2CemIdentityResearchExport.InspectJson(file));
        }
    }

    [Fact]
    public void RejectsPromotedPermissionsOrUnsupportedSchema()
    {
        var json = Export();
        foreach (var file in new[]
        {
            json.Replace("\"isUnverifiedManualClaim\":true",
                "\"isUnverifiedManualClaim\":false"),
            json.Replace("\"mayAccessEcu\":false",
                "\"mayAccessEcu\":true"),
            json.Replace("\"isRestorableBackup\":false",
                "\"isRestorableBackup\":true"),
            json.Replace("\"schemaVersion\":1", "\"schemaVersion\":2"),
            json.Replace("dvinholm-p2-cem-manual-identity-research",
                "untrusted-cem-config")
        })
        {
            Assert.NotEqual(json, file);
            Assert.Throws<InvalidDataException>(() =>
                VolvoP2CemIdentityResearchExport.InspectJson(file));
        }
    }

    [Fact]
    public void RejectsWrongTypesNoncanonicalAndVinLikePartNumbers()
    {
        var json = Export();
        foreach (var file in new[]
        {
            json.Replace("\"claimedModelYear\":2007", "\"claimedModelYear\":\"2007\""),
            json.Replace("\"claimedModelYear\":2007", "\"claimedModelYear\":2018"),
            json.Replace("\"claimedHardwarePartNumber\":\"HW123\"",
                "\"claimedHardwarePartNumber\":\"hw123\""),
            json.Replace("\"claimedHardwarePartNumber\":\"HW123\"",
                "\"claimedHardwarePartNumber\":\"TESTVIN1234567890\""),
            json.Replace("\"userClaimsVidaSource\":false",
                "\"userClaimsVidaSource\":\"yes\"")
        })
        {
            Assert.NotEqual(json, file);
            Assert.Throws<InvalidDataException>(() =>
                VolvoP2CemIdentityResearchExport.InspectJson(file));
        }
    }

    [Fact]
    public void RefusesOversizeMalformedAndEmptyResearchDocuments()
    {
        Assert.Throws<InvalidDataException>(() =>
            VolvoP2CemIdentityResearchExport.InspectJson(
                new string('A', VolvoP2CemIdentityResearchExport.MaximumBytes + 1)));
        Assert.Throws<InvalidDataException>(() =>
            VolvoP2CemIdentityResearchExport.InspectJson("{this is not JSON"));
        Assert.Throws<ArgumentException>(() =>
            VolvoP2CemIdentityResearchExport.InspectJson(""));
        Assert.Throws<InvalidDataException>(() =>
            Export("2015", "HW123", "SW456", "DS789", false));
        Assert.Throws<InvalidDataException>(() =>
            Export("2007", "TESTVIN1234567890", "SW456", "DS789", false));
    }

    [Fact]
    public async Task SaveIsExplicitUtf8AndCanBeInspectedWithoutSideEffects()
    {
        var directory = Path.Combine(Path.GetTempPath(),
            "dvinholm-vida-research-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var saved = Path.Combine(directory, "cem-identity.json");
            var applicationDirectory = Path.Combine(directory, "app");
            Directory.CreateDirectory(applicationDirectory);
            Assert.Empty(Directory.EnumerateFiles(directory));
            await VolvoP2CemIdentityResearchExport.SaveToUserSelectedPathAsync(
                "2007", "HW123", "SW456", "DS789", true, saved,
                applicationDirectory);
            var bytes = File.ReadAllBytes(saved);
            Assert.False(bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }));
            var inspected = VolvoP2CemIdentityResearchExport.InspectJson(
                new UTF8Encoding(false, true).GetString(bytes));
            Assert.True(inspected.IsUnverifiedManualClaim);
            Assert.Equal("HW123", inspected.ClaimedHardwarePartNumber);
            Assert.Single(Directory.EnumerateFiles(directory));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task BadDataOrAppFolderCannotOverwriteAnExistingReport()
    {
        var directory = Path.Combine(Path.GetTempPath(),
            "dvinholm-vida-blocked-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var protectedPath = Path.Combine(directory, "cem.json");
            File.WriteAllText(protectedPath, "original", Encoding.UTF8);
            Assert.Throws<IOException>(() =>
                VolvoP2CemIdentityResearchExport.SaveToUserSelectedPathAsync(
                    "2007", "HW123", "SW456", "DS789", false,
                    protectedPath, directory));
            Assert.Equal("original", File.ReadAllText(protectedPath));
            var outside = Path.Combine(Path.GetTempPath(), 
                "dvinholm-vida-invalid-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                await Assert.ThrowsAsync<InvalidDataException>(() =>
                    VolvoP2CemIdentityResearchExport.SaveToUserSelectedPathAsync(
                        "2007", "SECRET VALUE", "SW456", "DS789", false,
                        outside, directory));
                Assert.False(File.Exists(outside));
            }
            finally { if (File.Exists(outside)) File.Delete(outside); }
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static string Export(string year = "2007",
        string hardware = "HW123", string software = "SW456",
        string diagnostics = "DS789", bool fromVida = false) =>
        VolvoP2CemIdentityResearchExport.CreateJson(
            year, hardware, software, diagnostics, fromVida);
}
