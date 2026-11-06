namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline contracts for bilingual CEM research labels and the
/// approved three-panel Aurora catalog. No ECU session or private data.
/// </summary>
public sealed class AuroraCemCatalogLocalizationTests
{
    private static IReadOnlyList<VolvoP2CemConfigOptionDefinition> Search(
        string? query, string? category = null,
        VolvoP2CemCatalogEvidenceTier tier = VolvoP2CemCatalogEvidenceTier.All) =>
        AuroraCemConfigReferenceFilter.FindLocalized(query, category, tier);

    [Fact]
    public void EveryManifestEntryHasBothDisplayLabelsWithoutChangingItsIdentity()
    {
        var all = VolvoP2CemConfigOptionManifest.Entries;
        Assert.Equal(14, all.Count);
        foreach (var definition in all)
        {
            var en = AuroraCemCatalogLocalization.OptionLabel(definition, true);
            var ru = AuroraCemCatalogLocalization.OptionLabel(definition, false);
            Assert.Equal(definition.DisplayName, en);
            Assert.NotEqual(en, ru); // Every manifest entry must be translated, not silently fall back.
            Assert.False(string.IsNullOrWhiteSpace(ru));
            Assert.False(string.IsNullOrWhiteSpace(en));
            Assert.Equal(definition, VolvoP2CemConfigOptionManifest.Find(definition.CanonicalId));
            Assert.False(definition.MayReadVehicleConfiguration);
            Assert.False(definition.MayTransmitToVehicle);
            Assert.False(definition.MayAcquirePinOrAuthenticate);
        }
        Assert.Equal("Омыватели фар", AuroraCemCatalogLocalization.OptionLabel(
            VolvoP2CemConfigOptionManifest.Find("p2.xc90.headlamp-washers")!, false));
        Assert.Equal("Дневные ходовые огни", AuroraCemCatalogLocalization.OptionLabel(
            VolvoP2CemConfigOptionManifest.Find("xc90.oem.cem.daytime-running-lamps")!, false));
    }

    [Fact]
    public void CanonicalCategoryChoicesRemainIdenticalAcrossLanguages()
    {
        var ru = AuroraCemCatalogLocalization.Categories(false);
        var en = AuroraCemCatalogLocalization.Categories(true);
        Assert.Equal(10, ru.Count);
        Assert.Equal(ru.Select(x => x.CanonicalCategory), en.Select(x => x.CanonicalCategory));
        Assert.Null(ru[0].CanonicalCategory);
        Assert.Equal("Все категории", ru[0].ToString());
        Assert.Equal("All categories", en[0].ToString());
        Assert.Contains(ru, x => x.CanonicalCategory == "VehicleSpeedPolicy" &&
            x.Label == "Ограничение скорости");
        Assert.Contains(en, x => x.CanonicalCategory == "VehicleSpeedPolicy" &&
            x.Label == "Speed limit policy");
        Assert.Contains(ru, x => x.CanonicalCategory == "XC90 CEM service menu" &&
            x.Label == "Сервисное меню CEM XC90");
    }

    [Fact]
    public void RussianQueriesFindExactTranslatedVendorAndVolvoLabels()
    {
        Assert.Equal("p2.xc90.headlamp-washers",
            Assert.Single(Search("омыватели")).CanonicalId);
        Assert.Equal("xc90.oem.cem.daytime-running-lamps",
            Assert.Single(Search("дневные ходовые")).CanonicalId);
        Assert.Equal("p2.xc90.speed-policy",
            Assert.Single(Search("ограничение скорости")).CanonicalId);
        Assert.Equal("p2.xc90.wheel-size",
            Assert.Single(Search("колёса шины")).CanonicalId);
    }

    [Fact]
    public void OriginalEnglishQueryAndMixedLanguageTokensStillWork()
    {
        Assert.Equal("p2.xc90.headlamp-washers",
            Assert.Single(Search("HEADLAMP washers")).CanonicalId);
        Assert.Equal("p2.xc90.headlamp-washers",
            Assert.Single(Search("headlamp омыватели")).CanonicalId);
        Assert.Empty(Search("headlamp service"));
        Assert.Equal(14, Search("    ").Count);
    }

