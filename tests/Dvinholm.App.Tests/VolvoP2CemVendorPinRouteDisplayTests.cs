namespace Dvinholm.App.Tests;

/// <summary>Eight synthetic offline UI-report tests; no adapter or secrets.</summary>
public sealed class VolvoP2CemVendorPinRouteDisplayTests
{
    private static string Report(
        string? year, bool checkedYear, VolvoP2CemClaimedEngine engine,
        VolvoP2CemVendorPinTool tool) =>
        VolvoP2CemVendorPinRouteDisplay.Build(year, checkedYear, engine, tool);

    [Fact]
    public void V8VdashShowsSeparateKeyWarningButNeverAccess()
    {
        var result = Report("2007", true, VolvoP2CemClaimedEngine.V8_44,
            VolvoP2CemVendorPinTool.Vdash);
        Assert.Contains("VDASH", result);
        Assert.Contains("4.4 V8", result);
        Assert.Contains("KEY PROGRAMMING SEPARATE", result);
        Assert.Contains("caveat: YES", result);
        Assert.Contains("DISABLED", result);
    }

    [Fact]
    public void V8VddIsExcludedFromVddClaimOnly()
    {
        var result = Report("2007", true, VolvoP2CemClaimedEngine.V8_44,
            VolvoP2CemVendorPinTool.Vdd);
        Assert.Contains("VDD", result);
        Assert.Contains("VENDOR EXCLUDES", result);
        Assert.Contains("PIN acquisition, key programming and ECU configuration: DISABLED",
            result);
    }

    [Fact]
    public void SixCylinderAndHighOutputD5ShowDifferentProductClaims()
    {
        foreach (var engine in new[]
        {
            VolvoP2CemClaimedEngine.InlineSix32,
            VolvoP2CemClaimedEngine.D5_147Kw
        })
        {
            Assert.Contains("ADVERTISES DECODING", Report("2008", true, engine,
                VolvoP2CemVendorPinTool.Vdash));
            Assert.Contains("VENDOR EXCLUDES", Report("2008", true, engine,
                VolvoP2CemVendorPinTool.Vdd));
        }
    }

    [Fact]
    public void LateD5KeyWarningIsDistinctFromVddPinDecoding()
    {
        var late = Report("2013", true, VolvoP2CemClaimedEngine.D5_120Kw,
            VolvoP2CemVendorPinTool.Vdd);
        var early = Report("2012", true, VolvoP2CemClaimedEngine.D5_120Kw,
            VolvoP2CemVendorPinTool.Vdd);
        Assert.Contains("ADVERTISES SOME P2 ONLY", late);
        Assert.Contains("caveat: YES", late);
        Assert.Contains("caveat: NOT SHOWN BY THIS SOURCE", early);
    }

    [Fact]
    public void UnspecifiedEngineAndVdashDoNotClaimCoverage()
    {
        var report = Report("2007", true,
            VolvoP2CemClaimedEngine.OtherFiveCylinder,
            VolvoP2CemVendorPinTool.Vdash);
        Assert.Contains("DOES NOT ESTABLISH THIS VARIANT", report);
        Assert.Contains("NOT VERIFIED", report);
    }

    [Fact]
    public void UnknownUncheckedAndOutOfRangeInputsAreNotReadiness()
    {
        var reports = new[]
        {
            Report(null, true, VolvoP2CemClaimedEngine.V8_44,
                VolvoP2CemVendorPinTool.Vdash),
            Report("2007", false, VolvoP2CemClaimedEngine.V8_44,
                VolvoP2CemVendorPinTool.Vdash),
            Report("2004", true, VolvoP2CemClaimedEngine.V8_44,
                VolvoP2CemVendorPinTool.Vdash),
            Report("2015", true, VolvoP2CemClaimedEngine.V8_44,
                VolvoP2CemVendorPinTool.Vdash),
            Report("2007", true, VolvoP2CemClaimedEngine.Unknown,
                VolvoP2CemVendorPinTool.Vdash)
        };
        Assert.All(reports, x => Assert.Contains("UNKNOWN / UNCONFIRMED", x));
    }

    [Fact]
    public void MalformedRawYearIsNeverReflectedInReport()
    {
        foreach (var raw in new[] { "VIN12345678901234", "1234;PIN=9999", " 2007",
                                    "２０07", "", "2OO7" })
        {
            var report = Report(raw, true, VolvoP2CemClaimedEngine.V8_44,
                VolvoP2CemVendorPinTool.Vdash);
            Assert.Contains("Claimed XC90 model year: UNKNOWN", report);
            if (!string.IsNullOrEmpty(raw))
                Assert.DoesNotContain(raw, report);
            Assert.Contains("UNKNOWN / UNCONFIRMED", report);
        }
    }

    [Fact]
    public void InvalidEnumsAndAllResearchClaimsNeverEnableCem()
    {
        foreach (var engine in Enum.GetValues<VolvoP2CemClaimedEngine>())
        foreach (var tool in Enum.GetValues<VolvoP2CemVendorPinTool>())
        {
            var report = Report("2007", true, engine, tool);
            Assert.Contains("PIN acquisition, key programming and ECU configuration: DISABLED",
                report);
            Assert.Contains("CEM hardware/software identity: NOT VERIFIED", report);
            Assert.Contains("Physical diagnostic transport: NOT VERIFIED", report);
            Assert.DoesNotContain("PIN: ", report);
        }
        Assert.Contains("UNKNOWN / UNCONFIRMED", Report("2007", true,
            (VolvoP2CemClaimedEngine)900,
            (VolvoP2CemVendorPinTool)900));
    }
}
