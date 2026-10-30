namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline contracts for private ECU profile metadata in shared Aurora
/// activity. No real profile file, vehicle, adapter, VirtualCar or CAN request.
/// </summary>
public sealed class EcuProfileActivityPrivacyTests
{
    private const string SensitiveProfile = "CEM YV1ABCDEF12345678";
    private const string SensitiveFile = @"C:\garage\YV1ABCDEF12345678\ecu-profiles.json";

    [Fact]
    public void SavedProfileActivityDoesNotNeedUserMetadata()
    {
        var activity = EcuProfileActivity.Message(EcuProfileActivityKind.SavedUnverified);
        Assert.Contains("saved as unverified", activity);
        Assert.Contains("No diagnostic request was sent", activity);
        Assert.DoesNotContain(SensitiveProfile, activity);
    }

    [Fact]
    public void ApplyActivityIsSafeButWarnsCanIdsAreNotVerified()
    {
        var activity = EcuProfileActivity.Message(EcuProfileActivityKind.Applied);
        Assert.Contains("Check its source and CAN IDs", activity);
        Assert.Contains("No diagnostic request was sent", activity);
        Assert.DoesNotContain("0x7E0", activity);
        Assert.DoesNotContain(SensitiveProfile, activity);
    }

    [Fact]
    public void ImportActivityShowsOnlySafeNumericCount()
    {
        var activity = EcuProfileActivity.Message(EcuProfileActivityKind.Imported, 12);
        Assert.Contains("Imported and saved 12 ECU profile(s)", activity);
        Assert.DoesNotContain(SensitiveFile, activity);
        Assert.DoesNotContain("ecu-profiles.json", activity);
        Assert.DoesNotContain(SensitiveProfile, activity);
    }

    [Fact]
    public void ExportActivityDoesNotEchoSelectedFilename()
    {
        var activity = EcuProfileActivity.Message(EcuProfileActivityKind.Exported, 5);
        Assert.Contains("Exported 5 ECU profile(s) to the selected file", activity);
        Assert.Contains("Review before sharing", activity);
        Assert.DoesNotContain(SensitiveFile, activity);
        Assert.DoesNotContain(".json", activity);
    }

    [Fact]
    public void ExportBlockedActivityIsConstantAndContainsNoException()
    {
        var activity = EcuProfileActivity.Message(EcuProfileActivityKind.ExportBlocked);
        Assert.Contains("export blocked", activity);
        Assert.Contains("selected destination", activity);
        Assert.DoesNotContain(SensitiveFile, activity);
        Assert.DoesNotContain("System.", activity);
    }

    [Fact]
    public void NegativeCountsCannotBeRepresentedAsSuccessfulActions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EcuProfileActivity.Message(EcuProfileActivityKind.Imported, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EcuProfileActivity.Message(EcuProfileActivityKind.Exported, -7));
    }

    [Fact]
    public void UnknownActivityKindFailsClosed()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EcuProfileActivity.Message((EcuProfileActivityKind)999));
    }

    [Fact]
    public void RealUiUsesPayloadFreeStatusWhileRetainingDedicatedProfileDetails()
    {
        var source = MainWindowSource();
        Assert.Contains("SetStatus(EcuProfileActivity.Message(EcuProfileActivityKind.SavedUnverified));", source);
        Assert.Contains("SetStatus(EcuProfileActivity.Message(EcuProfileActivityKind.Applied));", source);
        Assert.Contains("SetStatus(EcuProfileActivity.Message(EcuProfileActivityKind.Imported, imported.All.Count));", source);
        Assert.Contains("SetStatus(EcuProfileActivity.Message(EcuProfileActivityKind.Exported, _profileCatalog.All.Count));", source);
        Assert.Contains("SetStatus(EcuProfileActivity.Message(EcuProfileActivityKind.ExportBlocked));", source);

        Assert.DoesNotContain("SetStatus($\"Applied profile", source);
        Assert.DoesNotContain("SetStatus($\"Saved ECU profile", source);
        Assert.DoesNotContain("from {file.Name}", source);
        Assert.DoesNotContain("to {Path.GetFileName(destinationPath)}.", source);
        var exportStart = source.IndexOf("private async void ExportProfiles_Click(", StringComparison.Ordinal);
        var exportEnd = source.IndexOf("private async void P2PinReview_Click(", exportStart, StringComparison.Ordinal);
        Assert.True(exportStart >= 0 && exportEnd > exportStart);
        Assert.DoesNotContain("SetStatus(ex.Message);", source[exportStart..exportEnd]);

        // User-controlled names and source evidence remain visible only in
        // dedicated profile controls, rather than the global activity text.
        Assert.Contains("EcuInfoText.Text = EcuProfileCatalogSearch.Describe(profile);", source);
        Assert.Contains("ProfileSelector.SelectedItem = selected;", source);
        Assert.Contains("UdsRequestId.Text = profile.RequestId.ToString(", source);
        Assert.Contains("UdsResponseId.Text = profile.ResponseId.ToString(", source);
    }

    private static string MainWindowSource()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "src", "Dvinholm.App",
                "MainWindow.xaml.cs");
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);
        }

        throw new FileNotFoundException("src/Dvinholm.App/MainWindow.xaml.cs");
    }
}
