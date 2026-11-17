namespace Dvinholm.App.Tests;

/// <summary>
/// Synthetic metadata-only checks, not physical vehicle acceptance.
/// </summary>
public sealed class CemConfigurationMappingResearchTests
{
    private static byte[] Body(int count)
    {
        var bytes = new byte[count + 2];
        bytes[0] = (byte)count;
        bytes[2] = 0xAB;
        bytes[^1] = 0x34;
        bytes[1] = (byte)((bytes[2] + bytes[^1]) & 0xFF);
        return bytes;
    }

    private static CemConfigurationIndexedSnapshot P2() =>
        CemConfigurationIndexedReview.InspectP2B9Fc(Body(140));

    private static CemConfigurationIndexedSnapshot P3() =>
        CemConfigurationIndexedReview.InspectP3F106(Body(252));

    private static CemConfigurationMappingResearchClaim Claim(
        CemConfigurationIndexedFormat format = CemConfigurationIndexedFormat.P2B9Fc,
        int index = 1,
        string part = "30682981",
        string revision = "SW-A") =>
        new(format, index, part, revision,
            "Unverified synthetic option", "Synthetic citation reference 01");

    private static CemConfigurationMappingResearchAssessment Review(
        CemConfigurationIndexedSnapshot snapshot,
        int index, IEnumerable<CemConfigurationMappingResearchClaim?>? claims,
        string? part = "30682981", string? revision = "SW-A") =>
        CemConfigurationMappingResearch.Assess(snapshot, index, part, revision, claims);

    [Fact]
    public void MissingClaimsDoNotIdentifyAnyOption()
    {
        Assert.Equal(CemConfigurationMappingResearchKind.MissingEvidence,
            Review(P2(), 1, null).Kind);
        Assert.Equal(CemConfigurationMappingResearchKind.MissingEvidence,
            Review(P2(), 1, []).Kind);
        Assert.Empty(CemConfigurationMappingResearch.ProductionMappings);
    }

    [Fact]
    public void ExactP2ResearchClaimOnlyReturnsUnverifiedRawValue()
    {
        var result = Review(P2(), 1, [Claim()]);
        Assert.Equal(CemConfigurationMappingResearchKind.UnverifiedResearchMatch, result.Kind);
        Assert.Equal("AB", result.UnverifiedRawValueHex);
        Assert.Equal(1, result.MatchingResearchRecords);
        Assert.False(result.HasAuthenticatedPhysicalCem);
        Assert.False(result.HasVerifiedParameterMapping);
        Assert.False(result.MayReadLiveConfiguration);
        Assert.False(result.MayPrepareOrWriteConfiguration);
        Assert.False(result.MayAcquirePin);
    }

    [Fact]
    public void P3Ordinal252RequiresCorrectGenerationAndExactScope()
    {
        var claim = Claim(CemConfigurationIndexedFormat.P3F106, 252);
        var match = Review(P3(), 252, [claim]);
        Assert.Equal(CemConfigurationMappingResearchKind.UnverifiedResearchMatch, match.Kind);
        Assert.Equal("34", match.UnverifiedRawValueHex);
        Assert.Equal(CemConfigurationMappingResearchKind.NoExactResearchMatch,
            Review(P2(), 140, [claim]).Kind);
        Assert.Equal(CemConfigurationMappingResearchKind.NoExactResearchMatch,
            Review(P3(), 1, [claim]).Kind);
    }

    [Fact]
    public void WrongHardwareOrSoftwareRevisionNeverMatches()
    {
        Assert.Equal(CemConfigurationMappingResearchKind.NoExactResearchMatch,
            Review(P2(), 1, [Claim()], part: "30682982").Kind);
        Assert.Equal(CemConfigurationMappingResearchKind.NoExactResearchMatch,
            Review(P2(), 1, [Claim()], revision: "SW-B").Kind);
        Assert.Equal(CemConfigurationMappingResearchKind.MissingClaimedRevision,
            Review(P2(), 1, [Claim()], revision: null).Kind);
    }

    [Fact]
    public void InvalidOrdinalsAndUnknownFormatsAreRejected()
    {
        Assert.Equal(CemConfigurationMappingResearchKind.InvalidSnapshotOrIndex,
            Review(P2(), 0, [Claim()]).Kind);
        Assert.Equal(CemConfigurationMappingResearchKind.InvalidSnapshotOrIndex,
            Review(P3(), 253, [Claim()]).Kind);
        var invalidP2 = Claim(index: 141);
        var result = Review(P2(), 1, [invalidP2]);
        Assert.Equal(CemConfigurationMappingResearchKind.InvalidEvidence, result.Kind);
        Assert.Contains(CemConfigurationMappingResearchProblem.IndexOutsideFormat, result.Problems);

        var invalidFormat = Claim(format: (CemConfigurationIndexedFormat)999);
        Assert.Contains(CemConfigurationMappingResearchProblem.InvalidFormat,
            Review(P2(), 1, [invalidFormat]).Problems);
    }

