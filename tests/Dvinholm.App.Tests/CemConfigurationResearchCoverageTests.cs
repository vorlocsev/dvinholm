namespace Dvinholm.App.Tests;

/// <summary>
/// No real Volvo mappings, no CAN/J2534, no vehicle identification.
/// Claims are invented and checked with the strict research gate.
/// </summary>
public sealed class CemConfigurationResearchCoverageTests
{
    private static CemConfigurationMappingResearchClaim Claim(
        int index,
        CemConfigurationIndexedFormat format = CemConfigurationIndexedFormat.P2B9Fc,
        string part = "00000000",
        string revision = "SYNTHETIC-1",
        string meaning = "Synthetic claimed option A",
        string evidence = "https://example.invalid/source-a") =>
        new(format, index, part, revision, meaning, evidence);

    private static CemConfigurationResearchCatalogDocument Catalog(
        params CemConfigurationMappingResearchClaim[] rows) =>
        new(CemConfigurationResearchCatalogImport.Format, 1, true, false,
            false, Array.AsReadOnly(rows));

    private static CemConfigurationResearchCoverageAssessment Assess(
        CemConfigurationResearchCatalogDocument catalog,
        CemConfigurationIndexedFormat format = CemConfigurationIndexedFormat.P2B9Fc,
        string? part = "00000000",
        string? revision = "SYNTHETIC-1") =>
        CemConfigurationResearchCoverage.Assess(catalog, format, part, revision);

    [Fact]
    public void P2WholeFormatTotalsHaveNoInventedOEMMappings()
    {
        var result = Assess(Catalog(Claim(1)));
        Assert.Equal(CemConfigurationIndexedFormat.P2B9Fc, result.Format);
        Assert.Equal(140, result.TotalOrdinals);
        Assert.Equal(1, result.ExactUnverifiedClaimCount);
        Assert.Equal(139, result.NoIndexedResearchClaimCount);
        Assert.Equal(0, result.OtherSoftwareRevisionClaimCount);
        Assert.Equal(0, result.OtherHardwarePartClaimCount);
        Assert.Equal(0, result.PotentialCrossSourceTextDifferenceCount);
        Assert.Equal(100.0 / 140.0, result.ExactUserClaimScopePercent, 8);
        Assert.False(result.HasAuthenticatedPhysicalCem);
        Assert.False(result.HasVerifiedSoftwareRevision);
        Assert.False(result.HasVerifiedOptionMeanings);
        Assert.False(result.IsRestorableBackup);
        Assert.False(result.MayReadLiveConfiguration);
        Assert.False(result.MayWriteConfiguration);
        Assert.False(result.MayAcquirePin);
        Assert.False(result.Ordinals[0].HasVerifiedOptionMeaning);
        Assert.False(result.Ordinals[0].MayWriteConfiguration);
    }

    [Fact]
    public void P3HasExactly252OneBasedOrdinals()
    {
        var result = Assess(Catalog(Claim(252,
            CemConfigurationIndexedFormat.P3F106)),
            CemConfigurationIndexedFormat.P3F106);
        Assert.Equal(252, result.TotalOrdinals);
        Assert.Equal(252, result.Ordinals[^1].Index1Based);
        Assert.Equal(CemConfigurationResearchCoverageKind.ExactUnverifiedClaim,
            result.Ordinals[^1].Kind);
        Assert.Equal(251, result.NoIndexedResearchClaimCount);
    }

    [Fact]
    public void ExactOtherSoftwareOtherPartAndMissingAreMutuallyExclusive()
    {
        var result = Assess(Catalog(
            Claim(1),
            Claim(2, revision: "SYNTHETIC-2"),
            Claim(3, part: "00000001"),
            Claim(4, revision: "SYNTHETIC-2"),
            Claim(4, part: "00000001")));
        Assert.Equal(1, result.ExactUnverifiedClaimCount);
        Assert.Equal(2, result.OtherSoftwareRevisionClaimCount);
        Assert.Equal(1, result.OtherHardwarePartClaimCount);
        Assert.Equal(136, result.NoIndexedResearchClaimCount);
        Assert.Equal(140,
            result.ExactUnverifiedClaimCount + result.OtherSoftwareRevisionClaimCount +
            result.OtherHardwarePartClaimCount + result.NoIndexedResearchClaimCount);
        Assert.Equal(CemConfigurationResearchCoverageKind.OnlyOtherClaimedSoftwareRevision,
            result.Ordinals[3].Kind);
    }

