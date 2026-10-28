using System.Text;
using System.Text.Json;

namespace Dvinholm.App.Tests;

/// <summary>Eight synthetic-only checks, no physical CEM or ECU requests.</summary>
public sealed class VolvoP2CemRevisionSourceComparisonTests
{
    [Fact]
    public void ExactTripleMatchIsStillAnUnauthenticatedClaim()
    {
        var result = VolvoP2CemRevisionSourceComparison.Compare(Card(), Claim());
        Assert.Equal(VolvoP2CemRevisionSourceMatchKind.SameUnverifiedPartNumberClaims,
            result.Kind);
        Assert.True(result.HardwareClaimMatches);
        Assert.True(result.SoftwareClaimMatches);
        Assert.True(result.DiagnosticSoftwareClaimMatches);
        Assert.False(result.HasAuthenticatedCemIdentity);
        Assert.False(result.HasVerifiedDiagnosticAddressing);
        Assert.False(result.HasVerifiedConfigurationMapping);
        Assert.False(result.MayConnectOrReadCem);
        Assert.False(result.MayAcquirePin);
        Assert.False(result.MayWriteConfiguration);
    }

    [Fact]
    public void OutsideClaimedYearRangeNeverComparesPartNumbers()
    {
        var scoped = Claim() with { ModelYearFrom = 2008, ModelYearThrough = 2009 };
        var result = VolvoP2CemRevisionSourceComparison.Compare(Card(), scoped);
        Assert.Equal(VolvoP2CemRevisionSourceMatchKind.YearOutsideResearchScope,
            result.Kind);
        Assert.False(result.HardwareClaimMatches);
        Assert.False(result.SoftwareClaimMatches);
        Assert.False(result.DiagnosticSoftwareClaimMatches);
        var text = VolvoP2CemRevisionSourceComparison.BuildReadOnlyReport(Card(), scoped);
        Assert.Contains("Comparison withheld", text);
    }

    [Fact]
    public void HardwareSoftwareAndDiagnosticMismatchAreDistinguished()
    {
        foreach (var changed in new[]
        {
            Claim() with { ClaimedHardwarePartNumber = "HW-002" },
            Claim() with { ClaimedSoftwarePartNumber = "SW-002" },
            Claim() with { ClaimedDiagnosticSoftwarePartNumber = "DIAG-002" }
        })
        {
            var result = VolvoP2CemRevisionSourceComparison.Compare(Card(), changed);
            Assert.Equal(VolvoP2CemRevisionSourceMatchKind.ClaimedPartNumbersDiffer,
                result.Kind);
            Assert.Equal(2, (result.HardwareClaimMatches ? 1 : 0) +
                (result.SoftwareClaimMatches ? 1 : 0) +
                (result.DiagnosticSoftwareClaimMatches ? 1 : 0));
            Assert.False(result.MayWriteConfiguration);
        }
    }

