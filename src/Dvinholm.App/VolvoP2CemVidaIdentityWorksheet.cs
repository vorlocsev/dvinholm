using System.Globalization;
using System.Text;

namespace Dvinholm.App;

/// <summary>
/// Offline worksheet for identifiers a person transcribed from an
/// authorized diagnostic session. This does NOT authenticate their source,
/// recognize a physical CEM or introduce any actual diagnostic request.
/// </summary>
internal enum VolvoP2CemVidaWorksheetKind
{
    MissingFields,
    InvalidModelYear,
    InvalidPartNumber,
    ManualClaimUnverified
}

internal sealed record VolvoP2CemVidaWorksheetAssessment(
    VolvoP2CemVidaWorksheetKind Kind,
    string Summary)
{
    internal bool IsVerifiedPhysicalCem => false;
    internal bool HasVerifiedCanOrKLineTransport => false;
    internal bool HasVerifiedConfigurationMapping => false;
    internal bool MayRequestLiveEcu => false;
    internal bool MayRequestPin => false;
    internal bool MayEditOrWriteConfiguration => false;
}

/// <summary>
/// Collect exactly the three part-number fields described for VIDA ECU
/// identification. Do not accept a VIN, hardware serial, PIN, owner name,
/// arbitrary source note or raw diagnostic bytes. No persistence or logging.
/// </summary>
internal static class VolvoP2CemVidaIdentityWorksheet
{
    internal const int FirstGenerationXc90EarliestModelYear = 2002;
    internal const int FirstGenerationXc90LastModelYear = 2014;
    internal const int MaximumPartNumberCharacters = 40;

    internal static VolvoP2CemVidaWorksheetAssessment Assess(
        string? modelYearText,
        string? hardwarePartNumber,
        string? softwarePartNumber,
        string? diagnosticSoftwarePartNumber,
        bool manuallyTranscribedFromVida)
    {
        if (string.IsNullOrWhiteSpace(modelYearText) ||
            string.IsNullOrWhiteSpace(hardwarePartNumber) ||
            string.IsNullOrWhiteSpace(softwarePartNumber) ||
            string.IsNullOrWhiteSpace(diagnosticSoftwarePartNumber))
            return Result(VolvoP2CemVidaWorksheetKind.MissingFields,
                "A claimed model year and three independently transcribed part-number fields are required.");

        if (!int.TryParse(modelYearText, NumberStyles.None,
                CultureInfo.InvariantCulture, out var modelYear) ||
            modelYearText.Length != 4 ||
            modelYear < FirstGenerationXc90EarliestModelYear ||
            modelYear > FirstGenerationXc90LastModelYear)
            return Result(VolvoP2CemVidaWorksheetKind.InvalidModelYear,
                "Model year is outside the research scope for first-generation XC90.");

        var hardware = hardwarePartNumber.Trim().ToUpperInvariant();
        var software = softwarePartNumber.Trim().ToUpperInvariant();
        var diagnostics = diagnosticSoftwarePartNumber.Trim().ToUpperInvariant();

        if (!ValidPartNumber(hardware) || !ValidPartNumber(software) ||
            !ValidPartNumber(diagnostics))
            return Result(VolvoP2CemVidaWorksheetKind.InvalidPartNumber,
                "One or more part-number fields have an invalid form. Do not enter VIN, " +
                "serial number, PIN, passwords or raw ECU values.");

        var report = new StringBuilder()
            .AppendLine("Dvinholm XC90 P2 CEM | manual identity research worksheet")
            .AppendLine("UNVERIFIED USER-TRANSCRIBED CLAIM. No ECU identity is authenticated.")
            .AppendLine("These are textual part-number notes, NOT manufacturer protocol IDs.")
            .Append("Claimed XC90 model year: ").AppendLine(
                modelYear.ToString(CultureInfo.InvariantCulture))
            .Append("Claimed hardware P/N: ").AppendLine(hardware)
            .Append("Claimed software P/N: ").AppendLine(software)
            .Append("Claimed diagnostic software P/N: ").AppendLine(diagnostics)
            .Append("Claimed source: ").AppendLine(manuallyTranscribedFromVida
                ? "User says these fields were read in VIDA (not independently verified)"
                : "Other / not independently documented")
            .AppendLine("Hardware serial and VIN: intentionally not collected.")
            .AppendLine()
            .AppendLine(VolvoP2CemCommunityPartNumberResearch.BuildResearchNote(hardware))
            .AppendLine("Transport, firmware compatibility, configuration mappings and PIN: UNKNOWN.")
            .AppendLine("No CAN/UDS/K-Line requests, configuration writes or PIN operations enabled.")
            .AppendLine("Review before copying or sharing. Part numbers can be identifying metadata.")
            .ToString();

        return Result(VolvoP2CemVidaWorksheetKind.ManualClaimUnverified, report);
    }

    private static bool ValidPartNumber(string text)
    {
        if (text.Length is < 3 or > MaximumPartNumberCharacters ||
            !text.Any(c => c is >= '0' and <= '9') ||
            !text.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'Z'
                or '.' or '-' or '_' or '/'))
            return false;

        // A 17-character uninterrupted alphanumeric identifier could be a
        // vehicle VIN. Reject it rather than silently reusing private data.
        if (text.Length == 17 &&
            text.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'Z'))
            return false;

        return true;
    }

    private static VolvoP2CemVidaWorksheetAssessment Result(
        VolvoP2CemVidaWorksheetKind kind, string summary) => new(kind, summary);
}
