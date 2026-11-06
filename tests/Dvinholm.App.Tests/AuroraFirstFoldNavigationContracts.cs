namespace Dvinholm.App.Tests;

/// <summary>
/// Offline source contracts for Aurora navigation first-fold stabilization.
/// They verify no navigation callback can become a stale scroll correction,
/// but do not replace real Windows rendering or UI Automation acceptance.
/// </summary>
public sealed class AuroraFirstFoldNavigationContracts
{
    private static string Source(string relative)
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory);
             directory is not null; directory = directory.Parent)
        {
            var full = Path.Combine(directory.FullName,
                relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(full)) return File.ReadAllText(full);
        }
        throw new FileNotFoundException(relative);
    }

    private static string Main() => Source("src/Dvinholm.App/MainWindow.xaml.cs");
    private static string Updates() => Source("src/Dvinholm.App/MainWindow.UnifiedUpdates.cs");

    [Fact]
    public void FirstFoldResetUsesBothImmediateAndDeferredLayoutChecks()
    {
        var main = Main();
        var start = main.IndexOf("private void ResetWorkspaceFirstFoldAfterNavigation()",
            StringComparison.Ordinal);
        var end = main.IndexOf("private void DashboardNavigate(", start,
            StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var section = main[start..end];
        var generation = section.IndexOf("++_workspaceFirstFoldGeneration",
            StringComparison.Ordinal);
        var first = section.IndexOf("WorkspaceScroll.ChangeView(null, 0, null);",
            generation, StringComparison.Ordinal);
        var deferred = section.IndexOf(
            "DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>",
            first, StringComparison.Ordinal);
        var guard = section.IndexOf("generation != _workspaceFirstFoldGeneration",
            deferred, StringComparison.Ordinal);
        var offset = section.IndexOf("WorkspaceScroll.VerticalOffset > 0.5",
            guard, StringComparison.Ordinal);
        var second = section.IndexOf("WorkspaceScroll.ChangeView(null, 0, null);",
            offset, StringComparison.Ordinal);
        Assert.True(generation >= 0 && first > generation && deferred > first &&
            guard > deferred && offset > guard && second > offset);
    }

    [Fact]
    public void DeferredScrollRejectsObsoleteRoutesClosedWindowsAndHiddenHost()
    {
        var main = Main();
        Assert.Contains("private int _workspaceFirstFoldGeneration;", main);
        Assert.Contains("var generation = ++_workspaceFirstFoldGeneration;", main);
        Assert.Contains("if (_closing || generation != _workspaceFirstFoldGeneration ||",
            main);
        Assert.Contains("AuroraWorkspaceHost.Visibility != Visibility.Visible", main);
        Assert.Contains("if (WorkspaceScroll.VerticalOffset > 0.5)", main);
        Assert.DoesNotContain("Task.Delay(", main);
    }

    [Fact]
    public void NormalNavigationResetsFirstFoldAfterUpdatingFocusAndDestination()
    {
        var main = Main();
        var start = main.IndexOf("private void DashboardNavigate(", StringComparison.Ordinal);
        var end = main.IndexOf("private void WorkspaceNavigate_Click(", start,
            StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var section = main[start..end];
        var page = section.IndexOf("_workspaceDestination = destination;",
            StringComparison.Ordinal);
        var presentation = section.IndexOf("ApplyUnifiedWorkspaceSection(destination);",
            page, StringComparison.Ordinal);
        var focus = section.IndexOf("EcuSelector.Focus(FocusState.Programmatic)",
            presentation, StringComparison.Ordinal);
        var reset = section.IndexOf("ResetWorkspaceFirstFoldAfterNavigation();",
            focus, StringComparison.Ordinal);
        Assert.True(page >= 0 && presentation > page && focus > presentation &&
            reset > focus);
        Assert.DoesNotContain("PassThruOpen(", section);
        Assert.DoesNotContain("Connect_Click(", section);
    }

    [Fact]
    public void UpdatesNavigationSharesResetAfterFocusAndDoesNotStartInstall()
    {
        var updates = Updates();
        var start = updates.IndexOf("private void UnifiedUpdatesNavigate_Click(",
            StringComparison.Ordinal);
        Assert.True(start >= 0);
        var section = updates[start..];
        var focus = section.IndexOf("UnifiedUpdateChannel.Focus(FocusState.Programmatic)",
            StringComparison.Ordinal);
        var alternate = section.IndexOf("SidebarUpdatesButton.Focus(FocusState.Programmatic)",
            focus, StringComparison.Ordinal);
        var reset = section.IndexOf("ResetWorkspaceFirstFoldAfterNavigation();",
            alternate, StringComparison.Ordinal);
        Assert.True(focus >= 0 && alternate > focus && reset > alternate);
        Assert.DoesNotContain("UnifiedStageUpdate_Click(", section);
        Assert.DoesNotContain("UnifiedPreflight_Click(", section);
        Assert.DoesNotContain("PassThruOpen(", section);
    }

    [Fact]
    public void FirstFoldResetNeverInjectsVehicleRequestsOrUserFocus()
    {
        var main = Main();
        var start = main.IndexOf("private void ResetWorkspaceFirstFoldAfterNavigation()",
            StringComparison.Ordinal);
        var end = main.IndexOf("private void DashboardNavigate(", start,
            StringComparison.Ordinal);
        var section = main[start..end];
        foreach (var prohibited in new[] {
            "Focus(", "PassThru", "Connect_Click(", "ReadDtc_Click(",
            "ReadCemPin", "UnifiedStageUpdate_Click(", "Start-BitsTransfer",
            "Invoke-WebRequest" })
        {
            Assert.DoesNotContain(prohibited, section);
        }
        Assert.Contains("WorkspaceScroll.ChangeView(null, 0, null);", section);
    }
}
