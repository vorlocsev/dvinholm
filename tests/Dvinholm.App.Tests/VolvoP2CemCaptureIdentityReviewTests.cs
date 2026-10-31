namespace Dvinholm.App.Tests;

/// <summary>
/// Eight deterministic offline gates for post-capture CEM identity status.
/// All IDs and DID bytes are synthetic and are NOT XC90 protocol mappings.
/// No physical/virtual adapter, CAN transmission or PIN service is invoked.
/// </summary>
public sealed class VolvoP2CemCaptureIdentityReviewTests
{
    [Fact]
    public void ProductionReadbackRemainsUnknownWhenNoVerifiedSignaturesAreBundled()
    {
        Assert.Empty(VolvoP2CemSupportMatrix.ProductionEntries);
        var result = VolvoP2CemCaptureIdentityReview.Assess(Snapshot());
        Assert.Equal(VolvoP2CemCaptureIdentityKind.NoProductionSignatures, result.Kind);
        Assert.Equal(2, result.ObservedDidCount);
        Assert.Contains("UNKNOWN", result.ActivityMessage);
        Assert.Contains("no verified production CEM signatures", result.ActivityMessage);
        AssertBlocked(result);
    }

    [Fact]
    public void ProfileVerificationCheckboxAndNameCannotAuthenticatePhysicalCem()
    {
        var snapshot = Snapshot() with
        {
            ProfileMarkedVerified = true,
            ProfileName = "CEM VERIFIED VEHICLE SECRET"
        };
        var result = VolvoP2CemCaptureIdentityReview.Assess(snapshot);
        Assert.Equal(VolvoP2CemCaptureIdentityKind.NoProductionSignatures, result.Kind);
        Assert.DoesNotContain("VEHICLE SECRET", result.ActivityMessage);
        AssertBlocked(result);
    }

    [Fact]
    public void InvalidCanAddressingOrNonUtcCaptureCannotBeEvaluated()
    {
        var original = Snapshot();
        var invalid = new[]
        {
            original with { ResponseId = original.RequestId },
            original with { RequestId = 0x800 },
            original with { IsExtended = true, ResponseId = 0x20000000 },
            original with { CapturedAtUtc = default },
            original with { CapturedAtUtc = new DateTimeOffset(2026, 10, 31, 4, 0, 0,
                TimeSpan.FromHours(2)) }
        };
        foreach (var candidate in invalid)
        {
            var result = VolvoP2CemCaptureIdentityReview.Assess(candidate);
            Assert.Equal(VolvoP2CemCaptureIdentityKind.InvalidCapture, result.Kind);
            Assert.Equal(0, result.ObservedDidCount);
            AssertBlocked(result);
        }
    }

    [Fact]
    public void DuplicateEmptyMalformedOrOversizedDidValuesFailClosed()
    {
        var original = Snapshot();
        var invalid = new[]
        {
            original with { Values = [] },
            original with { Values = [
                new EcuDidReadback(0xF187, "AA11"),
                new EcuDidReadback(0xF187, "BB22")] },
            original with { Values = [new EcuDidReadback(0xF187, "A")] },
            original with { Values = [new EcuDidReadback(0xF187, "ZZ")] },
            original with { Values = [new EcuDidReadback(0xF187,
                new string('A', EcuConfigurationReadback.MaximumDidValueBytes * 2 + 2))] }
        };
        foreach (var candidate in invalid)
        {
            var result = VolvoP2CemCaptureIdentityReview.Assess(candidate);
            Assert.Equal(VolvoP2CemCaptureIdentityKind.InvalidCapture, result.Kind);
            AssertBlocked(result);
        }
    }

    [Fact]
    public void EvenSyntheticEvidenceRequiresExplicitInScopeModelYear()
    {
        foreach (int? year in new int?[] { null, 2002, 2015 })
        {
            var result = VolvoP2CemCaptureIdentityReview.Assess(
                Snapshot(), year, [Entry()]);
            Assert.Equal(VolvoP2CemCaptureIdentityKind.ExactModelYearRequired, result.Kind);
            AssertBlocked(result);
        }
    }

