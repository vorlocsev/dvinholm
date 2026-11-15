using System.Text.RegularExpressions;

namespace Dvinholm.App;

/// <summary>
/// Display-only hint based on local J2534 registry registration labels.
/// A product word in a registry key is NOT a USB detection, vendor identity,
/// installation check, hardware capability, or permission to connect.
/// </summary>
internal enum J2534RegistrationFamily
{
    NoMatchingRegistration,
    DiCE,
    Mongoose,
    GenericJ2534,
    Ambiguous
}

internal static class J2534RegistrationIdentity
{
    // Product-token boundaries: "DiCE64" is permitted, but "Medice" is not.
    // Registry text is untrusted and capped to prevent unbounded matching.
    private static readonly Regex DiceToken =
        new(@"(?<![A-Za-z])dice(?![A-Za-z])",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex MongooseToken =
        new(@"(?<![A-Za-z])mongoose",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    internal static J2534RegistrationFamily Classify(J2534Device? device,
        string? currentDllPath)
    {
        if (device is null ||
            string.IsNullOrWhiteSpace(device.DllPath) ||
            !string.Equals(device.DllPath, currentDllPath?.Trim(),
                StringComparison.OrdinalIgnoreCase))
            return J2534RegistrationFamily.NoMatchingRegistration;

        // Do not include DLL paths or copy untrusted registration strings
        // to the UI. Only fixed, translated descriptions leave this helper.
        var name = ((device.Name ?? string.Empty) + " " +
            (device.Vendor ?? string.Empty));
        if (name.Length > 512)
            name = name[..512];
        var dice = DiceToken.IsMatch(name);
        var mongoose = MongooseToken.IsMatch(name);
        if (dice && mongoose)
            return J2534RegistrationFamily.Ambiguous;
        if (dice)
            return J2534RegistrationFamily.DiCE;
        if (mongoose)
            return J2534RegistrationFamily.Mongoose;
        return J2534RegistrationFamily.GenericJ2534;
    }

    internal static string Caption(J2534Device? selected, string? currentDllPath,
        bool english)
    {
        var family = Classify(selected, currentDllPath);
        var label = family switch
        {
            J2534RegistrationFamily.NoMatchingRegistration =>
                english ? "no matching registration / manual DLL path" :
                    "нет выбранной записи / путь DLL введён вручную",
            J2534RegistrationFamily.DiCE => "DiCE",
            J2534RegistrationFamily.Mongoose => "Mongoose",
            J2534RegistrationFamily.GenericJ2534 =>
                english ? "generic J2534 / other label" :
                    "обычный J2534 / другое название",
            _ => english ? "ambiguous DiCE / Mongoose label" :
                "неоднозначное название DiCE / Mongoose"
        };
        return english
            ? "Registration name hint: " + label +
              ". Registry label only; no driver or physical adapter verified."
            : "Подсказка по записи реестра: " + label +
              ". Только название; драйвер и физический адаптер не проверены.";
    }
}
