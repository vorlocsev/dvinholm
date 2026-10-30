namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline checks for profile switches during read-only UDS and DID
/// snapshot operations. Never opens J2534, VirtualCar or a real ECU.
/// </summary>
public sealed class EcuProfileInflightSelectionTests
{
    [Fact]
    public void RapidAtoBtoAInvalidatesEarlierResponseEvenWithSameCanAddresses()
    {
        var lifetime = new UdsReadResultLifetime();
        var fromA = lifetime.Invalidate();
        lifetime.Invalidate(); // Switch to B, even if both profiles use 7E0/7E8.
        lifetime.Invalidate(); // Switch back to A before queued UI fields apply.

        Assert.False(lifetime.CanDisplay(fromA));
        Assert.True(lifetime.CanDisplay(lifetime.Generation));
    }

    [Fact]
    public void SelectionInvalidationPrecedesDeferredFieldApply()
    {
        var source = MainSource();
        var region = Between(source, "private void ProfileSelector_SelectionChanged(",
            "private void ApplySelectedProfile(");
        var invalidate = region.IndexOf("InvalidateProfileContext();", StringComparison.Ordinal);
        var enqueue = region.IndexOf("DispatcherQueue.TryEnqueue(", StringComparison.Ordinal);
        Assert.True(invalidate >= 0 && enqueue > invalidate);
        Assert.Contains("if (_loadingProfiles)", region);
    }

    [Fact]
    public void InvalidationStopsRemainingReadWorkWithoutClaimingCanTxRollback()
    {
        var helper = Between(MainSource(), "private void InvalidateProfileContext()",
            "private void ProfileSelector_SelectionChanged(");
        var clear = helper.IndexOf("ClearDiagnosticReadResult();", StringComparison.Ordinal);
        var cancel = helper.IndexOf("_udsCts?.Cancel();", StringComparison.Ordinal);
        var refresh = helper.IndexOf("RefreshSnapshotCaptureAvailability();", StringComparison.Ordinal);
        Assert.True(clear >= 0 && clear < cancel && cancel < refresh);
        Assert.DoesNotContain("IsoTpChannel(", helper);
        Assert.DoesNotContain("_session.Bus", helper);
    }

    [Fact]
    public void EarlierQueuedSelectionApplyIsRejectedAfterSecondSelection()
    {
        var region = Between(MainSource(), "private void ProfileSelector_SelectionChanged(",
            "private void ApplySelectedProfile(");
        var bump = region.IndexOf("_profileFilterGeneration++;", StringComparison.Ordinal);
        var capture = region.IndexOf("var generation = _profileFilterGeneration;", StringComparison.Ordinal);
        var guard = region.IndexOf("generation != _profileFilterGeneration", StringComparison.Ordinal);
        var apply = region.IndexOf("ApplySelectedProfile(ProfileSelector.SelectedItem as EcuProfile);",
            StringComparison.Ordinal);
        Assert.True(bump >= 0 && bump < capture && capture < guard && guard < apply);
    }

    [Fact]
    public void FilteringOutSelectedProfileInvalidatesEvenWithoutTextChanged()
    {
        var source = MainSource();
        var region = Between(source, "private void RefreshProfileFilter()",
            "private void LoadSavedProfileCatalog()");
        var hide = region.IndexOf("if (previousId is not null && retained is null)", StringComparison.Ordinal);
        var invalidate = region.IndexOf("InvalidateProfileContext();", hide, StringComparison.Ordinal);
        var clear = region.IndexOf("ClearProfileInputs();", hide, StringComparison.Ordinal);
        Assert.True(hide >= 0 && hide < invalidate && invalidate < clear);
        Assert.Contains("_loadingProfiles = true", region);
        Assert.Contains("ProfileSelector.SelectedItem = retained;", region);
    }

    [Fact]
    public void CatalogReplacementInvalidatesBeforeReplacingSelection()
    {
        var region = Between(MainSource(), "private void SetProfileCatalog(",
            "private void ProfileSearch_TextChanged(");
        var invalidate = region.IndexOf("InvalidateProfileContext();", StringComparison.Ordinal);
        var assign = region.IndexOf("_profileCatalog = catalog;", StringComparison.Ordinal);
        var clear = region.IndexOf("ClearProfileInputs();", StringComparison.Ordinal);
        Assert.True(invalidate >= 0 && invalidate < assign && assign < clear);
        Assert.Contains("_profileFilterGeneration++;", region);
    }

    [Fact]
    public void ReapplyingAnUnchangedAddressPairStillRefreshesReadButtons()
    {
        var region = Between(MainSource(), "private void ApplySelectedProfile(",
            "private static string CanBaudRateSettingsFile");
        Assert.Contains("UdsRequestId.Text = profile.RequestId.ToString(", region);
        Assert.Contains("UdsResponseId.Text = profile.ResponseId.ToString(", region);
        Assert.Contains("SetUdsEnabled(_connected && !_closing);", region);
        Assert.DoesNotContain("RefreshSnapshotCaptureAvailability();", region);
        Assert.DoesNotContain("_session.", region);
    }

    [Fact]
    public void OldSnapshotGenerationFailsEvenIfUserReturnsToSameProfile()
    {
        var profile = new EcuProfile("offline.same", "Offline test", 0x7E0, 0x7E8,
            false, new ushort[] { 0xF190 }, "Offline test data");
        var lifetime = new UdsReadResultLifetime();
        var first = lifetime.Invalidate();
        lifetime.Invalidate(); // Profile selection changed.
        lifetime.Invalidate(); // Returned to same object, same addresses.
        Assert.False(EcuSnapshotOperationContext.IsCurrent(
            profile, profile, first, lifetime.Generation, addressAndSessionStillValid: true));
        Assert.True(EcuSnapshotOperationContext.IsCurrent(
            profile, profile, lifetime.Generation, lifetime.Generation,
            addressAndSessionStillValid: true));
    }

    private static string Between(string source, string first, string next)
    {
        var start = source.IndexOf(first, StringComparison.Ordinal);
        Assert.True(start >= 0, "Missing " + first);
        var end = source.IndexOf(next, start + first.Length, StringComparison.Ordinal);
        Assert.True(end > start, "Missing " + next);
        return source[start..end];
    }

    private static string MainSource()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, "src", "Dvinholm.App", "MainWindow.xaml.cs");
            if (File.Exists(path))
                return File.ReadAllText(path);
        }

        throw new FileNotFoundException("src/Dvinholm.App/MainWindow.xaml.cs");
    }
}
