using System.Globalization;

namespace Dvinholm.App;

/// <summary>
/// Payload-free public activity for local ECU profile actions. SetStatus is
/// mirrored to the Aurora home page: user-provided profile names, filenames,
/// CAN addresses and exception text belong in their dedicated controls, not
/// in global activity. No vehicle communication or file I/O.
/// </summary>
internal enum EcuProfileActivityKind
{
    SavedUnverified,
    Applied,
    Imported,
    Exported,
    ExportBlocked
}

internal static class EcuProfileActivity
{
    internal static string Message(EcuProfileActivityKind kind, int profileCount = 0)
    {
        if (profileCount < 0)
            throw new ArgumentOutOfRangeException(nameof(profileCount));

        return kind switch
        {
            EcuProfileActivityKind.SavedUnverified =>
                "ECU profile saved as unverified. No diagnostic request was sent.",
            EcuProfileActivityKind.Applied =>
                "ECU profile selected. Check its source and CAN IDs before a read. No diagnostic request was sent.",
            EcuProfileActivityKind.Imported =>
                "Imported and saved " + profileCount.ToString(CultureInfo.InvariantCulture) +
                " ECU profile(s). Select a profile and verify its CAN IDs before use.",
            EcuProfileActivityKind.Exported =>
                "Exported " + profileCount.ToString(CultureInfo.InvariantCulture) +
                " ECU profile(s) to the selected file. Review before sharing.",
            EcuProfileActivityKind.ExportBlocked =>
                "ECU profile export blocked. Check the catalog and selected destination; no new export was saved.",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }
}
