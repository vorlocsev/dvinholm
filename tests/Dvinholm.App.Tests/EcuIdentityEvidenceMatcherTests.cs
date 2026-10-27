namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline synthetic-only compatibility checks. These fixtures do not
/// identify actual P2/CEM variants or authorize ECU writes or PIN access.
/// </summary>
public sealed class EcuIdentityEvidenceMatcherTests
{
    [Fact]
    public void EmptyCatalogKeepsObservedEcuUnknown()
    {
        var assessment = EcuIdentityEvidenceMatcher.Assess(Snapshot(), []);
        Assert.Equal(EcuIdentityMatchKind.Unknown, assessment.Kind);
        Assert.Null(assessment.VariantId);
    }

    [Fact]
    public void ExactSourcedTwoDidFingerprintMayBeClassifiedReadOnly()
    {
        var assessment = EcuIdentityEvidenceMatcher.Assess(Snapshot(), [Rule()]);

        Assert.Equal(EcuIdentityMatchKind.VerifiedReadOnlyIdentity, assessment.Kind);
        Assert.Equal("synthetic.p2.cem", assessment.VariantId);
        Assert.Contains("not authorized", assessment.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnverifiedRuleCannotClassifyEcuEvenIfBytesMatch()
    {
        var assessment = EcuIdentityEvidenceMatcher.Assess(Snapshot(), [Rule() with { IsVerified = false }]);
        Assert.Equal(EcuIdentityMatchKind.Unknown, assessment.Kind);
    }

    [Fact]
    public void ChangedSoftwareIdentifierMustNotMatch()
    {
        var assessment = EcuIdentityEvidenceMatcher.Assess(
            Snapshot(),
            [Rule() with
            {
                RequiredIdentifiers =
                [
                    new EcuDidReadback(0xF187, "004142"),
                    new EcuDidReadback(0xF189, "BADA55")
                ]
            }]);

        Assert.Equal(EcuIdentityMatchKind.Unknown, assessment.Kind);
    }

    [Fact]
    public void MissingOrCorruptedObservedIdentifierCannotMatch()
    {
        var missing = Snapshot() with { Values = [new EcuDidReadback(0xF187, "004142")] };
        Assert.Equal(EcuIdentityMatchKind.Unknown,
            EcuIdentityEvidenceMatcher.Assess(missing, [Rule()]).Kind);

        var repeated = Snapshot() with
        {
            Values =
            [
                new EcuDidReadback(0xF187, "004142"),
                new EcuDidReadback(0xF187, "004142"),
                new EcuDidReadback(0xF189, "010203")
            ]
        };
        Assert.Equal(EcuIdentityMatchKind.Unknown,
            EcuIdentityEvidenceMatcher.Assess(repeated, [Rule()]).Kind);
    }

    [Fact]
    public void CanAddressOrFramingMismatchCannotMatch()
    {
        Assert.Equal(EcuIdentityMatchKind.Unknown,
            EcuIdentityEvidenceMatcher.Assess(
                Snapshot(), [Rule() with { RequestId = 0x7E1 }]).Kind);
        Assert.Equal(EcuIdentityMatchKind.Unknown,
            EcuIdentityEvidenceMatcher.Assess(
                Snapshot(), [Rule() with { ResponseId = 0x7E9 }]).Kind);
        Assert.Equal(EcuIdentityMatchKind.Unknown,
            EcuIdentityEvidenceMatcher.Assess(
                Snapshot(), [Rule() with { IsExtended = true }]).Kind);
    }

    [Fact]
    public void AmbiguousVerifiedFingerprintsAreNotSilentlySelected()
    {
        var assessment = EcuIdentityEvidenceMatcher.Assess(
            Snapshot(),
            [Rule(), Rule() with { VariantId = "synthetic.other" }]);

        Assert.Equal(EcuIdentityMatchKind.Ambiguous, assessment.Kind);
        Assert.Null(assessment.VariantId);
    }

    [Fact]
    public void VinOnlyMalformedDuplicateAndUnattributedRulesFailClosed()
    {
        var invalid = new[]
        {
            Rule() with { EvidenceSource = "" },
            Rule() with { RequiredIdentifiers = [new EcuDidReadback(0xF190, "595631"), new EcuDidReadback(0xF189, "010203")] },
            Rule() with { RequiredIdentifiers = [new EcuDidReadback(0xF187, "004142"), new EcuDidReadback(0xF187, "010203")] },
            Rule() with { RequiredIdentifiers = [new EcuDidReadback(0xF187, "00414"), new EcuDidReadback(0xF189, "010203")] },
            Rule() with { RequiredIdentifiers = [new EcuDidReadback(0xF187, "0041ZZ"), new EcuDidReadback(0xF189, "010203")] },
            Rule() with { RequiredIdentifiers = [new EcuDidReadback(0xF187, "004142")] }
        };

        foreach (var rule in invalid)
            Assert.Equal(EcuIdentityMatchKind.Unknown,
                EcuIdentityEvidenceMatcher.Assess(Snapshot(), [rule]).Kind);
    }

    private static EcuConfigurationReadSnapshot Snapshot() =>
        new("synthetic.ecm", "Synthetic ECU", 0x7E0, 0x7E8, false, false,
            DateTimeOffset.UtcNow,
            [
                new EcuDidReadback(0xF187, "004142"),
                new EcuDidReadback(0xF189, "010203"),
                new EcuDidReadback(0xF190, "595631")
            ]);

    private static EcuIdentityEvidenceRule Rule() =>
        new("synthetic.p2.cem", "P2", "CEM", 0x7E0, 0x7E8, false, true,
            "Synthetic offline contract fixture, not a real ECU identifier",
            [
                new EcuDidReadback(0xF187, "004142"),
                new EcuDidReadback(0xF189, "010203")
            ]);
}
