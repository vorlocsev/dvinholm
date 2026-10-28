using System.Text.Json;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight deterministic tests for the bounded, payload-free, opt-in JSON data.
/// Tests do not open a physical J2534 adapter or invoke CEM diagnostics.
/// </summary>
public sealed class CanMonitorRxMetadataJsonTests
{
    [Fact]
    public void EmptyHistoryHasExplicitUnverifiedSchemaAndUnknownDropCount()
    {
        using var document = Parse([], 0, null, false);
        var root = document.RootElement;
        Assert.Equal(CanMonitorRxMetadataJson.Format, root.GetProperty("format").GetString());
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("buffered-rx-unverified", root.GetProperty("source").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("subscriberQueueDropsSinceClear").ValueKind);
        Assert.Empty(root.GetProperty("canIdGroups").EnumerateArray());
        Assert.False(root.GetProperty("containsPayload").GetBoolean());
        Assert.False(root.GetProperty("authorizesDiagnosticsOrConfiguration").GetBoolean());
    }

    [Fact]
    public void DlcHistogramAggregatesAndIsSorted()
    {
        using var document = Parse([
            Row("7E8", "11-bit", 8), Row("7E8", "11-bit", 2),
            Row("7E8", "11-bit", 8), Row("7E0", "11-bit", 0)
        ], 4, 0, false);
        var groups = document.RootElement.GetProperty("canIdGroups");
        Assert.Equal("7E0", groups[0].GetProperty("id").GetString());
        Assert.Equal("7E8", groups[1].GetProperty("id").GetString());
        Assert.Equal(3, groups[1].GetProperty("frameCount").GetInt32());
        var hist = groups[1].GetProperty("dlcCounts");
        Assert.Equal(2u, hist[0].GetProperty("dlc").GetUInt32());
        Assert.Equal(1, hist[0].GetProperty("count").GetInt32());
        Assert.Equal(8u, hist[1].GetProperty("dlc").GetUInt32());
        Assert.Equal(2, hist[1].GetProperty("count").GetInt32());
    }

    [Fact]
    public void StandardAndExtendedIdsRemainDistinct()
    {
        using var document = Parse([
            Row("7E8", "11-bit", 3), Row("000007E8", "29-bit", 3)
        ], 2, 1, false);
        var groups = document.RootElement.GetProperty("canIdGroups");
        Assert.Equal(2, document.RootElement.GetProperty("distinctCanIdFormatGroups").GetInt32());
        Assert.Equal("11-bit", groups[0].GetProperty("format").GetString());
        Assert.Equal("29-bit", groups[1].GetProperty("format").GetString());
    }

    [Fact]
    public void SyntheticLabelAndDisplayBoundArePreserved()
    {
        var rows = Enumerable.Range(0, 65)
            .Select(id => Row(id.ToString("X3"), "11-bit", 1)).ToArray();
        using var document = Parse(rows, 65, null, true);
        var root = document.RootElement;
        Assert.Equal("synthetic-demo", root.GetProperty("source").GetString());
        Assert.Equal(65, root.GetProperty("distinctCanIdFormatGroups").GetInt32());
        Assert.Equal(1, root.GetProperty("omittedGroups").GetInt32());
        Assert.Equal(64, root.GetProperty("canIdGroups").GetArrayLength());
        Assert.Equal("03F", root.GetProperty("canIdGroups")[63].GetProperty("id").GetString());
    }

    [Fact]
    public void PayloadTimestampsSecretsAndVinAreNeverSerialized()
    {
        const string sensitive = "YV1CM59H271234567 PIN 54321";
        var json = CanMonitorRxMetadataJson.Build([
            new CanMonitorRow(987654321, "7E8", "11-bit", 8, sensitive)
        ], 1, null, false);
        Assert.DoesNotContain(sensitive, json);
        Assert.DoesNotContain("987654321", json);
        Assert.DoesNotContain("Timestamp", json, StringComparison.OrdinalIgnoreCase);
        using var document = JsonDocument.Parse(json);
        Assert.False(document.RootElement.GetProperty("containsTimestamps").GetBoolean());
        Assert.False(document.RootElement.GetProperty("confirmsPhysicalCemIdentity").GetBoolean());
    }

