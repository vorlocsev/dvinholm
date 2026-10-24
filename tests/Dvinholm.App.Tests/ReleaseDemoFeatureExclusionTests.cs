using System.Reflection;
using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>Production XAML and compiled binary must not expose synthetic controls.</summary>
public sealed class ReleaseDemoFeatureExclusionTests
{
    private static string Root()
    {
        for (DirectoryInfo? d = new(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "src", "Dvinholm.App", "MainWindow.xaml")))
                return d.FullName;
        throw new DirectoryNotFoundException("Dvinholm source checkout not found.");
    }

    private static string Xaml() => File.ReadAllText(Path.Combine(Root(), "src", "Dvinholm.App", "MainWindow.xaml"));
    private static XElement Control(string name)
    {
        var document = XDocument.Load(Path.Combine(Root(), "src", "Dvinholm.App", "MainWindow.xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        return document.Descendants().Single(e => (string?)e.Attribute(x + "Name") == name);
    }

    [Fact]
    public void ProductionAssembly_ExcludesSyntheticCanGenerator()
    {
        var type = typeof(MainWindow).Assembly.GetType("Dvinholm.App.CanDemoFrameGenerator");
#if DVINHOLM_DEVELOPMENT_FEATURES
        Assert.NotNull(type);
#else
        Assert.Null(type);
#endif
    }

    [Fact]
    public void ProductionAssembly_ExcludesOfflineUdsSample()
    {
        var type = typeof(MainWindow).Assembly.GetType("Dvinholm.App.UdsOfflineSample");
#if DVINHOLM_DEVELOPMENT_FEATURES
        Assert.NotNull(type);
#else
        Assert.Null(type);
#endif
    }

    [Fact]
    public void ProductionAssembly_ExcludesSyntheticTxtReport()
    {
        var type = typeof(MainWindow).Assembly.GetType("Dvinholm.App.UdsOfflineTextReport");
#if DVINHOLM_DEVELOPMENT_FEATURES
        Assert.NotNull(type);
#else
        Assert.Null(type);
#endif
    }

    [Fact]
    public void StaticXaml_HasNoTestProfileButton()
    {
        Assert.DoesNotContain("Add test profiles", Xaml());
        Assert.DoesNotContain("AddTestProfiles_Click", Xaml());
    }

    [Fact]
    public void StaticXaml_HasNoDemoMonitorPanel()
    {
        Assert.DoesNotContain("Start demo", Xaml());
        Assert.DoesNotContain("DemoMonitor_Click", Xaml());
        Assert.NotNull(Control("FramesView"));
    }

    [Fact]
    public void StaticXaml_HasNoOfflineUdsDemoPanel()
    {
        Assert.DoesNotContain("Preview UDS example (offline)", Xaml());
        Assert.DoesNotContain("Export synthetic UDS TXT", Xaml());
        Assert.NotNull(Control("ReadVinButton"));
    }

    [Fact]
    public void DeveloperHandlers_AreCompiledOnlyWithFeatureOptIn()
    {
        var type = typeof(MainWindow);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (var handler in new[] { "InitializeDevelopmentControls", "AddTestProfiles_Click",
            "PreviewUdsSample_Click", "ExportOfflineUdsTxt_Click", "DemoMonitor_Click",
            "DemoTimer_Tick", "EmitDemoFrames", "StopDemoMonitor", "ClearDemoData" })
        {
            var method = type.GetMethod(handler, flags);
#if DVINHOLM_DEVELOPMENT_FEATURES
            Assert.NotNull(method);
#else
            Assert.Null(method);
#endif
        }
    }

    [Fact]
    public void RealDiagnosticsAndUserProfiles_RemainAvailable()
    {
        Assert.NotNull(Control("ConnectButton"));
        Assert.NotNull(Control("StartButton"));
        Assert.NotNull(Control("CreateProfileButton"));
        Assert.NotNull(Control("EditProfileButton"));
        Assert.NotNull(Control("ReadDidButton"));
        Assert.NotNull(Control("ReadDtcButton"));
        var document = XDocument.Load(Path.Combine(Root(), "src", "Dvinholm.App", "MainWindow.xaml"));
        foreach (var handler in new[] { "Csv_Click", "ImportProfiles_Click", "ExportProfiles_Click" })
            Assert.Contains(document.Descendants(), e => (string?)e.Attribute("Click") == handler);
    }
}
