namespace Dvinholm.App;

/// <summary>
/// Pure RU/EN captions for an already observed passive CAN response.
/// These are presentation-only: never turn an inferred request address into
/// verified ECU identity, authorization or permission to transmit.
/// </summary>
internal static class AuroraEcuInventoryCaptions
{
    internal static string Frames(bool english) =>
        english ? "Frames" : "Кадры";

    internal static string MaxPayload(bool english) =>
        english ? "Max bytes" : "Макс. байт";

    internal static string RequestBasis(bool english, bool hasInferredRequest) =>
        hasInferredRequest
            ? (english ? "Inferred" : "Предполагается")
            : (english ? "Unknown" : "Неизвестно");
}
