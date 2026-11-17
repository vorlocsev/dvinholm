namespace Dvinholm.App;

/// <summary>
/// Manual artwork choices only. The selected illustration does not decode VIN,
/// identify ECUs, validate supported services or determine a physical bus.
/// Only local JPG paths are resolved; a manual choice never identifies a vehicle.
/// </summary>
internal sealed record AuroraVehicleArtwork(
    string Id, string Label, string Platform);

internal static class AuroraVehicleArtworkCatalog
{
    internal const string Asset = "ms-appx:///Assets/Vehicles/xc90-p2.jpg";

    internal static string ImageUri(AuroraVehicleArtwork model) =>
        "ms-appx:///Assets/Vehicles/" + model.Id + ".jpg";

    internal static readonly IReadOnlyList<AuroraVehicleArtwork> Models =
    [
        new("xc90-p2", "Volvo XC90 I · P2", "P2"),
        new("xc70-p2", "Volvo XC70 II · P2", "P2"),
        new("v70-p2", "Volvo V70 II · P2", "P2"),
        new("s60-p2", "Volvo S60 I · P2", "P2"),
        new("s80-p2", "Volvo S80 I · P2", "P2"),
        // Second-generation XC90 is SPA, never mislabel it P3.
        new("xc90-spa", "Volvo XC90 II · SPA", "SPA"),
        new("xc70-p3", "Volvo XC70 III · P3", "P3"),
        new("v70-p3", "Volvo V70 III · P3", "P3"),
        new("s80-p3", "Volvo S80 II · P3", "P3")
    ];

    internal static AuroraVehicleArtwork Resolve(string? id) =>
        Models.FirstOrDefault(model => string.Equals(model.Id, id, StringComparison.Ordinal))
        ?? Models[0];
}
