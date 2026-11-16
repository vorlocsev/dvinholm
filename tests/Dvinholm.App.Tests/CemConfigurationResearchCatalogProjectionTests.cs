using System.Security.Cryptography;

namespace Dvinholm.App.Tests;

public sealed class CemConfigurationResearchCatalogProjectionTests
{
    private static string Json() =>
        """
        {"format":"dvinholm-cem-index-mapping-research",
         "schemaVersion":1,"isUnverifiedResearch":true,
         "isRestorableBackup":false,"mayAccessEcu":false,
         "claims":[
          {"dataFormat":"P2B9Fc","index1Based":1,"hardwarePartNumber":"30682981",
           "softwareRevision":"SW-A","claimedMeaning":"P2 synthetic candidate",
           "evidenceReference":"Synthetic private citation A"},
          {"dataFormat":"P2B9Fc","index1Based":140,"hardwarePartNumber":"30682981",
           "softwareRevision":"SW-A","claimedMeaning":"P2 last ordinal",
           "evidenceReference":"Synthetic private citation B"},
          {"dataFormat":"P3F106","index1Based":252,"hardwarePartNumber":"30682981",
           "softwareRevision":"SW-A","claimedMeaning":"P3 last ordinal",
           "evidenceReference":"Synthetic private citation C"}
         ]}
        """;

    private static CemConfigurationResearchCatalogDocument Document() =>
        CemConfigurationResearchCatalogImport.InspectJson(Json());

    private static byte[] Body(int count)
    {
        var result = new byte[count + 2];
        result[0] = checked((byte)count);
        result[2] = 0x34;
        result[^1] = 0x56;
        result[1] = unchecked((byte)(0x34 + 0x56));
        return result;
    }

    [Fact]
    public void AllAndFilteredResearchClaimsKeepDifferentP2AndP3Scopes()
    {
        var doc = Document();
        var all = CemConfigurationResearchCatalogProjection.Filter(doc, null);
        Assert.Equal(3, all.Count);
        Assert.Equal(2, CemConfigurationResearchCatalogProjection.Filter(doc,
            CemConfigurationIndexedFormat.P2B9Fc).Count);
        var p3 = CemConfigurationResearchCatalogProjection.Filter(doc,
            CemConfigurationIndexedFormat.P3F106);
        Assert.Single(p3);
        Assert.Equal(252, p3[0].Index1Based);
        Assert.Null(p3[0].LocalRawHex);
        Assert.False(p3[0].HasVerifiedMeaning);
        Assert.False(p3[0].MatchesAuthenticatedCem);
        Assert.False(p3[0].MayReadOrWriteVehicle);
    }

    [Fact]
    public void LocalP2BodyShowsOnlyMatchingOrdinalRawBytes()
    {
        var body = CemConfigurationIndexedReview.InspectP2B9Fc(Body(140));
        var all = CemConfigurationResearchCatalogProjection.Filter(Document(), null, body);
        Assert.Equal("34", all[0].LocalRawHex);
        Assert.Equal("56", all[1].LocalRawHex);
        Assert.Null(all[2].LocalRawHex);
        Assert.Contains("локально, не подтверждено", all[0].Display);
    }

    [Fact]
    public void P3BodyCannotBeShownAsP2OptionValue()
    {
        var body = CemConfigurationIndexedReview.InspectP3F106(Body(252));
        var rows = CemConfigurationResearchCatalogProjection.Filter(Document(), null, body);
        Assert.Null(rows[0].LocalRawHex);
        Assert.Null(rows[1].LocalRawHex);
        Assert.Equal("56", rows[2].LocalRawHex);
    }

    [Theory]
    [InlineData(false, "исследование, не подтверждено")]
    [InlineData(true, "unverified research")]
    public void ClaimsWithoutRawBytesHaveLocalizedUnverifiedStatus(
        bool english, string phrase)
    {
        var rows = CemConfigurationResearchCatalogProjection.Filter(
            Document(), null, english: english);
        Assert.Equal(3, rows.Count);
        Assert.All(rows, row => Assert.Contains(phrase, row.Display));
        Assert.All(rows, row => Assert.Null(row.LocalRawHex));
    }

