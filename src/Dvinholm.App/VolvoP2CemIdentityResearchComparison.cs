using System.Globalization;
using System.Text;

namespace Dvinholm.App;

/// <summary>
/// Compares two already inspected, manually authored VIDA research cards.
/// Differences are observations about user-entered text, never ECU evidence,
/// transport parameters, permissions, live configuration or a PIN.
/// </summary>
internal static class VolvoP2CemIdentityResearchComparison
{
    internal static string BuildReadOnlyDiff(
        VolvoP2CemIdentityResearchDocument first,
        VolvoP2CemIdentityResearchDocument second)
    {
        // Reuse the same fail-closed review used by the file inspector. A
        // caller cannot make forged backup/access flags part of a comparison.
        VolvoP2CemIdentityResearchFileInspection.BuildReadOnlyReview(first);
        VolvoP2CemIdentityResearchFileInspection.BuildReadOnlyReview(second);
        EnsureCanonical(first);
        EnsureCanonical(second);

        var differences = 0;
        var output = new StringBuilder()
            .AppendLine("XC90 P2 CEM identity research comparison (OFFLINE)")
            .AppendLine("UNVERIFIED USER-TRANSCRIBED CLAIMS, not authenticated VIDA or ECU data.")
            .AppendLine("First = previously opened JSON; Second = explicitly selected comparison JSON.")
            .AppendLine("Part numbers and model year may describe different modules or revisions.")
            .AppendLine();

        CompareField(output, "Claimed model year",
            first.ClaimedModelYear.ToString(CultureInfo.InvariantCulture),
            second.ClaimedModelYear.ToString(CultureInfo.InvariantCulture),
            ref differences);
        CompareField(output, "Claimed hardware P/N",
            first.ClaimedHardwarePartNumber, second.ClaimedHardwarePartNumber,
            ref differences);
        CompareField(output, "Claimed software P/N",
            first.ClaimedSoftwarePartNumber, second.ClaimedSoftwarePartNumber,
            ref differences);
        CompareField(output, "Claimed diagnostic software P/N",
            first.ClaimedDiagnosticSoftwarePartNumber,
            second.ClaimedDiagnosticSoftwarePartNumber, ref differences);

        output.AppendLine();
        output.Append("Claimed identity-field differences: ")
            .Append(differences).AppendLine("/4.");
        output.Append("Claimed VIDA source flag: ")
            .Append(first.UserClaimsVidaSource == second.UserClaimsVidaSource
                ? "SAME" : "DIFFERENT").AppendLine();
        output.Append("  First: ").AppendLine(first.UserClaimsVidaSource
            ? "user says transcribed from VIDA" : "no VIDA transcription claimed");
        output.Append("  Second: ").AppendLine(second.UserClaimsVidaSource
            ? "user says transcribed from VIDA" : "no VIDA transcription claimed");
        output.AppendLine();
        output.AppendLine("Matching fields do NOT establish that two physical CEMs are identical.");
        output.AppendLine("Differences do NOT determine compatibility, network addressing or firmware support.");
        output.AppendLine("No file path, VIN, PIN, serial, secret or raw ECU response is included.");
        output.AppendLine("No CAN/J2534/UDS request, profile import, configuration read/write or PIN operation.");
        output.AppendLine("Nothing is saved automatically. No service permission is granted.");
        return output.ToString();
    }

    private static void CompareField(
        StringBuilder output, string label, string first, string second,
        ref int differences)
    {
        var same = string.Equals(first, second, StringComparison.Ordinal);
        if (!same)
            differences++;
        output.Append(label).Append(": ").AppendLine(same ? "SAME" : "DIFFERENT");
        output.Append("  First: ").AppendLine(first);
        output.Append("  Second: ").AppendLine(second);
    }

    private static void EnsureCanonical(VolvoP2CemIdentityResearchDocument record)
    {
        if (!Canonical(record.ClaimedHardwarePartNumber) ||
            !Canonical(record.ClaimedSoftwarePartNumber) ||
            !Canonical(record.ClaimedDiagnosticSoftwarePartNumber))
            throw new InvalidDataException("Noncanonical CEM identity research card.");
    }

    private static bool Canonical(string? partNumber) =>
        partNumber is not null &&
        partNumber == partNumber.Trim().ToUpperInvariant();
}