    [Fact]
    public void DuplicateAndConflictingResearchScopesFailClosed()
    {
        var duplicate = Review(P2(), 1, [Claim(), Claim()]);
        Assert.Equal(CemConfigurationMappingResearchKind.InvalidEvidence, duplicate.Kind);
        Assert.Contains(CemConfigurationMappingResearchProblem.RepeatedOrConflictingScope,
            duplicate.Problems);
        Assert.Null(duplicate.UnverifiedRawValueHex);
        var contradiction = Review(P2(), 1, [Claim(),
            Claim() with { ClaimedMeaning = "Different unverified claim" }]);
        Assert.Equal(CemConfigurationMappingResearchKind.InvalidEvidence, contradiction.Kind);
    }

    [Fact]
    public void WholeCatalogMustBeValidNotOnlySelectedIndex()
    {
        var result = Review(P2(), 1, [Claim(),
            Claim(index: 2) with { EvidenceReference = "bad" }]);
        Assert.Equal(CemConfigurationMappingResearchKind.InvalidEvidence, result.Kind);
        Assert.Contains(CemConfigurationMappingResearchProblem.InvalidEvidenceReference,
            result.Problems);
        Assert.Equal(0, result.MatchingResearchRecords);
    }

    [Fact]
    public void ClaimInputsAreBoundedAndValidated()
    {
        var tooMany = Enumerable.Range(0, CemConfigurationMappingResearch.MaximumRecords + 1)
            .Select(i => (CemConfigurationMappingResearchClaim?)Claim(index: i + 1));
        var result = Review(P2(), 1, tooMany);
        Assert.Equal(CemConfigurationMappingResearchKind.InvalidEvidence, result.Kind);
        Assert.Contains(CemConfigurationMappingResearchProblem.TooManyRecords, result.Problems);

        var malformed = Review(P2(), 1,
            [Claim() with { ClaimedMeaning = "Injected\ntext" }, null]);
        Assert.Contains(CemConfigurationMappingResearchProblem.InvalidMeaning, malformed.Problems);
        Assert.Contains(CemConfigurationMappingResearchProblem.NullRecord, malformed.Problems);
    }

    [Fact]
    public void UntrustedSnapshotCannotBecomeVerifiedEvidence()
    {
        var snapshot = new CemConfigurationIndexedSnapshot(
            CemConfigurationIndexedFormat.P2B9Fc, new string('F', 64),
            [new CemConfigurationIndexedValue(1, 0xFF)]);
        var result = Review(snapshot, 1, [Claim()]);
        Assert.Equal(CemConfigurationMappingResearchKind.InvalidSnapshotOrIndex, result.Kind);
        Assert.Null(result.UnverifiedRawValueHex);
        Assert.Throws<ArgumentNullException>(() =>
            CemConfigurationMappingResearch.Assess(null!, 1, null, null, null));
    }

    [Fact]
    public void ResearchClaimNeverCreatesExecutableConfigurationAuthority()
    {
        var result = Review(P2(), 1, [Claim()]);
        Assert.False(result.MayReadLiveConfiguration);
        Assert.False(result.MayPrepareOrWriteConfiguration);
        Assert.Empty(CemConfigurationMappingResearch.ProductionMappings);
        Assert.Empty(VolvoP2CemParameterEvidenceCatalog.ProductionRecords);
        var preflight = Source("src/Dvinholm.App/VolvoP2CemConfigurationReadPreflight.cs");
        Assert.Contains("internal bool MayReadLiveCemConfiguration => false;", preflight);
        var source = Source("src/Dvinholm.App/CemConfigurationMappingResearch.cs");
        foreach (var forbidden in new[] {
            "PassThru", "J2534", "SendAsync(", "Connect(", "UdsClient",
            "ReadCemPin(", "SecurityAccess(", "WriteConfiguration(" })
            Assert.DoesNotContain(forbidden, source);
    }

    private static string Source(string relative)
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory);
             directory is not null; directory = directory.Parent)
        {
            var file = Path.Combine(directory.FullName,
                relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(file)) return File.ReadAllText(file);
        }
        throw new FileNotFoundException(relative);
    }
}
