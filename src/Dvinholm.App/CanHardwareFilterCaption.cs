namespace Dvinholm.App;

/// <summary>
/// Display-only label for the existing J2534 hardware-filter toggle.
/// The active flag comes from a successful adapter operation; this helper
/// cannot start, remove, or otherwise modify a CAN filter.
/// </summary>
internal static class CanHardwareFilterCaption
{
    internal static string Text(bool active, bool english) => (active, english) switch
    {
        (false, false) => "Применить аппаратный фильтр",
        (false, true) => "Apply HW filter",
        (true, false) => "Убрать аппаратный фильтр",
        (true, true) => "Remove HW filter"
    };
}
