namespace Dvinholm.App;

/// <summary>
/// Manual artwork choices only. The selected illustration does not decode VIN,
/// identify ECUs, validate supported services or determine a physical bus.
/// Atlas coordinates are fixed offline UI resources, never vehicle metadata.
/// </summary>
internal sealed record AuroraVehicleArtwork(
    string Id, string Label, string Platform, int AtlasColumn, int AtlasRow);

internal static class AuroraVehicleArtworkCatalog
{
    internal const int CellWidth = 640;
    internal const int CellHeight = 255;
    internal const string Asset = "ms-appx:///Assets/Vehicles/Volvo-Model-Atlas.svg";

    internal static readonly IReadOnlyList<AuroraVehicleArtwork> Models =
    [
        new("xc90-p2", "Volvo XC90 I · P2", "P2", 0, 0),
        new("xc70-p2", "Volvo XC70 II · P2", "P2", 1, 0),
        new("v70-p2", "Volvo V70 II · P2", "P2", 2, 0),
        new("s60-p2", "Volvo S60 I · P2", "P2", 0, 1),
        new("s80-p2", "Volvo S80 I · P2", "P2", 1, 1),
        // Second-generation XC90 is SPA, never mislabel it P3.
        new("xc90-spa", "Volvo XC90 II · SPA", "SPA", 2, 1),
        new("xc70-p3", "Volvo XC70 III · P3", "P3", 0, 2),
        new("v70-p3", "Volvo V70 III · P3", "P3", 1, 2),
        new("s80-p3", "Volvo S80 II · P3", "P3", 2, 2)
    ];

    internal static AuroraVehicleArtwork Resolve(string? id) =>
        Models.FirstOrDefault(model => string.Equals(model.Id, id, StringComparison.Ordinal))
        ?? Models[0];
}
