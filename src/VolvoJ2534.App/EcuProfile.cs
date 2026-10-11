namespace VolvoJ2534.App;

/// <summary>
/// Data-only definition of a UDS ECU endpoint. Profiles must be based on a
/// documented or observed addressing scheme; a profile is not proof that an
/// ECU supports every listed DID.
/// </summary>
internal sealed class EcuProfile
{
    internal EcuProfile(
        string id,
        string name,
        uint requestId,
        uint responseId,
        bool isExtended,
        IEnumerable<ushort>? readableDataIdentifiers,
        string source,
        bool isVerified = false)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Profile ID is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Profile name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(source))
            throw new ArgumentException("A source or verification note is required.", nameof(source));

        var maxId = isExtended ? 0x1FFFFFFFu : 0x7FFu;
        if (requestId > maxId)
            throw new ArgumentOutOfRangeException(nameof(requestId), $"Request ID exceeds the {(isExtended ? "29-bit" : "11-bit")} CAN ID range.");
        if (responseId > maxId)
            throw new ArgumentOutOfRangeException(nameof(responseId), $"Response ID exceeds the {(isExtended ? "29-bit" : "11-bit")} CAN ID range.");
        if (requestId == responseId)
            throw new ArgumentException("Request and response CAN IDs must be different.");

        Id = id.Trim();
        Name = name.Trim();
        RequestId = requestId;
        ResponseId = responseId;
        IsExtended = isExtended;
        Source = source.Trim();
        IsVerified = isVerified;
        ReadableDataIdentifiers = new HashSet<ushort>(readableDataIdentifiers ?? Array.Empty<ushort>());
    }

    internal string Id { get; }
    internal string Name { get; }
    internal uint RequestId { get; }
    internal uint ResponseId { get; }
    internal bool IsExtended { get; }
    internal string Source { get; }
    internal bool IsVerified { get; }
    internal IReadOnlySet<ushort> ReadableDataIdentifiers { get; }
}

/// <summary>
/// In-memory profile catalog with case-insensitive stable IDs.
/// The initial catalog is intentionally empty until profile data has a reliable source.
/// </summary>
internal sealed class EcuProfileCatalog
{
    private readonly Dictionary<string, EcuProfile> _profiles =
        new(StringComparer.OrdinalIgnoreCase);

    internal EcuProfileCatalog(IEnumerable<EcuProfile>? profiles = null)
    {
        if (profiles is null)
            return;

        foreach (var profile in profiles)
            Add(profile);
    }

    internal IReadOnlyList<EcuProfile> All =>
        _profiles.Values.OrderBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase).ToArray();

    internal IReadOnlyList<EcuProfile> Verified =>
        _profiles.Values.Where(profile => profile.IsVerified)
            .OrderBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase).ToArray();

    internal bool TryGet(string id, out EcuProfile? profile)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            profile = null;
            return false;
        }

        return _profiles.TryGetValue(id.Trim(), out profile);
    }

    internal void Add(EcuProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (!_profiles.TryAdd(profile.Id, profile))
            throw new ArgumentException($"An ECU profile with ID '{profile.Id}' already exists.", nameof(profile));
    }
}
