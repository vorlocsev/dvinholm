namespace Dvinholm.App.Tests;

/// <summary>
/// Eight source-scoped synthetic/offline checks. No CEM identity, actual
/// parameter bytes, J2534 driver, VIN, PIN or ECU programming is used.
/// </summary>
public sealed class VolvoP2CemHistoricalCustomerValuePreflightTests
{
    private static VolvoP2CemCustomerValueReview Check(
        int? year, bool confirmed, VolvoP2CemCustomerParameterKind parameter,
        string? desired, string? second = null) =>
        VolvoP2CemHistoricalCustomerValuePreflight.Assess(
            year, confirmed, parameter, desired, second);

    [Fact]
    public void SourceValueSetsAreExactImmutableHistoricalMetadata()
    {
        var source = VolvoP2CemHistoricalCustomerValuePreflight;
        Assert.Equal(new[] { 0, 10, 28, 30, 31, 32, 33, 34, 35, 36,
                             37, 38, 39, 40, 41, 43 },
            source.SeatTemperatureCelsius);
        Assert.Equal(new[] { 0, 30, 60, 90 }, source.ApproachLightSeconds);
        Assert.Equal(new[] { "OFF", "ON" }, source.SwitchValues);
        Assert.Equal(new[] { "FLEX_ZERO", "FLEX_STANDARD", "FLEX_LOW" },
            source.DaytimeLampModes);
        var list = Assert.IsAssignableFrom<IList<int>>(source.SeatTemperatureCelsius);
        Assert.True(list.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => list.Add(42));
        Assert.Contains("reading-off-and-programming-data",
            source.Source.Replace("reading-off-and-programming-data/", "reading-off-and-programming-data"));
    }

    [Fact]
    public void UnconfirmedYearAndOutOfScopeYearsNeverUseSourceRanges()
    {
        foreach (var year in new int?[] { null, 2003, 2005, 2006 })
            Assert.Equal(VolvoP2CemCustomerValueReviewKind.VehicleYearUnconfirmed,
                Check(year, false, VolvoP2CemCustomerParameterKind.ApproachLightsSeconds, "30").Kind);
        foreach (var year in new int?[] { 2002, 2007, 2014, 2099 })
            Assert.Equal(VolvoP2CemCustomerValueReviewKind.OutsideHistoricalSourceScope,
                Check(year, true, VolvoP2CemCustomerParameterKind.ApproachLightsSeconds, "30").Kind);
    }

    [Fact]
    public void SeatHeatingPermitsOnlySourceListedLowHighWithHighGreaterThanLow()
    {
        foreach (var year in new[] { 2003, 2004, 2005, 2006 })
            Assert.Equal(VolvoP2CemCustomerValueReviewKind.ListedInHistoricalSourceOnly,
                Check(year, true, VolvoP2CemCustomerParameterKind.SeatHeaterLowHighCelsius,
                    "37", "41").Kind);
        Assert.Equal(VolvoP2CemCustomerValueReviewKind.ListedInHistoricalSourceOnly,
            Check(2004, true, VolvoP2CemCustomerParameterKind.SeatHeaterLowHighCelsius,
                "0", "10").Kind);
    }

    [Fact]
    public void SeatHeatingRejectsEqualReversedUnavailableOrPartialLevels()
    {
        foreach (var pair in new[] { ("37", "37"), ("41", "37"), ("27", "43"),
                                      ("37", "42"), ("-1", "43"), ("037", "43") })
            Assert.Equal(VolvoP2CemCustomerValueReviewKind.NotListedOrInvalidRelationship,
                Check(2005, true, VolvoP2CemCustomerParameterKind.SeatHeaterLowHighCelsius,
                    pair.Item1, pair.Item2).Kind);
        Assert.Equal(VolvoP2CemCustomerValueReviewKind.MissingDesiredValue,
            Check(2005, true, VolvoP2CemCustomerParameterKind.SeatHeaterLowHighCelsius,
                "37").Kind);
    }

