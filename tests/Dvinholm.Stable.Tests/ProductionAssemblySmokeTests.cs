using System.Reflection;
using System.Xml.Linq;
using Dvinholm.App;
using Xunit;

namespace Dvinholm.Stable.Tests;

/// <summary>
/// Independent, headless Windows x86 verification compiled against the actual
/// Release app with DvinholmDevelopmentFeatures=false. Never starts WinUI or
/// contacts a J2534 device. These eight checks are intentionally NOT part
/// of the Development test project.
/// </summary>
public sealed class ProductionAssemblySmokeTests
{
    private static Assembly App => typeof(MainWindow).Assembly;
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;

    private static string MainXaml()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory);
             directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, "src", "Dvinholm.App", "MainWindow.xaml");
            if (File.Exists(path))
                return path;
        }
        throw new FileNotFoundException("MainWindow.xaml not found in checkout");
    }

    [Fact]
    public void NoSyntheticCanGeneratorType()
    {
        Assert.Null(App.GetType("Dvinholm.App.CanDemoFrameGenerator"));
        Assert.Null(App.GetType("Dvinholm.App.CemPinStore"));
        Assert.Null(App.GetType("Dvinholm.App.CemPinWindowsProtection"));
    }

    [Fact]
    public void NoOfflineUdsSampleType() =>
        Assert.Null(App.GetType("Dvinholm.App.UdsOfflineSample"));

    [Fact]
    public void NoOfflineTxtReportType() =>
        Assert.Null(App.GetType("Dvinholm.App.UdsOfflineTextReport"));

    [Fact]
    public void NoDemoOrTestControlsHandlers()
    {
        foreach (var method in new[]
        {
            "InitializeDevelopmentControls", "AddTestProfiles_Click", "PreviewUdsSample_Click",
            "ExportOfflineUdsTxt_Click", "DemoMonitor_Click", "DemoTimer_Tick",
            "EmitDemoFrames", "StopDemoMonitor", "ClearDemoData"
        })
            Assert.Null(typeof(MainWindow).GetMethod(method, PrivateInstance));
    }

    [Fact]
    public void LiveReadOnlyUdsHandlersRemainCompiled()
    {
        foreach (var method in new[] { "ReadVin_Click", "ReadDid_Click", "ReadDtc_Click" })
            Assert.NotNull(typeof(MainWindow).GetMethod(method, PrivateInstance));
    }

    [Fact]
    public void RealMonitorAndProfileControlsRemainInProductionXaml()
    {
        var xml = XDocument.Load(MainXaml());
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var names = xml.Descendants()
            .Select(element => (string?)element.Attribute(x + "Name"))
            .Where(name => name is not null)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var name in new[]
        {
            "ConnectButton", "StartButton", "FramesView",
            "CreateProfileButton", "EditProfileButton", "ReadVinButton",
            "ReadDidButton", "ReadDtcButton"
        })
            Assert.Contains(name, names);
        Assert.DoesNotContain("Add test profiles", File.ReadAllText(MainXaml()));
        Assert.DoesNotContain("Start demo", File.ReadAllText(MainXaml()));
    }

    [Fact]
    public void ProductionCsvNeverEmitsSyntheticSchema()
    {
        var rowType = App.GetType("Dvinholm.App.CanMonitorRow")
            ?? throw new InvalidOperationException("Real CAN monitor row type missing");
        var csvType = App.GetType("Dvinholm.App.CanMonitorCsv")
            ?? throw new InvalidOperationException("Real CAN CSV writer missing");
        var rows = Array.CreateInstance(rowType, 0);
        var build = csvType.GetMethod("BuildLines", PrivateStatic)
            ?? throw new InvalidOperationException("CAN CSV BuildLines missing");
        var normal = (IReadOnlyList<string>)build.Invoke(null, [rows, false])!;
        var accidentalDemo = (IReadOnlyList<string>)build.Invoke(null, [rows, true])!;
        Assert.Equal(new[] { "timestamp,id,type,dlc,data" }, normal);
        Assert.Equal(normal, accidentalDemo);
    }

    [Fact]
    public void SoftwareMultiCanIdFilterRemainsAvailable()
    {
        var historyType = App.GetType("Dvinholm.App.CanMonitorHistory")
            ?? throw new InvalidOperationException("CAN Monitor history missing");
        var matches = historyType.GetMethod("MatchesCanIdFilter", PrivateStatic)
            ?? throw new InvalidOperationException("Software ID filtering missing");
        Assert.True((bool)matches.Invoke(null, ["7E0,7E8", "7E8"])!);
        Assert.True((bool)matches.Invoke(null, ["7E0,7E8", "7E0"])!);
        Assert.False((bool)matches.Invoke(null, ["7E0,7E8", "123"])!);
    }
}