    [Fact]
    public void StrictJsonRoundTripRejectsUnknownDuplicateOrWronglyTypedFields()
    {
        var valid = JsonSerializer.Serialize(Claim(),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var parsed = VolvoP2CemRevisionSourceComparison.InspectJson(valid);
        Assert.Equal("HW-001", parsed.ClaimedHardwarePartNumber);
        var invalid = new[]
        {
            valid.Replace("\"schemaVersion\":1",
                "\"schemaVersion\":1,\"schemaVersion\":1", StringComparison.Ordinal),
            valid.Replace("\"schemaVersion\":1",
                "\"schemaVersion\":1,\"secretPin\":\"123456\"", StringComparison.Ordinal),
            valid.Replace("\"modelYearFrom\":2007",
                "\"modelYearFrom\":\"2007\"", StringComparison.Ordinal),
            valid.Replace("\"isUnverifiedResearchClaim\":true",
                "\"isUnverifiedResearchClaim\":\"yes\"", StringComparison.Ordinal)
        };
        foreach (var json in invalid)
        {
            Assert.NotEqual(valid, json);
            Assert.Throws<InvalidDataException>(() =>
                VolvoP2CemRevisionSourceComparison.InspectJson(json));
        }
    }

    [Fact]
    public void DangerousPermissionsVinShapedPartAndContradictoryYearsAreRejected()
    {
        foreach (var claim in new[]
        {
            Claim() with { MayAccessEcu = true },
            Claim() with { IsRestorableBackup = true },
            Claim() with { IsUnverifiedResearchClaim = false },
            Claim() with { ClaimedHardwarePartNumber = "YV1CM59H271234567" },
            Claim() with { ModelYearFrom = 2014, ModelYearThrough = 2007 },
            Claim() with { SourceReference = "source\ninjection" },
            Claim() with { ClaimedSoftwarePartNumber = "SW-001 " }
        })
            Assert.Throws<InvalidDataException>(() =>
                VolvoP2CemRevisionSourceComparison.Compare(Card(), claim));
    }

    [Fact]
    public void ForgedManualIdentityFlagsOrInvalidPartNumbersFailClosed()
    {
        foreach (var card in new[]
        {
            Card() with { MayAccessEcu = true },
            Card() with { IsRestorableBackup = true },
            Card() with { IsUnverifiedManualClaim = false },
            Card() with { ClaimedSoftwarePartNumber = "SECRET PIN" }
        })
            Assert.Throws<InvalidDataException>(() =>
                VolvoP2CemRevisionSourceComparison.Compare(card, Claim()));
    }

    [Fact]
    public void ReportNeverExposesPartNumbersPathsOrSourceText()
    {
        var claim = Claim() with
        {
            SourceReference = "PRIVATE-source-reference-not-for-display"
        };
        var text = VolvoP2CemRevisionSourceComparison.BuildReadOnlyReport(Card(), claim);
        Assert.Contains("NOT AN ECU IDENTIFICATION", text);
        Assert.Contains("SAME", text);
        Assert.Contains("DISABLED", text);
        Assert.DoesNotContain(claim.SourceReference, text);
        Assert.DoesNotContain(Card().ClaimedHardwarePartNumber, text);
        Assert.DoesNotContain(Card().ClaimedSoftwarePartNumber, text);
        Assert.DoesNotContain(Card().ClaimedDiagnosticSoftwarePartNumber, text);
    }

    [Fact]
    public async Task FileInspectorRejectsInvalidUtf8BomOversizeAndCancellation()
    {
        var folder = Path.Combine(Path.GetTempPath(),
            "dvinholm-cem-claim-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "research.json");
        try
        {
            var json = JsonSerializer.Serialize(Claim(),
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            File.WriteAllText(path, json, new UTF8Encoding(false));
            var read = await VolvoP2CemRevisionSourceComparison.InspectFileAsync(path);
            Assert.Equal("SW-001", read.ClaimedSoftwarePartNumber);

            File.WriteAllBytes(path, [0xC3, 0x28]);
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                VolvoP2CemRevisionSourceComparison.InspectFileAsync(path));

            File.WriteAllBytes(path, [0xEF, 0xBB, 0xBF, 0x7B, 0x7D]);
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                VolvoP2CemRevisionSourceComparison.InspectFileAsync(path));

            using (var file = File.Open(path, FileMode.Create))
                file.SetLength(VolvoP2CemRevisionSourceComparison.MaximumFileBytes + 1);
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                VolvoP2CemRevisionSourceComparison.InspectFileAsync(path));

            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                VolvoP2CemRevisionSourceComparison.InspectFileAsync(
                    path, cancellation.Token));
            Assert.Single(Directory.EnumerateFiles(folder));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private static VolvoP2CemIdentityResearchDocument Card() =>
        new(VolvoP2CemIdentityResearchExport.Format, 1, true, false, false,
            2007, "HW-001", "SW-001", "DIAG-001", true);

    private static VolvoP2CemRevisionSourceClaim Claim() =>
        new(VolvoP2CemRevisionSourceComparison.Format, 1, true, false, false,
            2007, 2007, "HW-001", "SW-001", "DIAG-001",
            "Synthetic source for offline research only");
}
