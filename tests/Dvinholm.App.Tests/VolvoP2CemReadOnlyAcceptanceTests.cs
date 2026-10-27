namespace Dvinholm.App.Tests;

/// <summary>
/// Eight cross-component offline acceptance gates for the 0.3.0 read-only
/// pipeline. Fake DID replies are injected through the existing test seam:
/// no adapter, vehicle, PIN, SecurityAccess, ECU programming or file writes.
/// </summary>
public sealed class VolvoP2CemReadOnlyAcceptanceTests
{
    [Fact]
    public void CapturedDidsSurviveJsonExportAndInspectionWithoutDecoding()
    {
        var observed = Capture("AA11", "BB22");
        var json = EcuConfigurationSnapshotExport.CreateJson(
            observed, userApprovedSensitiveExport: true);
        var historical = EcuConfigurationSnapshotInspection.InspectJson(json);

        Assert.Equal("synthetic.p2.cem", historical.ProfileId);
        Assert.Equal("7E0", historical.RequestCanId);
        Assert.Equal("7E8", historical.ResponseCanId);
        Assert.False(historical.ProfileMarkedVerified);
        Assert.False(historical.IsRestorableBackup);
        Assert.True(historical.ContainsPotentiallySensitiveData);
        Assert.Equal(new ushort[] { 0xF187, 0xF189 },
            historical.Values.Select(value => value.DataIdentifier));
        Assert.Equal(new[] { "AA11", "BB22" },
            historical.Values.Select(value => value.ValueHex));
    }

    [Fact]
    public void TwoCompleteReadbacksCanBeComparedThroughExportAndInspection()
    {
        var before = EcuConfigurationSnapshotInspection.InspectJson(
            EcuConfigurationSnapshotExport.CreateJson(Capture("AA11", "BB22"), true));
        var after = EcuConfigurationSnapshotInspection.InspectJson(
            EcuConfigurationSnapshotExport.CreateJson(Capture("AA12", "BB22"), true));

        var result = EcuHistoricalSnapshotComparer.Compare(before, after);

        Assert.Equal(2, result.ComparedCount);
        Assert.Equal(1, result.ChangedCount);
        Assert.Equal(new ushort[] { 0xF187 }, result.ChangedDataIdentifiers);
        Assert.DoesNotContain("AA11", result.Summary);
        Assert.DoesNotContain("AA12", result.Summary);
        Assert.DoesNotContain("BB22", result.Summary);
    }

    [Fact]
    public void ReadRequestsRemainSortedAndOnlyInjectedReadFunctionIsUsed()
    {
        var calls = new List<ushort>();
        var profile = Profile(0xF189, 0xF187);
        var captured = EcuConfigurationReadback.Capture(profile, (did, _) =>
        {
            calls.Add(did);
            return did switch
            {
                0xF187 => [0xF1, 0x87, 0xCA, 0xFE],
                0xF189 => [0xF1, 0x89, 0xBA, 0xBE],
                _ => throw new InvalidOperationException("Unexpected synthetic DID")
            };
        });

        var historical = EcuConfigurationSnapshotInspection.InspectJson(
            EcuConfigurationSnapshotExport.CreateJson(captured, true));

        Assert.Equal(new ushort[] { 0xF187, 0xF189 }, calls);
        Assert.Equal(new[] { "CAFE", "BABE" }, historical.Values.Select(x => x.ValueHex));
    }

    [Fact]
    public void SecondReadFailureNeverCreatesCompleteBaseline()
    {
        var calls = new List<ushort>();
        Assert.Throws<InvalidDataException>(() =>
            EcuConfigurationReadback.Capture(Profile(0xF187, 0xF189),
                (did, _) =>
                {
                    calls.Add(did);
                    return did == 0xF187
                        ? [0xF1, 0x87, 0xAA, 0x11]
                        : [0xF1, 0x90, 0xBB, 0x22]; // mismatched echo
                }));
        Assert.Equal(new ushort[] { 0xF187, 0xF189 }, calls);
    }

