namespace Dvinholm.App.Tests;

/// <summary>
/// Eight deterministic offline tests. No picker, physical adapter,
/// vehicle, ECU service, configuration write or PIN search is involved.
/// </summary>
public sealed class VolvoP2CemIdentityResearchComparisonTests
{
    [Fact]
    public void IdenticalResearchCardsRemainUnverifiedAndReadOnly()
    {
        var report = Compare(Card(), Card());
        Assert.Contains("Claimed identity-field differences: 0/4", report);
        Assert.Contains("Claimed hardware P/N: SAME", report);
        Assert.Contains("UNVERIFIED USER-TRANSCRIBED CLAIMS", report);
        Assert.Contains("No CAN/J2534/UDS request", report);
        Assert.Contains("No service permission is granted", report);
        Assert.DoesNotContain("file://", report);
    }

    [Fact]
    public void HardwarePartNumberDifferenceIsVisible()
    {
        var report = Compare(Card(), Card(hardware: "HW999"));
        Assert.Contains("Claimed hardware P/N: DIFFERENT", report);
        Assert.Contains("First: HW123", report);
        Assert.Contains("Second: HW999", report);
        Assert.Contains("Claimed identity-field differences: 1/4", report);
    }

    [Fact]
    public void SoftwarePartNumberDifferenceIsVisible()
    {
        var report = Compare(Card(), Card(software: "SW999"));
        Assert.Contains("Claimed software P/N: DIFFERENT", report);
        Assert.Contains("Claimed diagnostic software P/N: SAME", report);
        Assert.Contains("Claimed identity-field differences: 1/4", report);
    }

    [Fact]
    public void DiagnosticSoftwarePartNumberDifferenceIsVisible()
    {
        var report = Compare(Card(), Card(diagnostics: "DS999"));
        Assert.Contains("Claimed diagnostic software P/N: DIFFERENT", report);
        Assert.Contains("Claimed hardware P/N: SAME", report);
        Assert.Contains("Claimed identity-field differences: 1/4", report);
    }

    [Fact]
    public void YearDifferenceIsAClaimAndNotACompatibilityDecision()
    {
        var report = Compare(Card(), Card(year: "2008"));
        Assert.Contains("Claimed model year: DIFFERENT", report);
        Assert.Contains("First: 2007", report);
        Assert.Contains("Second: 2008", report);
        Assert.Contains("Differences do NOT determine compatibility", report);
        Assert.Contains("Claimed identity-field differences: 1/4", report);
    }

    [Fact]
    public void VidaSourceSelfReportDoesNotIncreaseIdentityDifferenceCount()
    {
        var report = Compare(Card(), Card(vida: true));
        Assert.Contains("Claimed identity-field differences: 0/4", report);
        Assert.Contains("Claimed VIDA source flag: DIFFERENT", report);
        Assert.Contains("user says transcribed from VIDA", report);
        Assert.Contains("not authenticated VIDA or ECU data", report);
    }

    [Fact]
    public void ForgedFirstCardCannotClaimBackupOrEcuAccess()
    {
        foreach (var forged in new[]
        {
            Card() with { IsRestorableBackup = true },
            Card() with { MayAccessEcu = true },
            Card() with { IsUnverifiedManualClaim = false }
        })
        {
            Assert.Throws<InvalidDataException>(() => Compare(forged, Card()));
        }
    }

    [Fact]
    public void ForgedSecondCardOrNoncanonicalNumberFailsClosed()
    {
        foreach (var forged in new[]
        {
            Card() with { MayAccessEcu = true },
            Card() with { ClaimedHardwarePartNumber = "hw123" },
            Card() with { ClaimedModelYear = 2099 },
            Card() with { ClaimedSoftwarePartNumber = "NO VALID NUMBER" }
        })
        {
            Assert.Throws<InvalidDataException>(() => Compare(Card(), forged));
        }
    }

    private static string Compare(
        VolvoP2CemIdentityResearchDocument first,
        VolvoP2CemIdentityResearchDocument second) =>
        VolvoP2CemIdentityResearchComparison.BuildReadOnlyDiff(first, second);

    private static VolvoP2CemIdentityResearchDocument Card(
        string year = "2007", string hardware = "HW123",
        string software = "SW456", string diagnostics = "DS789",
        bool vida = false) =>
        VolvoP2CemIdentityResearchExport.InspectJson(
            VolvoP2CemIdentityResearchExport.CreateJson(
                year, hardware, software, diagnostics, vida));
}
