namespace Dvinholm.App.Tests;

/// <summary>Eight synthetic offline checks, no vehicle or PIN operation.</summary>
public sealed class VolvoP2CemVendorPinRouteResearchTests
{
    private static VolvoP2CemVendorPinAssessment Review(
        int? year, bool checkedYear, VolvoP2CemClaimedEngine engine,
        VolvoP2CemVendorPinTool tool) =>
        VolvoP2CemVendorPinRouteResearch.Assess(year, checkedYear, engine, tool);

    [Fact]
    public void V8VdashDecodingOfferHasSeparateKeyWarning()
    {
        var r = Review(2007, true, VolvoP2CemClaimedEngine.V8_44,
            VolvoP2CemVendorPinTool.Vdash);
        Assert.Equal(VolvoP2CemVendorPinClaimKind.AdvertisedWithSeparateKeyProgrammingCaveat,
            r.Kind);
        Assert.True(r.SeparateKeyProgrammingCaveat);
        Assert.Contains("NOT verified key programming", r.Explanation);
        Assert.False(r.MayDecodePin);
    }

    [Fact]
    public void V8VddDecodingExcluded()
    {
        var r = Review(2007, true, VolvoP2CemClaimedEngine.V8_44,
            VolvoP2CemVendorPinTool.Vdd);
        Assert.Equal(VolvoP2CemVendorPinClaimKind.ExcludedByVendorForThisEngine, r.Kind);
        Assert.False(r.MayConnectToCem);
    }

    [Fact]
    public void SixCylinder32HasRouteSplit()
    {
        Assert.Equal(VolvoP2CemVendorPinClaimKind.AdvertisedWithSeparateKeyProgrammingCaveat,
            Review(2008, true, VolvoP2CemClaimedEngine.InlineSix32,
                VolvoP2CemVendorPinTool.Vdash).Kind);
        Assert.Equal(VolvoP2CemVendorPinClaimKind.ExcludedByVendorForThisEngine,
            Review(2008, true, VolvoP2CemClaimedEngine.InlineSix32,
                VolvoP2CemVendorPinTool.Vdd).Kind);
    }

    [Fact]
    public void D5_147KwHasRouteSplit()
    {
        Assert.Equal(VolvoP2CemVendorPinClaimKind.AdvertisedWithSeparateKeyProgrammingCaveat,
            Review(2009, true, VolvoP2CemClaimedEngine.D5_147Kw,
                VolvoP2CemVendorPinTool.Vdash).Kind);
        Assert.Equal(VolvoP2CemVendorPinClaimKind.ExcludedByVendorForThisEngine,
            Review(2009, true, VolvoP2CemClaimedEngine.D5_147Kw,
                VolvoP2CemVendorPinTool.Vdd).Kind);
    }

    [Fact]
    public void OtherFiveCylinderVddIsNotGuaranteed()
    {
        var r = Review(2006, true, VolvoP2CemClaimedEngine.OtherFiveCylinder,
            VolvoP2CemVendorPinTool.Vdd);
        Assert.Equal(VolvoP2CemVendorPinClaimKind.AdvertisedForSomeP2Only, r.Kind);
        Assert.False(r.SeparateKeyProgrammingCaveat);
        Assert.Contains("does not guarantee success", r.Explanation);
    }

    [Fact]
    public void D5_120KwLateKeyCaveatDoesNotInventVddDecodeExclusion()
    {
        var later = Review(2013, true, VolvoP2CemClaimedEngine.D5_120Kw,
            VolvoP2CemVendorPinTool.Vdd);
        Assert.Equal(VolvoP2CemVendorPinClaimKind.AdvertisedForSomeP2Only, later.Kind);
        Assert.True(later.SeparateKeyProgrammingCaveat);
        var earlier = Review(2012, true, VolvoP2CemClaimedEngine.D5_120Kw,
            VolvoP2CemVendorPinTool.Vdd);
        Assert.False(earlier.SeparateKeyProgrammingCaveat);
    }

    [Fact]
    public void UnknownUnconfirmedOutOfScopeOrInvalidInputFailsClosed()
    {
        var cases = new[]
        {
            Review(null, true, VolvoP2CemClaimedEngine.V8_44, VolvoP2CemVendorPinTool.Vdash),
            Review(2007, false, VolvoP2CemClaimedEngine.V8_44, VolvoP2CemVendorPinTool.Vdash),
            Review(2003, true, VolvoP2CemClaimedEngine.V8_44, VolvoP2CemVendorPinTool.Vdash),
            Review(2015, true, VolvoP2CemClaimedEngine.V8_44, VolvoP2CemVendorPinTool.Vdash),
            Review(2007, true, VolvoP2CemClaimedEngine.Unknown, VolvoP2CemVendorPinTool.Vdash),
            Review(2007, true, (VolvoP2CemClaimedEngine)999, VolvoP2CemVendorPinTool.Vdash),
            Review(2007, true, VolvoP2CemClaimedEngine.V8_44, (VolvoP2CemVendorPinTool)999)
        };
        Assert.All(cases, r => Assert.Equal(
            VolvoP2CemVendorPinClaimKind.UnconfirmedYearOrEngine, r.Kind));
    }

    [Fact]
    public void AllVendorClaimsAlwaysDenyEveryLiveOperation()
    {
        foreach (var engine in Enum.GetValues<VolvoP2CemClaimedEngine>())
        foreach (var tool in Enum.GetValues<VolvoP2CemVendorPinTool>())
        {
            var r = Review(2007, true, engine, tool);
            Assert.False(r.HasVerifiedCemRevision);
            Assert.False(r.HasVerifiedDiagnosticTransport);
            Assert.False(r.MayConnectToCem);
            Assert.False(r.MayDecodePin);
            Assert.False(r.MayProgramKeys);
            Assert.False(r.MayWriteConfiguration);
        }
        var report = VolvoP2CemVendorPinRouteResearch.BuildOfflineOverview();
        Assert.Contains("OFFLINE ONLY", report);
        Assert.Contains("DIFFERENT operations", report);
        Assert.DoesNotContain("PIN:", report);
    }
}
