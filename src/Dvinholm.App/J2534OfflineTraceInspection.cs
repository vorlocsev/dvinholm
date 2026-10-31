using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Dvinholm.App;

/// <summary>
/// Strict, bounded offline reader for the metadata-only schema-15 JSONL
/// emitted by Virtual Car's independently maintained J2534 tracing proxy.
/// This is NOT a protocol decoder, response recognizer or live adapter.
/// </summary>
internal static class J2534OfflineTraceInspection
{
    internal const int MaximumFileBytes = 512 * 1024;
    internal const int MaximumEvents = 5000;
    internal const int MaximumLineCharacters = 1024;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly HashSet<string> KnownFields = new(StringComparer.Ordinal)
    {
        "schema", "ms", "event", "channel", "status", "requested", "count",
        "class", "can_id", "size", "value", "core_version", "protocol_id",
        "ioctl_id", "vbatt_mv", "core_class", "core_decision"
    };

    internal static string BuildRedactedReport(string? jsonl)
    {
        if (string.IsNullOrWhiteSpace(jsonl) ||
            StrictUtf8.GetByteCount(jsonl) > MaximumFileBytes ||
            jsonl[0] == '\uFEFF' || jsonl.IndexOf('\0') >= 0)
            throw new InvalidDataException("Invalid or oversized J2534 trace.");

        var lines = jsonl.Split('\n');
        var count = lines.Length - (lines[^1].Length == 0 ? 1 : 0);
        if (count is < 1 or > MaximumEvents)
            throw new InvalidDataException("Invalid J2534 event count.");

        var activeChannels = new HashSet<uint>();
        var successfulTxWindow = new HashSet<uint>();
        uint? previousMs = null;
        int resets = 0, resetWindows = 0;
        int connects = 0, failedConnects = 0, reconnected = 0;
        int disconnects = 0, disconnectedFailed = 0;
        int writes = 0, successfulWrites = 0, failedWrites = 0;
        int reads = 0, successfulReadsWithData = 0, readsAfterTxWithData = 0;
        int readsBeforeTxWithData = 0, multiTx = 0, multiRx = 0, orphanCalls = 0;
        int a3 = 0, b9fb = 0, b9e0 = 0, b9f0 = 0, b9fc = 0;
        int e302 = 0, f9e0 = 0, f9f0 = 0, fragments = 0;
        int otherEvents = 0;
        int inconsistentWriteBatches = 0, inconsistentReadBatches = 0;
        int successfulDeviceCloses = 0, failedDeviceCloses = 0;
        int closeInvalidatedChannels = 0, closeInvalidatedTxWindows = 0;

        for (int i = 0; i < count; i++)
        {
            var line = lines[i].EndsWith('\r') ? lines[i][..^1] : lines[i];
            if (line.Length is < 2 or > MaximumLineCharacters)
                throw new InvalidDataException("Invalid J2534 trace line length.");

            try
            {
                using var doc = JsonDocument.Parse(line,
                    new JsonDocumentOptions { MaxDepth = 3 });
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                    throw new InvalidDataException("Invalid J2534 event object.");
                var keys = new HashSet<string>(StringComparer.Ordinal);
                foreach (var field in root.EnumerateObject())
                    if (!KnownFields.Contains(field.Name) || !keys.Add(field.Name))
                        throw new InvalidDataException(
                            "Unexpected or repeated J2534 trace field.");

                if (keys.Count < 11 ||
                    U32(root, "schema") != 15 ||
                    !root.TryGetProperty("can_id", out var canId) ||
                    canId.ValueKind != JsonValueKind.String ||
                    !IsCanonicalCanId(canId.GetString()))
                    throw new InvalidDataException("Unknown J2534 trace format.");

                uint ms = U32(root, "ms");
                uint channel = U32(root, "channel");
                uint status = U32(root, "status");
                uint requested = U32(root, "requested");
                uint messages = U32(root, "count");
                _ = U32(root, "size");
                _ = U32(root, "value");
                if (root.TryGetProperty("core_version", out _)) _ = U32(root, "core_version");
                if (root.TryGetProperty("protocol_id", out _)) _ = U32(root, "protocol_id");
                if (root.TryGetProperty("ioctl_id", out _)) _ = U32(root, "ioctl_id");
                if (root.TryGetProperty("vbatt_mv", out _)) _ = U32(root, "vbatt_mv");
                if (root.TryGetProperty("core_decision", out _)) _ = U32(root, "core_decision");
                var ev = ShortString(root, "event");
                var category = ShortString(root, "class");
                if (root.TryGetProperty("core_class", out _))
                    _ = ShortString(root, "core_class");

                // The proxy logs GetTickCount(), which may wrap. A backward
                // move also indicates a possible concatenation or reset.
                // Do not bridge TX/read windows across such a boundary.
                if (previousMs is uint last && ms < last)
                {
                    resets++;
                    resetWindows += successfulTxWindow.Count;
                    successfulTxWindow.Clear();
                    activeChannels.Clear();
                }
                previousMs = ms;

                switch (ev)
                {
                    case "connect":
                        if (status != 0 || channel == 0)
                        {
                            failedConnects++;
                            break;
                        }
                        connects++;
                        if (!activeChannels.Add(channel)) reconnected++;
                        successfulTxWindow.Remove(channel);
                        break;
                    case "disconnect":
                        if (!activeChannels.Contains(channel))
                        {
                            orphanCalls++;
                            break;
                        }
                        if (status == 0)
                        {
                            disconnects++;
                            activeChannels.Remove(channel);
                            successfulTxWindow.Remove(channel);
                        }
                        else disconnectedFailed++;
                        break;
                    case "write":
                        writes++;
                        if (!activeChannels.Contains(channel))
                        {
                            orphanCalls++;
                            break;
                        }
                        // Every write starts a new temporal window. A failed
                        // write can NEVER credit later reads as responses.
                        successfulTxWindow.Remove(channel);
                        if (requested > 1 || messages > 1) multiTx++;
                        // The source logs requested *NumMsgs and its result.
                        // An impossible over-return is untrusted metadata,
                        // even when the recorded native status says success.
                        if (messages > requested)
                        {
                            inconsistentWriteBatches++;
                            failedWrites++;
                            break;
                        }
                        if (status == 0 && messages > 0)
                        {
                            successfulWrites++;
                            successfulTxWindow.Add(channel);
                            switch (category)
                            {
                                case "a3_tx": a3++; break;
                                case "b9fb_tx": b9fb++; break;
                                case "b9e0_tx": b9e0++; break;
                                case "b9f0_tx": b9f0++; break;
                                case "b9fc_tx": b9fc++; break;
                            }
                        }
                        else failedWrites++;
                        break;
                    case "read":
                        reads++;
                        if (!activeChannels.Contains(channel))
                        {
                            orphanCalls++;
                            break;
                        }
                        if (messages > requested)
                        {
                            inconsistentReadBatches++;
                            // Once a recorded batch contradicts its requested
                            // count, no earlier accepted TX should lend later
                            // reads a plausible temporal association.
                            successfulTxWindow.Remove(channel);
                            break;
                        }
                        if (status == 0 && messages > 0)
                        {
                            successfulReadsWithData++;
                            if (messages > 1 || requested > 1) multiRx++;
                            if (successfulTxWindow.Contains(channel))
                                readsAfterTxWithData++;
                            else
                                readsBeforeTxWithData++;
                            switch (category)
                            {
                                case "e3_02_candidate": e302++; break;
                                case "f9e0_candidate": f9e0++; break;
                                case "f9f0_candidate": f9f0++; break;
                                case "d2_rx_fragment": fragments++; break;
                            }
                        }
                        break;
                    case "close":
                        otherEvents++;
                        if (status == 0)
                        {
                            successfulDeviceCloses++;
                            // The trace records the device handle on close, but
                            // records only a channel handle on connect. There is
                            // no reliable device-to-channel mapping. Conservatively
                            // invalidate ALL channel generations after a close.
                            closeInvalidatedChannels += activeChannels.Count;
                            closeInvalidatedTxWindows += successfulTxWindow.Count;
                            activeChannels.Clear();
                            successfulTxWindow.Clear();
                        }
                        else failedDeviceCloses++;
                        break;
                    case "open":
                    case "ioctl":
                        otherEvents++;
                        break;
                    default:
                        throw new InvalidDataException("Unsupported J2534 trace event.");
                }
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException("Malformed J2534 trace JSONL.", ex);
            }
        }

        // Construct output from fixed text and bounded integers ONLY.
        // Never include channel IDs, CAN IDs, raw strings, paths, exact
        // times, classifications from untrusted JSON or ECU data.
        return new StringBuilder()
            .AppendLine("J2534 SCHEMA-15 EVENT RESEARCH (OFFLINE, UNVERIFIED)")
            .AppendLine("Source: user-selected local trace; physical origin NOT authenticated.")
            .AppendLine("Trace may contain metadata that fingerprints a vehicle; review before sharing.")
            .Append("Events inspected: ").AppendLine(count.ToString(CultureInfo.InvariantCulture))
            .Append("Connected sessions reported: ").AppendLine(connects.ToString(CultureInfo.InvariantCulture))
            .Append("Failed connects / repeated channel connects: ").Append(failedConnects)
                .Append(" / ").AppendLine(reconnected.ToString(CultureInfo.InvariantCulture))
            .Append("Successful / failed disconnects: ").Append(disconnects)
                .Append(" / ").AppendLine(disconnectedFailed.ToString(CultureInfo.InvariantCulture))
            .Append("Writes observed / accepted / unsuccessful: ").Append(writes)
                .Append(" / ").Append(successfulWrites)
                .Append(" / ").AppendLine(failedWrites.ToString(CultureInfo.InvariantCulture))
            .Append("Reads observed / successful with data: ").Append(reads)
                .Append(" / ").AppendLine(successfulReadsWithData.ToString(CultureInfo.InvariantCulture))
            .Append("Data reads temporally after accepted TX: ")
                .AppendLine(readsAfterTxWithData.ToString(CultureInfo.InvariantCulture))
            .Append("Data reads without preceding accepted TX in channel window: ")
                .AppendLine(readsBeforeTxWithData.ToString(CultureInfo.InvariantCulture))
            .Append("Ambiguous multi-message TX / RX calls: ").Append(multiTx)
                .Append(" / ").AppendLine(multiRx.ToString(CultureInfo.InvariantCulture))
            .Append("Inconsistent TX / RX batch counts (returned > requested): ")
                .Append(inconsistentWriteBatches).Append(" / ")
                .AppendLine(inconsistentReadBatches.ToString(CultureInfo.InvariantCulture))
            .Append("Orphan channel calls / other events: ").Append(orphanCalls)
                .Append(" / ").AppendLine(otherEvents.ToString(CultureInfo.InvariantCulture))
            .Append("Timestamp regressions/wraps: ").Append(resets)
                .Append("; invalidated TX windows: ")
                .AppendLine(resetWindows.ToString(CultureInfo.InvariantCulture))
            .Append("Successful / failed device closes: ").Append(successfulDeviceCloses)
                .Append(" / ").AppendLine(failedDeviceCloses.ToString(CultureInfo.InvariantCulture))
            .Append("Channel observations / TX windows invalidated by device close: ")
                .Append(closeInvalidatedChannels).Append(" / ")
                .AppendLine(closeInvalidatedTxWindows.ToString(CultureInfo.InvariantCulture))
            .Append("Reported TX category A3 / B9FB / B9E0 / B9F0 / B9FC: ")
                .Append(a3).Append(" / ").Append(b9fb).Append(" / ")
                .Append(b9e0).Append(" / ").Append(b9f0).Append(" / ")
                .AppendLine(b9fc.ToString(CultureInfo.InvariantCulture))
            .Append("Reported RX candidates E3-02 / F9E0 / F9F0 / D2-fragment: ")
                .Append(e302).Append(" / ").Append(f9e0).Append(" / ")
                .Append(f9f0).Append(" / ").AppendLine(fragments.ToString(CultureInfo.InvariantCulture))
            .AppendLine("Temporal RX-after-TX is NOT verified ECU response, request pairing, VIN read or CEM acknowledgment.")
            .AppendLine("Read batches may contain many messages; only first category may be classified by source.")
            .AppendLine("A batch reporting more messages than requested is untrusted, not ECU evidence.")
            .AppendLine("Event ordering, clock continuity, channel origin and dropped events are UNKNOWN.")
            .AppendLine("Device close invalidates all channel evidence: device-to-channel mapping is absent.")
            .AppendLine("This is not a CAN CSV. There are NO frame bytes for ISO-TP reconstruction.")
            .AppendLine("No CAN IDs, channel IDs, paths, timestamps, VIN, PIN or raw JSON values are displayed.")
            .AppendLine("No J2534 connection, CAN transmission, security access or CEM configuration read/write.")
            .ToString();
    }