    [Fact]
    public void RussianSearchCannotMixManufacturerAndVendorEvidenceTiers()
    {
        Assert.Empty(Search("дневные ходовые", tier:
            VolvoP2CemCatalogEvidenceTier.VendorAdvertisement));
        Assert.Empty(Search("омыватели", tier:
            VolvoP2CemCatalogEvidenceTier.VolvoPublishedServiceMenu));
        Assert.Equal("xc90.oem.cem.daytime-running-lamps",
            Assert.Single(Search("дневные ходовые",
                "XC90 CEM service menu",
                VolvoP2CemCatalogEvidenceTier.VolvoPublishedServiceMenu)).CanonicalId);
        Assert.Empty(Search("дневные ходовые", "ExteriorLighting"));
        Assert.Equal("p2.xc90.speed-policy",
            Assert.Single(Search("ограничение", "VehicleSpeedPolicy",
                VolvoP2CemCatalogEvidenceTier.VendorAdvertisement)).CanonicalId);
    }

    [Fact]
    public void MalformedSearchAndForeignCategoryFailClosedWithoutEcho()
    {
        Assert.Throws<InvalidDataException>(() => Search(new string('x', 81)));
        Assert.Throws<InvalidDataException>(() => Search("private\nsearch"));
        Assert.Throws<InvalidDataException>(() =>
            Search("сервис", new string('a', 81)));
        Assert.Throws<InvalidDataException>(() =>
            Search("сервис", "private\tcategory"));
        Assert.Throws<InvalidDataException>(() =>
            Search("сервис", tier: (VolvoP2CemCatalogEvidenceTier)900));
        Assert.Empty(Search("сервис", "несуществующая категория"));
    }

    [Fact]
    public void LocalizedResultsStayImmutableWithNoVehicleCapabilities()
    {
        var results = Search(null);
        var list = Assert.IsAssignableFrom<IList<VolvoP2CemConfigOptionDefinition>>(results);
        Assert.Throws<NotSupportedException>(() => list[0] = results[1]);
        Assert.Equal(
            VolvoP2CemConfigOptionManifest.Entries.Select(x => x.CanonicalId),
            results.Select(x => x.CanonicalId));
        Assert.All(results, x =>
        {
            Assert.False(x.HasVerifiedPhysicalIdentity);
            Assert.False(x.HasVerifiedConfigurationReadMapping);
            Assert.False(x.HasVerifiedConfigurationWriteMapping);
            Assert.False(x.MayEditVehicleConfiguration);
            Assert.Null(x.VerifiedRawValueMapping);
            Assert.Empty(x.VerifiedAllowedValues);
        });
    }

    [Fact]
    public void AuroraSourcePreservesSelectionAndNeverEnablesWriteButtons()
    {
        static string Read(string path)
        {
            for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
                dir is not null; dir = dir.Parent)
            {
                var file = Path.Combine(dir.FullName,
                    path.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(file)) return File.ReadAllText(file);
            }
            throw new FileNotFoundException(path);
        }

        var source = Read("src/Dvinholm.App/MainWindow.AuroraReference.cs");
        var xaml = Read("src/Dvinholm.App/MainWindow.xaml");
        var locale = Read("src/Dvinholm.App/MainWindow.UnifiedUpdates.cs");
        Assert.Contains("selectedCategory = (CemConfigCategory.SelectedItem as", source);
        Assert.Contains("AuroraCemCatalogLocalization.Categories(_unifiedEnglish)", source);
        Assert.Contains("AuroraCemConfigReferenceFilter.FindLocalized(query, category, tier)", source);
        Assert.Contains("previousSelection = (CemConfigResults.SelectedItem", source);
        Assert.Contains("AuroraCemCatalogLocalization.OptionLabel(", source);
        Assert.Contains("ApplyAuroraCemConfigLocalization();", locale);
        foreach (var control in new[] { "CemConfigCategoriesTitle", "CemConfigPageTitle",
            "CemConfigPageIntro", "CemConfigResultsTitle", "CemConfigDetailHeading",
            "CemConfigReadOnlyNotice" })
            Assert.Contains("x:Name=\"" + control + "\"", xaml);
        foreach (var control in new[] { "CemConfigBackupButton", "CemConfigCompareButton",
            "CemConfigPrepareButton" })
        {
            var start = xaml.IndexOf("x:Name=\"" + control + "\"", StringComparison.Ordinal);
            Assert.True(start >= 0);
            var button = xaml[start..xaml.IndexOf("/>", start, StringComparison.Ordinal)];
            Assert.Contains("IsEnabled=\"False\"", button);
            Assert.DoesNotContain("Click=", button);
        }
        Assert.DoesNotContain("Send(", source);
        Assert.DoesNotContain("PassThru", source);
    }
}
