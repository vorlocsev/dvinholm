using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dvinholm.App;

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
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

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

    /// <summary>Exports profiles as editable JSON with CAN IDs in hexadecimal.</summary>
    internal string ToJson()
    {
        var documents = All.Select(profile => new ProfileDocument
        {
            Id = profile.Id,
            Name = profile.Name,
            RequestId = profile.RequestId.ToString(profile.IsExtended ? "X8" : "X3"),
            ResponseId = profile.ResponseId.ToString(profile.IsExtended ? "X8" : "X3"),
            IsExtended = profile.IsExtended,
            ReadableDataIdentifiers = profile.ReadableDataIdentifiers.Order().Select(did => did.ToString("X4")).ToArray(),
            Source = profile.Source,
            IsVerified = profile.IsVerified
        }).ToArray();

        return JsonSerializer.Serialize(documents, JsonOptions);
    }

    /// <summary>
    /// Imports a complete catalog. Invalid entries fail the import instead of
    /// silently dropping data or accepting invalid CAN addressing.
    /// </summary>
    internal static EcuProfileCatalog FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("Profile JSON cannot be empty.", nameof(json));

        ProfileDocument?[] documents;
        try
        {
            documents = JsonSerializer.Deserialize<ProfileDocument?[]>(json, JsonOptions)
                ?? throw new InvalidDataException("Profile JSON must be an array.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Profile JSON is malformed.", ex);
        }

        var profiles = new List<EcuProfile>(documents.Length);
        for (var index = 0; index < documents.Length; index++)
        {
            var document = documents[index]
                ?? throw new InvalidDataException($"Profile entry at index {index} is null.");

            if (!TryParseCanId(document.RequestId, out var requestId))
                throw new InvalidDataException($"Profile '{document.Id}' has an invalid hexadecimal requestId.");
            if (!TryParseCanId(document.ResponseId, out var responseId))
                throw new InvalidDataException($"Profile '{document.Id}' has an invalid hexadecimal responseId.");

            var dids = new List<ushort>();
            foreach (var value in document.ReadableDataIdentifiers ?? Array.Empty<string>())
            {
                if (!ushort.TryParse(value, System.Globalization.NumberStyles.HexNumber,
                        System.Globalization.CultureInfo.InvariantCulture, out var did))
                    throw new InvalidDataException($"Profile '{document.Id}' has an invalid hexadecimal DID '{value}'.");
                dids.Add(did);
            }

            try
            {
                profiles.Add(new EcuProfile(
                    document.Id ?? string.Empty,
                    document.Name ?? string.Empty,
                    requestId,
                    responseId,
                    document.IsExtended,
                    dids,
                    document.Source ?? string.Empty,
                    document.IsVerified));
            }
            catch (ArgumentException ex)
            {
                throw new InvalidDataException($"Profile at index {index} is invalid: {ex.Message}", ex);
            }
        }

        try
        {
            return new EcuProfileCatalog(profiles);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidDataException("Profile catalog contains duplicate or invalid IDs.", ex);
        }
    }

    private static bool TryParseCanId(string? text, out uint id) =>
        uint.TryParse(text, System.Globalization.NumberStyles.HexNumber,
            System.Globalization.CultureInfo.InvariantCulture, out id);

    private sealed class ProfileDocument
    {
        public ProfileDocument() { }

        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? RequestId { get; set; }
        public string? ResponseId { get; set; }
        public bool IsExtended { get; set; }
        public string[]? ReadableDataIdentifiers { get; set; }
        public string? Source { get; set; }
        public bool IsVerified { get; set; }
    }
}
