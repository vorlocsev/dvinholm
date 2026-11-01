namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline synthetic regressions for the GENERIC matcher entrypoint.
/// A supplied DID fingerprint or evidence-source label never authenticates
/// a physical ECU, PIN entitlement, configuration mapping or service route.
/// </summary>
public sealed class EcuIdentityUtcProvenanceBoundaryTests
{
    [Fact]
    public void DefaultTimestampCannotYieldVerifiedReadOnlyIdentity()
    {
        var result = Match(Snapshot() with { CapturedAtUtc = default }, [Rule()]);
        Unknown(result);
    }

    [Fact]
    public void NonUtcOffsetCannotPassAsOriginalUtcCapture()
    {
        foreach (var offset in new[] { TimeSpan.FromHours(2), TimeSpan.FromHours(-5) })
        {
            var snapshot = Snapshot() with
            {
                CapturedAtUtc = new DateTimeOffset(2026, 10, 27, 12, 0, 0, offset)
            };
            Unknown(Match(snapshot, [Rule()]));
        }
    }

    [Fact]
    public void ExactUtcSyntheticMatchOnlyDescribesResearchPattern()
    {
        var result = Match(Snapshot(), [Rule()]);
        Assert.Equal(EcuIdentityMatchKind.VerifiedReadOnlyIdentity, result.Kind);
        Assert.Equal("synthetic.cem.revA", result.VariantId);
        Assert.Contains("supplied research fingerprint", result.Explanation,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("physical ECU identity are unverified", result.Explanation);
        Assert.Contains("write access is not authorized", result.Explanation);
    }

    [Fact]
    public void SelfReportedSourceCannotMakeMatchingBytesPhysicallyAuthenticated()
    {
        const string privateClaim = "PRIVATE-SYNTHETIC-SOURCE-NOT-REAL-EVIDENCE";
        var result = Match(Snapshot(), [
            Rule() with
            {
                IsVerified = true,
                EvidenceSource = privateClaim
            }
        ]);
        Assert.Equal(EcuIdentityMatchKind.VerifiedReadOnlyIdentity, result.Kind);
        Assert.Contains("Source authenticity", result.Explanation);
        Assert.DoesNotContain(privateClaim, result.Explanation);
        Assert.DoesNotContain("independently sourced", result.Explanation);
    }

    [Fact]
    public void UnsupportedOrContradictoryRuleNeverOverridesUtcCheck()
    {
        foreach (var rule in new[]
        {
            Rule() with { IsVerified = false },
            Rule() with { Platform = "P3\nUNTRUSTED" },
            Rule() with { RequestId = 0x800u },
            Rule() with { RequiredIdentifiers = [new EcuDidReadback(0xF190, "595631"),
                                               new EcuDidReadback(0xF189, "BB22")] }
        })
            Unknown(Match(Snapshot(), [rule]));
    }

    [Fact]
    public void InvalidObservedDidShapeCannotBeAcceptedAsCapturedIdentity()
    {
        var baseline = Snapshot();
        foreach (var invalid in new[]
        {
            baseline with { Values = [] },
            baseline with { Values = [new EcuDidReadback(0xF187, "AA11"),
                                       new EcuDidReadback(0xF187, "BB22")] },
            baseline with { Values = [new EcuDidReadback(0xF187, "AA11"),
                                       new EcuDidReadback(0xF189, "B")] },
            baseline with { Values = [new EcuDidReadback(0xF187, "AA11"),
                                       new EcuDidReadback(0xF189, "ZZ")] }
        })
            Unknown(Match(invalid, [Rule()]));
    }

    [Fact]
    public void AmbiguousResearchRulesCannotSelectPhysicalRevision()
    {
        var result = Match(Snapshot(), [
            Rule(),
            Rule() with { VariantId = "synthetic.cem.revB" }
        ]);
        Assert.Equal(EcuIdentityMatchKind.Ambiguous, result.Kind);
        Assert.Null(result.VariantId);
        Assert.Contains("physical ECU identity is unverified", result.Explanation);
        Assert.DoesNotContain("revB", result.Explanation);
    }

    [Fact]
    public void SnapshotProfileVerifiedFlagAndRequestAddressAreNotProvenance()
    {
        var marked = Snapshot() with
        {
            ProfileMarkedVerified = true,
            ProfileName = "PRIVATE-SYNTHETIC-OWNER-NAME"
        };
        var matched = Match(marked, [Rule()]);
        Assert.Equal(EcuIdentityMatchKind.VerifiedReadOnlyIdentity, matched.Kind);
        Assert.Contains("physical ECU identity are unverified", matched.Explanation);
        Assert.DoesNotContain("PRIVATE-SYNTHETIC", matched.Explanation);
        Unknown(Match(marked with { ResponseId = 0x7E9u }, [Rule()]));
    }

    private static EcuIdentityAssessment Match(
        EcuConfigurationReadSnapshot snapshot,
        IEnumerable<EcuIdentityEvidenceRule> catalog) =>
        EcuIdentityEvidenceMatcher.Assess(snapshot, catalog);

    private static void Unknown(EcuIdentityAssessment result)
    {
        Assert.Equal(EcuIdentityMatchKind.Unknown, result.Kind);
        Assert.Null(result.VariantId);
        Assert.Contains("unverified", result.Explanation);
    }

    private static EcuConfigurationReadSnapshot Snapshot() =>
        new("synthetic-profile", "Research-only ECU", 0x7E0, 0x7E8,
            false, false,
            new DateTimeOffset(2026, 10, 27, 0, 0, 0, TimeSpan.Zero),
            [new EcuDidReadback(0xF187, "AA11"),
             new EcuDidReadback(0xF189, "BB22")]);

    private static EcuIdentityEvidenceRule Rule() =>
        new("synthetic.cem.revA", "P2", "CEM", 0x7E0, 0x7E8,
            false, true, "Synthetic data, not real ECU",
            [new EcuDidReadback(0xF187, "AA11"),
             new EcuDidReadback(0xF189, "BB22")]);
}