    [Fact]
    public void ClaimsOfTheWrongGenerationNeverInflateCoverage()
    {
        var result = Assess(Catalog(Claim(1,
            CemConfigurationIndexedFormat.P3F106)));
        Assert.Equal(0, result.ExactUnverifiedClaimCount);
        Assert.Equal(140, result.NoIndexedResearchClaimCount);
    }

    [Fact]
    public void MultipleRevisionsForOneIndexCountOnlyOneOrdinal()
    {
        var result = Assess(Catalog(
            Claim(1, revision: "SYNTHETIC-1"),
            Claim(1, revision: "SYNTHETIC-2"),
            Claim(1, revision: "SYNTHETIC-3")));
        Assert.Equal(1, result.ExactUnverifiedClaimCount);
        Assert.Equal(139, result.NoIndexedResearchClaimCount);
    }

    [Fact]
    public void DifferentSourceAndMeaningAcrossRevisionsAreFlaggedAsPotentialOnly()
    {
        var result = Assess(Catalog(
            Claim(1, revision: "SYNTHETIC-1",
                meaning: "Synthetic claimed option A",
                evidence: "https://example.invalid/first"),
            Claim(1, revision: "SYNTHETIC-2",
                meaning: "Synthetic claimed option B",
                evidence: "https://example.invalid/second"),
            Claim(4, revision: "SYNTHETIC-2",
                meaning: "Synthetic claimed option A",
                evidence: "https://example.invalid/first"),
            Claim(4, revision: "SYNTHETIC-3",
                meaning: "Synthetic claimed option B",
                evidence: "https://example.invalid/second")));
        Assert.Equal(new[] { 1, 4 }, result.PotentialCrossSourceTextDifferenceOrdinals);
        Assert.Equal(2, result.PotentialCrossSourceTextDifferenceCount);
        Assert.False(result.HasVerifiedOptionMeanings);
        Assert.Contains("NOT proven contradictions", result.CreateRedactedReport(true));
    }

    [Fact]
    public void SameSourceOrSameMeaningDoesNotCountAsCrossSourceTextDifference()
    {
        var result = Assess(Catalog(
            Claim(1, revision: "SYNTHETIC-1",
                meaning: "Synthetic claimed option A",
                evidence: "https://example.invalid/same"),
            Claim(1, revision: "SYNTHETIC-2",
                meaning: "Synthetic claimed option B",
                evidence: "https://example.invalid/same"),
            Claim(2, revision: "SYNTHETIC-1",
                meaning: "Synthetic claimed option A",
                evidence: "https://example.invalid/first"),
            Claim(2, revision: "SYNTHETIC-2",
                meaning: "Synthetic claimed option A",
                evidence: "https://example.invalid/second")));
        Assert.Empty(result.PotentialCrossSourceTextDifferenceOrdinals);
    }

    [Fact]
    public void OtherHardwarePartDoesNotCreateFalseDisagreement()
    {
        var result = Assess(Catalog(
            Claim(1, part: "00000000", revision: "SYNTHETIC-1",
                meaning: "Synthetic claimed option A",
                evidence: "https://example.invalid/first"),
            Claim(1, part: "00000001", revision: "SYNTHETIC-1",
                meaning: "Synthetic claimed option B",
                evidence: "https://example.invalid/second")));
        Assert.Empty(result.PotentialCrossSourceTextDifferenceOrdinals);
    }

    [Fact]
    public void DuplicateExactScopeAndUnsafeCatalogAreRejected()
    {
        var duplicated = Catalog(
            Claim(1, evidence: "https://example.invalid/first"),
            Claim(1, evidence: "https://example.invalid/second",
                meaning: "Synthetic claimed option B"));
        Assert.Throws<InvalidDataException>(() => Assess(duplicated));
        Assert.Throws<InvalidDataException>(() =>
            Assess(Catalog(Claim(1)) with { MayAccessEcu = true }));
        Assert.Throws<InvalidDataException>(() =>
            Assess(Catalog(Claim(1)) with { IsUnverifiedResearch = false }));
        Assert.Throws<InvalidDataException>(() =>
            Assess(Catalog(Claim(1) with { ClaimedMeaning = "bad\ntext" })));
    }

