using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Dvinholm.App.Tests;

public sealed class ReleaseDemoFeatureExclusionTests
{
    private static string Root()
    {
        for (DirectoryInfo? d = new(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "src", "Dvinholm.App", "MainWindow.xaml")))
                return d.FullName;
        throw new DirectoryNotFoundException("Dvinholm source checkout not found.");
    }

    private static XElement Control(string name)
    {
        var doc = XDocument.Load(Path.Combine(Root(), "src", "Dvinholm.App", "MainWindow.xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        return doc.Descendants().Single(e => (string?)e.Attribute(x + "Name") == name);
    }

    [Fact]
    public void ProductionAssembly_ExcludesSyntheticCanGenerator()
    {
        var t = typeof(MainWindow).Assembly.GetType("Dvinholm.App.CanDemoFrameGenerator");
#if DEBUG
        Assert.NotNull(t);
#else
        Assert.Null(t);
#endif
    }

    [Fact]
    public void ProductionAssembly_ExcludesOfflineUdsSample()
    {
        var t = typeof(MainWindow).Assembly.GetType("Dvinholm.App.UdsOfflineSample");
#if DEBUG
        Assert.NotNull(t);
#else
        Assert.Null(t);
#endif
    }

    [Fact]
    public void ProductionAssembly_ExcludesSyntheticTxtReport()
    {
        var t = typeof(MainWindow).Assembly.GetType("Dvinholm.App.UdsOfflineTextReport");
#if DEBUG
        Assert.NotNull(t);
#else
        Assert.Null(t);
#endif
    }

    [Fact]
    public void TestProfileButton_DefaultsToInvisible()
    {
        Assert.Equal("Collapsed", (string?)Control("AddTestProfilesButton").Attribute("Visibility"));
    }

    [Fact]
    public void DemoCanPanel_DefaultsToInvisible()
    {
        Assert.Equal("Collapsed", (string?)Control("DemoMonitorPanel").Attribute("Visibility"));
        Assert.NotNull(Control("DemoMonitorButton"));
    }

    [Fact]
    public void OfflineUdsPreviewAndTxtPanel_DefaultsToInvisible()
    {
        Assert.Equal("Collapsed", (string?)Control("OfflineUdsSamplePanel").Attribute("Visibility"));
        Assert.NotNull(Control("PreviewUdsSampleButton"));
        Assert.NotNull(Control("ExportOfflineUdsTxtButton"));
    }

    [Fact]
    public void DemoHandlers_HaveOnlyDebugBodies()
    {
        var code = File.ReadAllText(Path.Combine(Root(), "src", "Dvinholm.App", "MainWindow.xaml.cs"));
        foreach (var name in new[] { "AddTestProfiles_Click", "PreviewUdsSample_Click",
                     "ExportOfflineUdsTxt_Click", "DemoMonitor_Click",
                     "DemoTimer_Tick", "EmitDemoFrames", "StopDemoMonitor" })
        {
            var declaration = Regex.Match(code,
                @"private\s+(?:async\s+)?void\s+" + Regex.Escape(name) + @"\s*\(");
            Assert.True(declaration.Success, name + " declaration missing");
            var next = code.IndexOf("\n    private ", declaration.Index + declaration.Length);
            var method = code[declaration.Index..(next < 0 ? code.Length : next)];
            Assert.Contains("#if DEBUG", method);
            Assert.Contains("#endif", method);
        }
    }

    [Fact]
    public void RealDiagnosticsAndUserProfiles_StillPresent()
    {
        Assert.NotNull(Control("ConnectButton"));
        Assert.NotNull(Control("StartButton"));
        Assert.NotNull(Control("CreateProfileButton"));
        Assert.NotNull(Control("EditProfileButton"));
        var document = XDocument.Load(Path.Combine(Root(), "src", "Dvinholm.App", "MainWindow.xaml"));
        foreach (var handler in new[] { "Csv_Click", "ImportProfiles_Click", "ExportProfiles_Click" })
            Assert.Contains(document.Descendants(), e => (string?)e.Attribute("Click") == handler);
    }
}
