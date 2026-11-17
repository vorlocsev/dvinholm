namespace Dvinholm.App.Tests;

/// <summary>
/// Static WinUI event wiring contracts plus disposal semantics already covered
/// by CemPinSecretTests. Does not load an actual WinUI Window or expose a PIN.
/// </summary>
public sealed class CemPinWindowDeactivationSafetyContracts
{
    private static string Source(string relative)
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory);
             directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName,
                relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path)) return File.ReadAllText(path);
        }
        throw new FileNotFoundException(relative);
    }

    [Fact]
    public void WinUiDeactivationEventIsWiredOnlyForDevelopment()
    {
        var main = Source("src/Dvinholm.App/MainWindow.xaml.cs");
        var start = main.IndexOf(
            "Activated += (_, args) => OnCemPinWindowActivationChanged(", StringComparison.Ordinal);
        Assert.True(start > 0);
        var region = main[start..Math.Min(main.Length, start + 210)];
        Assert.Contains("WindowActivationState == WindowActivationState.Deactivated", region);
        Assert.Contains("#if DVINHOLM_DEVELOPMENT_FEATURES", main[
            Math.Max(0, start - 360)..start]);
        Assert.DoesNotContain("PassThru", region);
        Assert.DoesNotContain("Send(", region);
    }

    [Fact]
    public void DeactivationClearsRevealedMaterialButNotUnopenedHiddenResult()
    {
        var pin = Source("src/Dvinholm.App/MainWindow.CemPin.cs");
        var start = pin.IndexOf("private void OnCemPinWindowActivationChanged(",
            StringComparison.Ordinal);
        var end = pin.IndexOf("private void ClearCemPinMaterial()", start,
            StringComparison.Ordinal);
        Assert.True(start > 0 && end > start);
        var method = pin[start..end];
        Assert.Contains("if (!windowDeactivated || _cemPinShownAt == 0)", method);
        Assert.Contains("ClearCemPinMaterial();", method);
        Assert.DoesNotContain("CemPinResultText.Text =", method);
        Assert.DoesNotContain("PassThru", method);
        Assert.DoesNotContain("OpenCemPinStore", method);
        Assert.DoesNotContain("ReadCemPin_Click", method);
    }

    [Fact]
    public void ClearWipesSensitiveResultAndDoesNotLeaveRevealEnabled()
    {
        var pin = Source("src/Dvinholm.App/MainWindow.CemPin.cs");
        var start = pin.IndexOf("private void ClearCemPinMaterial()", StringComparison.Ordinal);
        var end = pin.IndexOf("private bool TryGetCemPinRange(", start, StringComparison.Ordinal);
        Assert.True(start > 0 && end > start);
        var method = pin[start..end];
        foreach (var required in new[] {
            "_cemPinViewTimer?.Stop();", "_cemPinShownAt = 0;",
            "_cemPinResult?.Dispose();", "_cemPinResult = null;",
            "_cemPinResultGuard?.Dispose();", "_cemPinLoadedFromStore = false;",
            "CemPinResultText.Text = CemText(", "CemPinRevealButton.IsEnabled = false;"
        }) Assert.Contains(required, method);
        Assert.DoesNotContain("Convert.ToHexString(", method);
    }

    [Fact]
    public void LeavingCemWorkspaceAlreadyClearsDiagnosticAndPinResult()
    {
        var main = Source("src/Dvinholm.App/MainWindow.xaml.cs");
        var begin = main.IndexOf("private void DashboardNavigate(", StringComparison.Ordinal);
        var end = main.IndexOf("private void WorkspaceNavigate_Click(", begin,
            StringComparison.Ordinal);
        Assert.True(begin > 0 && end > begin);
        Assert.Contains("_udsCts?.Cancel();", main[begin..end]);
        Assert.Contains("ClearDiagnosticReadResult();", main[begin..end]);
        var clearing = main.IndexOf("private long ClearDiagnosticReadResult(",
            StringComparison.Ordinal);
        var next = main.IndexOf("private void ClearDiagnosticResult_Click(", clearing,
            StringComparison.Ordinal);
        Assert.True(clearing > 0 && next > clearing);
        Assert.Contains("ClearCemPinMaterial();", main[clearing..next]);
    }

    [Fact]
    public void EncryptedPinSaveDoesNotBlockWinUiDispatcherAndRechecksLease()
    {
        var pin = Source("src/Dvinholm.App/MainWindow.CemPin.cs");
        var start = pin.IndexOf("private async void ReadCemPin_Click(", StringComparison.Ordinal);
        var end = pin.IndexOf("#else", start, StringComparison.Ordinal);
        Assert.True(start > 0 && end > start);
        var method = pin[start..end];
        var offloadedSave = method.IndexOf("await Task.Run(() => store.Save(candidate, partNumber, cts.Token));",
            StringComparison.Ordinal);
        var lateLease = method.IndexOf("cts.IsCancellationRequested || guard.IsCurrent != true || !result.HasPin",
            StringComparison.Ordinal);
        var rollback = method.IndexOf("await Task.Run(() => rollbackStore.Delete(abandonedId));",
            StringComparison.Ordinal);
        var publish = method.IndexOf("_cemPinResult = result;", StringComparison.Ordinal);
        var cleanup = method.IndexOf("_udsGate.Release();", StringComparison.Ordinal);
        Assert.True(offloadedSave > 0);
        Assert.True(lateLease > offloadedSave);
        Assert.True(rollback > lateLease);
        Assert.True(publish > rollback);
        Assert.True(cleanup > publish);
        Assert.Contains("savedEntry = await Task.Run(() => store.Save(candidate, partNumber, cts.Token));", method);
        Assert.Contains("if (savedEntry is not null && store is not null)", method);
        Assert.Contains("_udsResultLifetime.CanDisplay(generation)", method[offloadedSave..publish]);
        Assert.DoesNotContain("OpenCemPinStore().Save(result, identity.PartNumber)", method);
    }

    [Fact]
    public void SavedPinLoadAndDeleteAreOffDispatcherAndRevalidateGeneration()
    {
        var saved = Source("src/Dvinholm.App/MainWindow.CemPinSaved.cs");
        Assert.Contains("var inventory = await Task.Run(store.List);", saved);
        var deletion = saved.IndexOf("await Task.Run(() => store.Delete(selected.Id));",
            StringComparison.Ordinal);
        var deleteCheck = saved.IndexOf(
            "if (_closing || !_udsResultLifetime.CanDisplay(generation)) return;",
            deletion, StringComparison.Ordinal);
        var loaded = saved.IndexOf("loaded = await Task.Run(() => store.Load(selected.Id));",
            StringComparison.Ordinal);
        var loadCheck = saved.IndexOf(
            "if (_closing || !_udsResultLifetime.CanDisplay(generation)) return;",
            loaded, StringComparison.Ordinal);
        var publish = saved.IndexOf("_cemPinResult = CemPinAttemptResult.Restored(loaded);",
            StringComparison.Ordinal);
        Assert.True(deletion > 0 && deleteCheck > deletion);
        Assert.True(loaded > deleteCheck && loadCheck > loaded && publish > loadCheck);
        Assert.Contains("loaded?.Dispose();", saved);
        Assert.Contains("_udsGate.Release();", saved);
        Assert.DoesNotContain("store.Delete(selected.Id);\n                SetStatus", saved);
    }

    [Fact]
    public void RevealRemainsExplicitAndLimitedToFifteenSeconds()
    {
        var pin = Source("src/Dvinholm.App/MainWindow.CemPin.cs");
        Assert.Contains("private void RevealCemPin_Click(", pin);
        Assert.Contains("_cemPinShownAt = Stopwatch.GetTimestamp();", pin);
        Assert.Contains("TimeSpan.FromSeconds(15)", pin);
        Assert.Contains("ClearCemPinMaterial();", pin);
        Assert.DoesNotContain("Clipboard.SetContent", pin);
        Assert.DoesNotContain("ApplicationData.Current", pin);
    }
}