    [Theory]
    [InlineData(false, "локально, не подтверждено")]
    [InlineData(true, "local, unverified")]
    public void OnlyMatchingLocalBodyHasLocalizedRawByteSuffix(
        bool english, string phrase)
    {
        var body = CemConfigurationIndexedReview.InspectP2B9Fc(Body(140));
        var rows = CemConfigurationResearchCatalogProjection.Filter(
            Document(), null, body, english);
        Assert.Equal("34", rows[0].LocalRawHex);
        Assert.Contains(phrase, rows[0].Display);
        Assert.Null(rows[2].LocalRawHex);
        Assert.DoesNotContain(phrase, rows[2].Display);
    }

    [Fact]
    public void WrappingRowTemplateAndScrollableDetailsAreWiredInWinUi()
    {
        var xaml = Source("src/Dvinholm.App/MainWindow.xaml");
        Assert.Contains("x:Key=\"CemResearchCatalogRowTemplate\"", xaml);
        Assert.Contains("Text=\"{Binding Display}\" TextWrapping=\"Wrap\"", xaml);
        var ui = Source("src/Dvinholm.App/MainWindow.CemResearchCatalog.cs");
        Assert.Contains("ItemTemplate = (DataTemplate)RootLayout.Resources[", ui);
        Assert.Contains("CemResearchCatalogRowTemplate", ui);
        Assert.Contains("MaxHeight = 142", ui);
        Assert.Contains("catalog, selectedFormat, offlineBody, _unifiedEnglish", ui);
        Assert.DoesNotContain("DisplayMemberPath =", ui);
    }

    [Fact]
    public void FilterNeverMutatesOrPromotesProductionCatalog()
    {
        var doc = Document();
        var all = CemConfigurationResearchCatalogProjection.Filter(doc, null);
        var p2 = CemConfigurationResearchCatalogProjection.Filter(doc,
            CemConfigurationIndexedFormat.P2B9Fc);
        var list = Assert.IsAssignableFrom<IList<CemConfigurationResearchViewRow>>(p2);
        Assert.Throws<NotSupportedException>(() => list[0] = all[2]);
        Assert.Equal(3, doc.Claims.Count);
        Assert.Empty(CemConfigurationMappingResearch.ProductionMappings);
        Assert.Empty(VolvoP2CemParameterEvidenceCatalog.ProductionRecords);
    }

    [Fact]
    public void ForgedCatalogFlagsAreRejectedBeforeProjection()
    {
        var doc = Document() with { MayAccessEcu = true };
        Assert.Throws<InvalidDataException>(() =>
            CemConfigurationResearchCatalogProjection.Filter(doc, null));
        Assert.Throws<InvalidDataException>(() =>
            CemConfigurationResearchCatalogProjection.Filter(Document(),
                (CemConfigurationIndexedFormat)99));
    }

    [Fact]
    public void TruncatedOrReorderedIndexedSnapshotIsRejected()
    {
        var bogus = new CemConfigurationIndexedSnapshot(
            CemConfigurationIndexedFormat.P2B9Fc, new string('F', 64),
            [new CemConfigurationIndexedValue(1, 0xAB)]);
        Assert.Throws<InvalidDataException>(() =>
            CemConfigurationResearchCatalogProjection.Filter(Document(), null, bogus));
    }