    [Fact]
    public void CancellationBetweenReadResponsesAbortsWithoutSnapshot()
    {
        using var cancellation = new CancellationTokenSource();
        var calls = 0;
        Assert.ThrowsAny<OperationCanceledException>(() =>
            EcuConfigurationReadback.Capture(Profile(0xF187, 0xF189),
                (did, _) =>
                {
                    calls++;
                    cancellation.Cancel();
                    return [(byte)(did >> 8), (byte)did, 0xAB];
                }, cancellation.Token));
        Assert.Equal(1, calls);
    }

    [Fact]
    public void TamperedExportCannotEnterHistoricalComparison()
    {
        var json = EcuConfigurationSnapshotExport.CreateJson(Capture("AA11", "BB22"), true);
        var changed = json.Replace("\"valueHex\":\"AA11\"",
            "\"valueHex\":\"AA12\"", StringComparison.Ordinal);
        Assert.NotEqual(json, changed);

        Assert.Throws<InvalidDataException>(() =>
            EcuConfigurationSnapshotInspection.InspectJson(changed));
    }

    [Fact]
    public void SyntheticVerifiedIdentityAndYearOnlyPermitReadOnlySupport()
    {
        var snapshot = Capture("AA11", "BB22");
        var evidence = Evidence();
        Assert.Empty(VolvoP2CemEvidenceCatalogValidator.Validate([evidence]));

        var result = VolvoP2CemSupportMatrix.Assess(2007, snapshot, [evidence]);

        Assert.Equal(VolvoP2CemSupportKind.DocumentedReadOnlyDidSet, result.Kind);
        Assert.Equal("synthetic.cem.firmware.a", result.VariantId);
        Assert.Equal(new ushort[] { 0xF187, 0xF189 }, result.ObservedDocumentedDids);
        Assert.False(result.PinRetrievalSupported);
        Assert.False(result.ConfigurationWriteSupported);
        Assert.Empty(VolvoP2CemSupportMatrix.ProductionEntries);
    }

    [Fact]
    public void CorruptCatalogOrChangedFirmwareFailsClosedForEntirePipeline()
    {
        var snapshot = Capture("AA11", "BB22");
        var evidence = Evidence();
        var conflicting = evidence with
        {
            Identity = evidence.Identity with { VariantId = "synthetic.other.firmware" },
            DocumentedReadableDids = [0xF190] // invalid cross-vehicle mapping
        };
        Assert.NotEmpty(VolvoP2CemEvidenceCatalogValidator.Validate([evidence, conflicting]));
        Assert.Equal(VolvoP2CemSupportKind.Unknown,
            VolvoP2CemSupportMatrix.Assess(2007, snapshot, [evidence, conflicting]).Kind);

        Assert.Equal(VolvoP2CemSupportKind.Unknown,
            VolvoP2CemSupportMatrix.Assess(2007, Capture("AA12", "BB22"), [evidence]).Kind);
        Assert.Equal(VolvoP2CemSupportKind.Unknown,
            VolvoP2CemSupportMatrix.Assess(2015, snapshot, [evidence]).Kind);
    }

    private static EcuProfile Profile(params ushort[] identifiers) =>
        new("synthetic.p2.cem", "Synthetic P2 CEM", 0x7E0, 0x7E8,
            false, identifiers, "Synthetic offline test only, no real Volvo address", false);

    private static EcuConfigurationReadSnapshot Capture(string firmware, string hardware) =>
        EcuConfigurationReadback.Capture(Profile(0xF189, 0xF187),
            (did, _) =>
            {
                var value = Convert.FromHexString(did == 0xF187 ? firmware : hardware);
                return [(byte)(did >> 8), (byte)did, .. value];
            });

    private static VolvoP2CemSupportEntry Evidence() =>
        new(2005, 2009,
            new EcuIdentityEvidenceRule(
                "synthetic.cem.firmware.a", "P2", "CEM",
                0x7E0, 0x7E8, false, true,
                "Independent synthetic offline contract fixture, NOT Volvo protocol evidence",
                [
                    new EcuDidReadback(0xF187, "AA11"),
                    new EcuDidReadback(0xF189, "BB22")
                ]),
            [0xF189, 0xF187],
            "Synthetic read-only DID contract fixture, NOT verified P2 mapping");
}
