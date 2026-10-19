using System.Globalization;

namespace Dvinholm.App;

/// <summary>
/// Validates local ECU profile drafts before writing a complete catalog.
/// This editor never sends vehicle requests and does not confer verification.
/// </summary>
internal sealed record EcuProfileDraft(
    string Id,
    string Name,
    string RequestId,
    string ResponseId,
    bool IsExtended,
    string CandidateDids,
    string Source);

internal static class EcuProfileEditor
{
    internal static (EcuProfileCatalog Catalog, EcuProfile Profile) Save(
        EcuProfileCatalog existing,
        EcuProfileDraft draft,
        string? editingId = null)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(draft);

        var id = (draft.Id ?? string.Empty).Trim();
        var name = (draft.Name ?? string.Empty).Trim();
        var source = (draft.Source ?? string.Empty).Trim();

        if (id.Length == 0 || id.Length > 120)
            throw new ArgumentException("Profile ID must contain 1–120 characters.");
        if (name.Length == 0 || name.Length > 160)
            throw new ArgumentException("Profile name must contain 1–160 characters.");
        if (source.Length == 0 || source.Length > 2000)
            throw new ArgumentException("Provide a source / verification note (up to 2000 characters).");

        if (editingId is null)
        {
            if (existing.TryGet(id, out _))
                throw new ArgumentException($"Profile ID '{id}' already exists. Choose a unique ID.");
        }
        else
        {
            if (!existing.TryGet(editingId, out _))
                throw new ArgumentException("The selected profile no longer exists.");
            // Stable IDs are used as the identity in local catalogs; editing
            // them here would implicitly create a different profile.
            if (!string.Equals(id, editingId, StringComparison.Ordinal))
                throw new ArgumentException("Profile ID cannot be changed when editing.");
        }

        var maxId = draft.IsExtended ? 0x1FFFFFFFu : 0x7FFu;
        var requestId = ParseCanId(draft.RequestId, maxId, "Request ID");
        var responseId = ParseCanId(draft.ResponseId, maxId, "Response ID");
        if (requestId == responseId)
            throw new ArgumentException("Request and response CAN IDs must be different.");

        var dids = ParseDids(draft.CandidateDids);
        // Any edit invalidates an earlier self-reported verification. A new
        // profile is always unverified. Neither can be used as a compatibility
        // assertion without a separately audited evidence workflow.
        var next = new EcuProfile(id, name, requestId, responseId,
            draft.IsExtended, dids, source, isVerified: false);

        var entries = existing.All
            .Where(p => editingId is null ||
                        !string.Equals(p.Id, editingId, StringComparison.OrdinalIgnoreCase))
            .Append(next)
            .ToArray();
        return (new EcuProfileCatalog(entries), next);
    }

    private static uint ParseCanId(string? input, uint maximum, string field)
    {
        var value = (input ?? string.Empty).Trim();
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            value = value[2..];

        if (value.Length == 0 ||
            !uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var result) ||
            result > maximum)
            throw new ArgumentException(
                $"{field} must be hexadecimal between 0 and {maximum:X}.");

        return result;
    }

    private static IReadOnlyList<ushort> ParseDids(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Array.Empty<ushort>();

        var tokens = value.Split(
            [',', ';', ' ', '\t', '\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length > 128)
            throw new ArgumentException("Enter at most 128 candidate DIDs.");

        var result = new List<ushort>(tokens.Length);
        var unique = new HashSet<ushort>();
        foreach (var raw in tokens)
        {
            var text = raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? raw[2..] : raw;
            if (text.Length == 0 ||
                !ushort.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var did))
                throw new ArgumentException($"Invalid hexadecimal DID '{raw}'.");
            if (!unique.Add(did))
                throw new ArgumentException($"Duplicate candidate DID {did:X4}.");
            result.Add(did);
        }
        return result;
    }
}