    [Fact]
    public void UserChosenBodyFileMustBeExactValidatedRawData()
    {
        var path = Path.Combine(Path.GetTempPath(),
            "Dvinholm-Cem-Body-" + Guid.NewGuid().ToString("N") + ".bin");
        try
        {
            var body = Body(140);
            File.WriteAllBytes(path, body);
            var before = SHA256.HashData(File.ReadAllBytes(path));
            var indexed = CemConfigurationOfflineBlockInspection.InspectFileAsync(path)
                .GetAwaiter().GetResult();
            Assert.Equal(140, indexed.Values.Count);
            var after = SHA256.HashData(File.ReadAllBytes(path));
            Assert.True(before.SequenceEqual(after));
            Assert.False(indexed.IsRestorableBackup);
            body[1] ^= 1;
            File.WriteAllBytes(path, body);
            Assert.Throws<FormatException>(() =>
                CemConfigurationOfflineBlockInspection.InspectFileAsync(path)
                    .GetAwaiter().GetResult());
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void P3BodyFileIsParsedWithoutAutoIdentifyingAnEcu()
    {
        var path = Path.Combine(Path.GetTempPath(),
            "Dvinholm-Cem-P3-" + Guid.NewGuid().ToString("N") + ".bin");
        try
        {
            File.WriteAllBytes(path, Body(252));
            var indexed = CemConfigurationOfflineBlockInspection.InspectFileAsync(path)
                .GetAwaiter().GetResult();
            Assert.Equal(CemConfigurationIndexedFormat.P3F106, indexed.Format);
            Assert.False(indexed.HasVerifiedCemIdentity);
            Assert.False(indexed.MayReadOrWriteVehicle);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task BinaryInspectionRejectsOtherFileTypesAndLengths()
    {
        var path = Path.Combine(Path.GetTempPath(),
            "Dvinholm-Cem-BadBody-" + Guid.NewGuid().ToString("N") + ".bin");
        var wrong = path + ".json";
        try
        {
            File.WriteAllBytes(path, new byte[257]); // UDS header must be stripped.
            File.WriteAllBytes(wrong, Body(140));
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                CemConfigurationOfflineBlockInspection.InspectFileAsync(path));
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                CemConfigurationOfflineBlockInspection.InspectFileAsync(wrong));
        }
        finally { File.Delete(path); File.Delete(wrong); }
    }

    [Fact]
    public void XamlProvidesLocalOnlyButtonWithoutEnablingWriteControls()
    {
        var xaml = Source("src/Dvinholm.App/MainWindow.xaml");
        Assert.Contains("x:Name=\"CemConfigResearchJsonButton\"", xaml);
        Assert.Contains("Click=\"CemConfigResearchJson_Click\"", xaml);
        Assert.Contains("x:Name=\"CemConfigReadButton\"", xaml);
        Assert.Contains("x:Name=\"CemConfigWriteButton\"", xaml);
        var code = Source("src/Dvinholm.App/MainWindow.CemResearchCatalog.cs");
        Assert.Contains("CemConfigurationResearchCatalogImport.InspectFileAsync", code);
        Assert.Contains("CemConfigurationOfflineBlockInspection.InspectFileAsync", code);
        Assert.Contains("CemConfigurationResearchCatalogProjection.Filter", code);
        Assert.Contains("catch (FormatException)", code);
        Assert.Contains("Corrupted .bin: invalid value count or checksum.", code);
        Assert.DoesNotContain("PassThru", code);
        Assert.DoesNotContain("SendAsync(", code);
        Assert.DoesNotContain("WriteConfiguration(", code);
        Assert.DoesNotContain("UdsClient", code);
    }

    [Fact]
    public void ViewerExposesStableNonSensitiveWinUiAutomationIds()
    {
        var source = Source("src/Dvinholm.App/MainWindow.CemResearchCatalog.cs");
        foreach (var id in new[] {
            "DvinholmCemResearchDialog",
            "DvinholmCemResearchGenerationFilter",
            "DvinholmCemResearchClaimsList",
            "DvinholmCemResearchSafetyWarning",
            "DvinholmCemResearchClaimDetails"
        })
            Assert.Contains(id, source);
        Assert.Contains("AutomationProperties.SetName(dialog, L(", source);
        Assert.DoesNotContain("SetAutomationId(dialog, selected.Path", source);
    }

    [Fact]
    public void ExactHeadCemScreenshotHelperIsOperatorOnly()
    {
        var script = Source("eng/Capture-CemResearchViewerWinUI3.ps1");
        Assert.Contains("IConfirmSyntheticNoSecrets", script);
        Assert.Contains("Aurora-x64-build-evidence.json", script);
        Assert.Contains("DvinholmCemResearchDialog", script);
        Assert.Contains("GetDpiForWindow", script);
        Assert.Contains("Get-FileHash", script);
        Assert.Contains("CopyFromScreen", script);
        Assert.Contains("Get-Anchored", script.Replace("Get-CemAnchor", "Get-Anchored"));
        Assert.DoesNotContain("PassThru", script);
        Assert.DoesNotContain("FileOpenPicker", script);
        Assert.DoesNotContain("Invoke-RestMethod", script);
    }

    private static string Source(string relative)
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var file = Path.Combine(dir.FullName,
                relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(file)) return File.ReadAllText(file);
        }
        throw new FileNotFoundException(relative);
    }
}