    internal static async Task<string> InspectFileAsync(
        string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        cancellationToken.ThrowIfCancellationRequested();
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (stream.Length is < 1 or > MaximumFileBytes)
            throw new InvalidDataException("Invalid J2534 trace file length.");
        var data = new byte[(int)stream.Length];
        await stream.ReadExactlyAsync(data, cancellationToken);
        if (await stream.ReadAsync(new byte[1], cancellationToken) != 0)
            throw new InvalidDataException("J2534 trace changed while being read.");
        if (data.Length >= 3 && data[0] == 0xEF &&
            data[1] == 0xBB && data[2] == 0xBF)
            throw new InvalidDataException("J2534 JSONL must be UTF-8 without BOM.");
        try
        {
            return BuildRedactedReport(StrictUtf8.GetString(data));
        }
        catch (DecoderFallbackException ex)
        {
            throw new InvalidDataException("Invalid J2534 trace UTF-8.", ex);
        }
    }

    private static uint U32(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) ||
            value.ValueKind != JsonValueKind.Number ||
            !value.TryGetUInt32(out var result))
            throw new InvalidDataException("Invalid numeric J2534 trace field.");
        return result;
    }

    private static string ShortString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) ||
            value.ValueKind != JsonValueKind.String ||
            value.GetString() is not string result ||
            result.Length is < 1 or > 64 ||
            result.Any(char.IsControl))
            throw new InvalidDataException("Invalid J2534 trace string field.");
        return result;
    }

    private static bool IsCanonicalCanId(string? id) =>
        id is { Length: 10 } && id.StartsWith("0x", StringComparison.Ordinal) &&
        id.AsSpan(2).IndexOfAnyExcept("0123456789ABCDEF") < 0;
}
