namespace Dvinholm.App;

/// <summary>
/// Validates a complete imported ECU catalog and durably persists it before
/// returning it to the UI. An exception never publishes a replacement catalog.
/// This is local JSON only: it does not touch the J2534 adapter or send UDS.
/// </summary>
internal static class EcuProfileCatalogImporter
{
    internal static EcuProfileCatalog ImportAndPersist(
        string json,
        Action<EcuProfileCatalog> persist)
    {
        ArgumentNullException.ThrowIfNull(persist);

        // Parse *all* entries first: a single invalid entry must not change
        // the on-disk catalog or the in-memory selection.
        var candidate = EcuProfileCatalog.FromJson(json);

        // EcuProfileCatalogStore.Save already stages into a sibling temporary
        // file and replaces the previous file only after staging succeeds.
        persist(candidate);

        // The caller may publish this immutable replacement only after Save.
        return candidate;
    }
}