    [Fact]
    public void InvalidMetadataAndUnboundedHistoryAreRejected()
    {
        Assert.Throws<InvalidDataException>(() => CanMonitorRxMetadataJson.Build(
            [Row("800", "11-bit", 2)], 1, null, false));
        Assert.Throws<InvalidDataException>(() => CanMonitorRxMetadataJson.Build(
            [Row("7E8", "11-bit", 9)], 1, null, false));
        Assert.Throws<InvalidDataException>(() => CanMonitorRxMetadataJson.Build(
            [Row("7E8", "11-bit", 1)], 0, 0, false));
        Assert.Throws<InvalidDataException>(() => CanMonitorRxMetadataJson.Build(
            [], 0, -1, false));
        Assert.Throws<InvalidDataException>(() => CanMonitorRxMetadataJson.Build(
            Enumerable.Repeat(Row("123", "11-bit", 2),
                CanMonitorHistory.DefaultCapacity + 1).ToArray(),
            CanMonitorHistory.DefaultCapacity + 1, 0, false));
    }

    [Fact]
    public void SoftwareFilterDoesNotRemoveMetadataAndEvictionsAreExplicit()
    {
        var history = new CanMonitorHistory(2);
        history.Append(Row("101", "11-bit", 1));
        history.Append(Row("7E8", "11-bit", 4));
        history.Append(Row("7E0", "11-bit", 8));
        Assert.Single(history.Filter("7E8"));
        using var document = Parse(history.Filter(""), history.TotalReceived, 5, false);
        var root = document.RootElement;
        Assert.Equal(3, root.GetProperty("totalMonitorRxRowsSinceClear").GetInt64());
        Assert.Equal(2, root.GetProperty("retainedRxRows").GetInt32());
        Assert.Equal(1, root.GetProperty("evictedRxRows").GetInt64());
        Assert.Equal(5, root.GetProperty("subscriberQueueDropsSinceClear").GetInt64());
        Assert.Equal(2, root.GetProperty("canIdGroups").GetArrayLength());
    }

    [Fact]
    public void VirtualJ2534RxDoesNotTransmitOrLeakRawPayload()
    {
        using var adapter = new VirtualJ2534Adapter(_ => Array.Empty<CanFrame>());
        using var bus = new CanBus(adapter);
        using var subscription = bus.Subscribe();
        bus.Start();
        adapter.InjectReceiveFrame(new CanFrame(
            0x7E8, false, false, [0x03, 0x62, 0xF1, 0x90], 1234, 0));
        Assert.True(subscription.TryRead(TimeSpan.FromSeconds(2),
            CancellationToken.None, out var received));
        var history = new CanMonitorHistory();
        history.Append(CanMonitorRow.FromFrame(received));
        var json = CanMonitorRxMetadataJson.Build(
            history.Filter(""), history.TotalReceived,
            subscription.DroppedFrameCount, false);
        using var document = JsonDocument.Parse(json);
        Assert.Equal("7E8",
            document.RootElement.GetProperty("canIdGroups")[0].GetProperty("id").GetString());
        Assert.DoesNotContain("03 62 F1 90", json);
        Assert.Equal(0, adapter.TransmittedFrames);
    }

    private static JsonDocument Parse(
        IReadOnlyList<CanMonitorRow> rows, long total, long? drops, bool synthetic) =>
        JsonDocument.Parse(CanMonitorRxMetadataJson.Build(rows, total, drops, synthetic));

    private static CanMonitorRow Row(string id, string type, uint dlc) =>
        new(1, id, type, dlc, "DE AD BE EF");
}
