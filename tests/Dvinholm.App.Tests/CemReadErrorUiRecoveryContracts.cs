namespace Dvinholm.App.Tests;

/// <summary>
/// Static wiring checks only; a real WinUI message pump and physical J2534
/// receive-error event still require separate bench acceptance.
/// </summary>
public sealed class CemReadErrorUiRecoveryContracts
{
    private static string Source()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, "src", "Dvinholm.App",
                "MainWindow.xaml.cs");
            if (File.Exists(path)) return File.ReadAllText(path);
        }
        throw new FileNotFoundException("MainWindow.xaml.cs");
    }

    [Fact]
    public void ReadFaultInvalidatesDiagnosticGenerationBeforeRearmingControls()
    {
        var source = Source();
        var start = source.IndexOf("_session.ReadErrorEpoch += errorEpoch =>", StringComparison.Ordinal);
        Assert.True(start > 0);
        var stop = source.IndexOf("        };", start, StringComparison.Ordinal);
        Assert.True(stop > start);
        var handler = source[start..(stop + "        };".Length)];
        var invalidate = handler.IndexOf("ClearDiagnosticReadResult();", StringComparison.Ordinal);
        var clearIdentity = handler.IndexOf("ClearCemIdentity();", StringComparison.Ordinal);
        var refresh = handler.IndexOf("SetUdsEnabled(_connected && _session.IsConnected && !_closing);", StringComparison.Ordinal);
        Assert.True(invalidate > 0);
        Assert.True(clearIdentity > invalidate);
        Assert.True(refresh > clearIdentity);
        Assert.Contains("ClearCemPinMaterial();", handler);
        Assert.Contains("J2534ConnectionPresentation.ReadErrorActivity", handler);
        Assert.DoesNotContain("DiagnosticResultText.Text =", handler);
        Assert.DoesNotContain("ReadCemPin_Click(", handler);
        Assert.DoesNotContain("PassThru", handler);
    }

    [Fact]
    public void UiDoesNotTreatLogicalConnectFlagAsHealthyJ2534Transport()
    {
        var handler = Source();
        var start = handler.IndexOf("_session.ReadErrorEpoch += errorEpoch =>", StringComparison.Ordinal);
        var stop = handler.IndexOf("        };", start, StringComparison.Ordinal);
        Assert.True(start > 0 && stop > start);
        var block = handler[start..stop];
        Assert.Contains("SetUdsEnabled(_connected && _session.IsConnected && !_closing);", block);
        Assert.DoesNotContain("SetUdsEnabled(_connected);", block);
        Assert.DoesNotContain("SetUdsEnabled(true);", block);
    }
    [Fact]
    public void QueuedReadErrorCannotClearResultsFromReplacementConnection()
    {
        var source = Source();
        var start = source.IndexOf("_session.ReadErrorEpoch += errorEpoch =>", StringComparison.Ordinal);
        var stop = source.IndexOf("        };", start, StringComparison.Ordinal);
        Assert.True(start > 0 && stop > start);
        var handler = source[start..stop];
        var origin = handler.IndexOf("_session.ReadErrorEpoch += errorEpoch =>", StringComparison.Ordinal);
        var enqueue = handler.IndexOf("DispatcherQueue.TryEnqueue(", StringComparison.Ordinal);
        var check = handler.IndexOf("_session.ConnectionEpoch != errorEpoch", StringComparison.Ordinal);
        var clear = handler.IndexOf("ClearDiagnosticReadResult();", StringComparison.Ordinal);
        Assert.True(origin == 0 && enqueue > origin &&
            check > enqueue && clear > check);
        Assert.Contains("if (_closing || _session.ConnectionEpoch != errorEpoch)", handler);
        Assert.DoesNotContain("J2534ConnectionPresentation.ReadErrorActivity", handler[..check]);
    }

}