    [Fact]
    public void MismatchedRevisionAndCanAddressRemainUnknown()
    {
        var original = Snapshot();
        foreach (var input in new[]
        {
            original with { ResponseId = 0x7E9 },
            original with { Values = [
                new EcuDidReadback(0xF187, "AA11"),
                new EcuDidReadback(0xF189, "CC33")] }
        })
        {
            var result = VolvoP2CemCaptureIdentityReview.Assess(input, 2007, [Entry()]);
            Assert.Equal(VolvoP2CemCaptureIdentityKind.NoMatchingReadOnlySignature,
                result.Kind);
            AssertBlocked(result);
        }
    }

    [Fact]
    public void SyntheticSignatureMatchIsResearchOnlyAndDoesNotRevealDidsOrSecrets()
    {
        var source = Snapshot() with
        {
            ProfileId = "sensitive-profile-id",
            ProfileName = "private name"
        };
        var result = VolvoP2CemCaptureIdentityReview.Assess(source, 2007, [Entry()]);
        Assert.Equal(VolvoP2CemCaptureIdentityKind.ResearchSignatureMatched, result.Kind);
        Assert.Equal(2, result.ObservedDidCount);
        Assert.Contains("physical CEM identity", result.ActivityMessage);
        Assert.DoesNotContain("AA11", result.ActivityMessage);
        Assert.DoesNotContain("BB22", result.ActivityMessage);
        Assert.DoesNotContain("sensitive-profile-id", result.ActivityMessage);
        Assert.DoesNotContain("private name", result.ActivityMessage);
        Assert.DoesNotContain("synthetic.p2.cem", result.ActivityMessage);
        AssertBlocked(result);
    }

    [Fact]
    public void ConflictingSignaturesStayUnknownAndExistingCaptureUiShowsReview()
    {
        var first = Entry();
        var other = first with
        {
            Identity = first.Identity with { VariantId = "synthetic.p2.cem.revB" }
        };
        var result = VolvoP2CemCaptureIdentityReview.Assess(
            Snapshot(), 2007, [first, other]);
        Assert.Equal(VolvoP2CemCaptureIdentityKind.ConflictingReadOnlySignatures,
            result.Kind);
        Assert.DoesNotContain("revB", result.ActivityMessage);
        AssertBlocked(result);

        string ui = ReadMainWindowSource();
        Assert.Contains("VolvoP2CemCaptureIdentityReview.Assess(snapshot)", ui);
        Assert.Contains("cemIdentity.ActivityMessage", ui);
        Assert.Contains("userApprovedSensitiveExport: true", ui);
        Assert.Contains("Read and choose Save As", ui);
    }

    private static void AssertBlocked(VolvoP2CemCaptureIdentityAssessment result)
    {
        Assert.False(result.HasAuthenticatedPhysicalCem);
        Assert.False(result.MayReadCemConfiguration);
        Assert.False(result.MayAcquirePin);
        Assert.False(result.MayWriteConfiguration);
    }

    private static EcuConfigurationReadSnapshot Snapshot() => new(
        "synthetic.cem", "Synthetic CEM profile", 0x7E0, 0x7E8,
        false, false, new DateTimeOffset(2026, 10, 31, 0, 0, 0, TimeSpan.Zero),
        [new EcuDidReadback(0xF187, "AA11"),
         new EcuDidReadback(0xF189, "BB22")]);

    private static VolvoP2CemSupportEntry Entry() => new(
        2005, 2009,
        new EcuIdentityEvidenceRule(
            "synthetic.p2.cem.revA", "P2", "CEM",
            0x7E0, 0x7E8, false, true,
            "Only fabricated test data, not an actual CEM mapping",
            [new EcuDidReadback(0xF187, "AA11"),
             new EcuDidReadback(0xF189, "BB22")]),
        [0xF187, 0xF189], "Only fabricated DID mapping");

    private static string ReadMainWindowSource()
    {
        for (DirectoryInfo? path = new(AppContext.BaseDirectory);
             path is not null; path = path.Parent)
        {
            var file = Path.Combine(path.FullName, "src", "Dvinholm.App",
                "MainWindow.xaml.cs");
            if (File.Exists(file)) return File.ReadAllText(file);
        }
        throw new FileNotFoundException("MainWindow.xaml.cs");
    }
}