    [Fact]
    public void ApproachLightingAcceptsOnlyDocumentedDurations()
    {
        foreach (var seconds in new[] { "0", "30", "60", "90" })
            Assert.Equal(VolvoP2CemCustomerValueReviewKind.ListedInHistoricalSourceOnly,
                Check(2006, true, VolvoP2CemCustomerParameterKind.ApproachLightsSeconds,
                    seconds).Kind);
        foreach (var seconds in new[] { "15", "45", "120", "090", "-30", "30s" })
            Assert.Equal(VolvoP2CemCustomerValueReviewKind.NotListedOrInvalidRelationship,
                Check(2006, true, VolvoP2CemCustomerParameterKind.ApproachLightsSeconds,
                    seconds).Kind);
    }

    [Fact]
    public void PostDryingIsExactOnOffAndNoSecondValue()
    {
        foreach (var value in new[] { "ON", "OFF" })
            Assert.Equal(VolvoP2CemCustomerValueReviewKind.ListedInHistoricalSourceOnly,
                Check(2005, true, VolvoP2CemCustomerParameterKind.PostDrying,
                    value).Kind);
        foreach (var value in new[] { "on", "1", "TRUE", "AUTO" })
            Assert.Equal(VolvoP2CemCustomerValueReviewKind.NotListedOrInvalidRelationship,
                Check(2005, true, VolvoP2CemCustomerParameterKind.PostDrying,
                    value).Kind);
        Assert.Equal(VolvoP2CemCustomerValueReviewKind.NotListedOrInvalidRelationship,
            Check(2005, true, VolvoP2CemCustomerParameterKind.PostDrying,
                "ON", "OFF").Kind);
    }

    [Fact]
    public void DaytimeRunningLightModesRequireExactHistoricalNamesAndLawReview()
    {
        foreach (var mode in new[] { "FLEX_ZERO", "FLEX_STANDARD", "FLEX_LOW" })
            Assert.Equal(VolvoP2CemCustomerValueReviewKind.ListedInHistoricalSourceOnly,
                Check(2005, true, VolvoP2CemCustomerParameterKind.DaytimeRunningLamps,
                    mode).Kind);
        Assert.Equal(VolvoP2CemCustomerValueReviewKind.NotListedOrInvalidRelationship,
            Check(2005, true, VolvoP2CemCustomerParameterKind.DaytimeRunningLamps,
                "FLEX_UNKNOWN").Kind);
        Assert.Contains("Country-specific road lighting rules",
            VolvoP2CemHistoricalCustomerValuePreflight.BuildOfflineOverview());
    }

    [Fact]
    public void NoReviewOrOverviewCanAuthorizeRealEcuOrPinOperation()
    {
        var all = new[]
        {
            Check(2005, true, VolvoP2CemCustomerParameterKind.ApproachLightsSeconds, "30"),
            Check(2008, true, VolvoP2CemCustomerParameterKind.ApproachLightsSeconds, "30"),
            Check(null, false, VolvoP2CemCustomerParameterKind.PostDrying, "ON"),
            Check(2005, true, VolvoP2CemCustomerParameterKind.ApproachLightsSeconds, "45"),
            Check(2005, true, (VolvoP2CemCustomerParameterKind)999, "30"),
            Check(2005, true, VolvoP2CemCustomerParameterKind.PostDrying, null)
        };
        foreach (var review in all)
        {
            Assert.False(review.HasConfirmedCurrentVehicleValue);
            Assert.False(review.HasAuthenticatedCemFirmwareMapping);
            Assert.False(review.MayReadLiveConfiguration);
            Assert.False(review.MaySendJ2534OrUds);
            Assert.False(review.MayWriteConfiguration);
            Assert.False(review.MayAccessPinOrSecurity);
        }
        var overview = VolvoP2CemHistoricalCustomerValuePreflight.BuildOfflineOverview();
        Assert.Contains("CUSTOMER PARAMETERS are distinct", overview);
        Assert.Contains("No PIN, live config read", overview);
        Assert.DoesNotContain("PIN found", overview);
    }
}