    [Fact]
    public void InvalidClaimedScopeOrFormatFailsClosed()
    {
        var good = Catalog(Claim(1));
        Assert.Throws<InvalidDataException>(() => Assess(good, part: null));
        Assert.Throws<InvalidDataException>(() => Assess(good, revision: "bad revision\n"));
        Assert.Throws<InvalidDataException>(() => Assess(good,
            format: (CemConfigurationIndexedFormat)19));
    }

    [Fact]
    public void ResultCollectionsAreDetachedAndImmutable()
    {
        var claimArray = new[] { Claim(1) };
        var catalog = Catalog(claimArray);
        var result = Assess(catalog);
        var rows = Assert.IsAssignableFrom<IList<CemConfigurationResearchCoverageRow>>(
            result.Ordinals);
        Assert.Throws<NotSupportedException>(() =>
            rows[0] = new CemConfigurationResearchCoverageRow(1,
                CemConfigurationResearchCoverageKind.NoIndexedResearchClaim));
        var flagged = Assess(Catalog(
            Claim(1, revision: "SYNTHETIC-1",
                meaning: "Synthetic claimed option A",
                evidence: "https://example.invalid/first"),
            Claim(1, revision: "SYNTHETIC-2",
                meaning: "Synthetic claimed option B",
                evidence: "https://example.invalid/second")));
        var flags = Assert.IsAssignableFrom<IList<int>>(
            flagged.PotentialCrossSourceTextDifferenceOrdinals);
        Assert.Throws<NotSupportedException>(() => flags[0] = 3);
    }

    [Fact]
    public void RuEnReportIsRedactedAndOnlyDescribesUserClaimScope()
    {
        var result = Assess(Catalog(
            Claim(1, revision: "SYNTHETIC-1",
                meaning: "Synthetic claimed option A",
                evidence: "https://example.invalid/first"),
            Claim(1, revision: "SYNTHETIC-2",
                meaning: "Synthetic claimed option B",
                evidence: "https://example.invalid/second")));
        var en = result.CreateRedactedReport(true);
        var ru = result.CreateRedactedReport(false);
        Assert.Contains("Exact user-claim scope coverage: 0.7%", en);
        Assert.Contains("Possible text differences across sources/revisions: 1", en);
        Assert.Contains("Potential text-difference one-based ordinals: 1", en);
        Assert.Contains("Покрытие заявленными совпадениями: 0.7%", ru);
        Assert.Contains("Возможные текстовые расхождения источников/ревизий: 1", ru);
        foreach (var report in new[] { en, ru })
        {
            Assert.DoesNotContain("00000000", report);
            Assert.DoesNotContain("SYNTHETIC-1", report);
            Assert.DoesNotContain("SYNTHETIC-2", report);
            Assert.DoesNotContain("https://example.invalid", report);
            Assert.DoesNotContain("Synthetic claimed option", report);
            Assert.DoesNotContain("0x34", report);
        }
    }

    [Fact]
    public void ServiceHasNoHardwareOrFileWriteCapabilities()
    {
        var code = Source("src/Dvinholm.App/CemConfigurationResearchCoverage.cs");
        Assert.Contains("CemConfigurationMappingResearch.Validate(claims)", code);
        Assert.Contains("ValidPartNumber(claimedHardwarePartNumber)", code);
        Assert.Contains("PotentialCrossSourceTextDifferenceOrdinals", code);
        foreach (var forbidden in new[]
        {
            "PassThru", "J2534Session", "UdsClient", "SendAsync(",
            "ReadCemPin(", "SecurityAccess(", "WriteConfiguration(",
            "File.WriteAllBytes(", "FileMode.Create", "FileAccess.Write"
        })
            Assert.DoesNotContain(forbidden, code);
        Assert.Empty(CemConfigurationMappingResearch.ProductionMappings);
    }

    private static string Source(string relative)
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
        {
            var file = Path.Combine(dir.FullName,
                relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(file)) return File.ReadAllText(file);
        }
        throw new FileNotFoundException(relative);
    }
}
