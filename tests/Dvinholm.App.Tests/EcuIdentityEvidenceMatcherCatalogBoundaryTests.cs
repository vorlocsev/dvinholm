namespace Dvinholm.App.Tests;

/// <summary>
/// Eight synthetic preflight tests. A matching DID fingerprint is read-only
/// metadata, never a real XC90 CEM signature, PIN or write authorization.
/// </summary>
public sealed class EcuIdentityEvidenceMatcherCatalogBoundaryTests
{
    [Fact]
    public void ValidSyntheticSingleRuleStillMatchesReadOnly()
    {
        var result = Match(Snapshot(), [Rule()]);
        Assert.Equal(EcuIdentityMatchKind.VerifiedReadOnlyIdentity, result.Kind);
        Assert.Equal("synthetic.cem.a", result.VariantId);
        Assert.Contains("not authorized", result.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EmptyCatalogLeavesIdentityUnknown()
    {
        var result = Match(Snapshot(), []);
        Assert.Equal(EcuIdentityMatchKind.Unknown, result.Kind);
        Assert.Null(result.VariantId);
    }

    [Fact]
    public void MaximumCatalogSizeIsAcceptedWithoutGrantingPhysicalEvidence()
    {
        var catalog = Enumerable.Range(0, EcuIdentityEvidenceMatcher.MaximumRules)
            .Select(index => Rule() with
            {
                VariantId = "synthetic.variant." + index,
                RequestId = index == 0 ? 0x7E0u : 0x7E1u
            }).ToArray();
        var result = Match(Snapshot(), catalog);
        Assert.Equal(EcuIdentityMatchKind.VerifiedReadOnlyIdentity, result.Kind);
        Assert.Equal("synthetic.variant.0", result.VariantId);
    }

    [Fact]
    public void OverLimitCatalogFailsClosedEvenIfFirstRuleMatches()
    {
        var catalog = Enumerable.Repeat(Rule(), EcuIdentityEvidenceMatcher.MaximumRules + 1);
        var result = Match(Snapshot(), catalog);
        Assert.Equal(EcuIdentityMatchKind.Unknown, result.Kind);
        Assert.Null(result.VariantId);
    }

    [Fact]
    public void MalformedNonmatchingRuleInvalidatesWholeCatalog()
    {
        var invalid = Rule() with
        {
            VariantId = "synthetic.invalid",
            RequestId = 0x7E1u,
            RequiredIdentifiers = [new EcuDidReadback(0xF190, "595631"),
                                   new EcuDidReadback(0xF189, "010203")]
        };
        var result = Match(Snapshot(), [Rule(), invalid]);
        Assert.Equal(EcuIdentityMatchKind.Unknown, result.Kind);
    }

    [Fact]
    public void InvalidCanAddressingOrUnboundedSourceInvalidatesCatalog()
    {
        var broken = new[]
        {
            Rule() with { RequestId = 0x800u },
            Rule() with { ResponseId = 0x7E0u },
            Rule() with { EvidenceSource = new string('X', 513) },
            Rule() with { Platform = "P2\nFAKE" }
        };
        foreach (var rule in broken)
            Assert.Equal(EcuIdentityMatchKind.Unknown,
                Match(Snapshot(), [Rule(), rule]).Kind);
    }

    [Fact]
    public void InvalidSnapshotAddressOrExcessDidsNeverMatch()
    {
        var snapshot = Snapshot();
        var tooMany = Enumerable.Range(0, EcuConfigurationReadback.MaximumDataIdentifiers + 1)
            .Select(i => new EcuDidReadback((ushort)(0xA000 + i), "AA")).ToArray();
        foreach (var invalid in new[]
        {
            snapshot with { RequestId = 0x800u },
            snapshot with { ResponseId = snapshot.RequestId },
            snapshot with { Values = tooMany }
        })
            Assert.Equal(EcuIdentityMatchKind.Unknown, Match(invalid, [Rule()]).Kind);
    }

    [Fact]
    public void AmbiguousValidFingerprintsRemainUnresolved()
    {
        var result = Match(Snapshot(),
            [Rule(), Rule() with { VariantId = "synthetic.cem.b" }]);
        Assert.Equal(EcuIdentityMatchKind.Ambiguous, result.Kind);
        Assert.Null(result.VariantId);
    }

    private static EcuIdentityAssessment Match(
        EcuConfigurationReadSnapshot snapshot, IEnumerable<EcuIdentityEvidenceRule> rules) =>
        EcuIdentityEvidenceMatcher.Assess(snapshot, rules);

    private static EcuConfigurationReadSnapshot Snapshot() =>
        new("synthetic.ecu", "Synthetic ECU", 0x7E0, 0x7E8, false, false,
            DateTimeOffset.UtcNow,
            [new EcuDidReadback(0xF187, "004142"),
             new EcuDidReadback(0xF189, "010203")]);

    private static EcuIdentityEvidenceRule Rule() =>
        new("synthetic.cem.a", "P2", "CEM", 0x7E0, 0x7E8, false, true,
            "Synthetic evidence only, not a real XC90 module",
            [new EcuDidReadback(0xF187, "004142"),
             new EcuDidReadback(0xF189, "010203")]);
}
