namespace Dvinholm.App;

/// <summary>
/// Isolates settings for a verified, unpacked 0.2.3 human-acceptance copy.
/// The marker also survives direct EXE relaunch without the batch launcher.
/// Normal installed Stable/Development copies still use %LOCALAPPDATA%\Dvinholm.
/// </summary>
internal static class DvinholmUserDataDirectory
{
    internal const string AcceptanceVariable = "DVINHOLM_ACCEPTANCE_DATA_DIR";
    internal const string AcceptanceFolder = "acceptance-user-data";
    internal const string AcceptanceReceipt = "ACCEPTANCE-0.2.3.txt";

    private static bool HasAcceptanceReceipt =>
        File.Exists(Path.Combine(AppContext.BaseDirectory, AcceptanceReceipt));

    internal static bool IsAcceptanceLaunch =>
        HasAcceptanceReceipt || Environment.GetEnvironmentVariable(AcceptanceVariable) is not null;

    internal static string Current => Resolve(
        AppContext.BaseDirectory,
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        Environment.GetEnvironmentVariable(AcceptanceVariable),
        HasAcceptanceReceipt);

    internal static string Resolve(
        string applicationDirectory,
        string localApplicationDataDirectory,
        string? acceptanceDirectory,
        bool hasVerifiedReceipt)
    {
        if (acceptanceDirectory is null && !hasVerifiedReceipt)
            return Path.Combine(localApplicationDataDirectory, "Dvinholm");

        // An explicit request must never silently fall back to v0.2.2 data.
        // An extracted copy with a verified receipt isolates itself even
        // when a tester double-clicks Dvinholm.App.exe without the launcher.
        if (!hasVerifiedReceipt ||
            (acceptanceDirectory is not null &&
             (string.IsNullOrWhiteSpace(acceptanceDirectory) ||
              !Path.IsPathFullyQualified(acceptanceDirectory))))
            throw new InvalidOperationException(
                "Acceptance mode requires the extracted receipt and an absolute private data path.");

        var expected = Path.GetFullPath(Path.Combine(applicationDirectory, AcceptanceFolder));
        if (acceptanceDirectory is not null &&
            !string.Equals(expected, Path.GetFullPath(acceptanceDirectory), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Acceptance data must remain inside the private acceptance-user-data folder.");

        return expected;
    }
}
