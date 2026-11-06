using System.IO.Compression;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Dvinholm.App;
using Xunit;

namespace Dvinholm.UnifiedX64.Tests;

/// <summary>
/// Eight x64 Release-only gates using real managed metadata and the exact
/// PR pilot ZIP. No app window, J2534 DLL, vehicle, secret or network call.
/// This is NOT a physical-adapter or Windows GUI acceptance test.
/// </summary>
public sealed class UnifiedX64ProductionSmokeTests
{
    private static readonly XNamespace X =
        "http://schemas.microsoft.com/winfx/2006/xaml";
    private static Assembly App => typeof(MainWindow).Assembly;
    private const BindingFlags PrivateInstance =
        BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags AnyStatic =
        BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

    private static string RepoRoot()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Dvinholm.sln")))
                return dir.FullName;
        throw new FileNotFoundException("Dvinholm.sln checkout root not found.");
    }

    private static XDocument Layout() =>
        XDocument.Load(Path.Combine(RepoRoot(), "src", "Dvinholm.App",
            "MainWindow.xaml"));

    private static XElement Named(XDocument doc, string name) =>
        doc.Descendants().Single(element =>
            (string?)element.Attribute(X + "Name") == name);

    [Fact]
    public void TestRunnerIsRealWindowsX64WithProductionFeatureFlagDisabled()
    {
        Assert.True(OperatingSystem.IsWindows());
        Assert.True(Environment.Is64BitProcess);
        Assert.Equal(Architecture.X64, RuntimeInformation.ProcessArchitecture);
#if DVINHOLM_DEVELOPMENT_FEATURES
        Assert.Fail("Release smoke test assembly must not enable developer features.");
#endif
        Assert.Equal("Dvinholm.App", App.GetName().Name);
    }

    [Fact]
    public void ProductionAssemblyDoesNotContainExperimentalOrSyntheticTypes()
    {
        foreach (var type in new[]
        {
            "CanDemoFrameGenerator", "UdsOfflineSample", "UdsOfflineTextReport",
            "ExperimentalCemPinCanProvider", "CemPinStore",
            "CemPinWindowsProtection"
        })
            Assert.Null(App.GetType("Dvinholm.App." + type));
    }

    [Fact]
    public void ProductionMainWindowOmitsDeveloperControlHandlers()
    {
        foreach (var method in new[]
        {
            "InitializeDevelopmentControls", "AddTestProfiles_Click",
            "PreviewUdsSample_Click", "ExportOfflineUdsTxt_Click",
            "DemoMonitor_Click", "OpenCemPinStore", "TryGetCemPinRange"
        })
            Assert.Null(typeof(MainWindow).GetMethod(method,
                PrivateInstance | BindingFlags.Static));

        foreach (var method in new[]
        {
            "ReadVin_Click", "ReadDid_Click", "ReadDtc_Click",
            "CheckCemPrerequisites_Click"
        })
            Assert.NotNull(typeof(MainWindow).GetMethod(method, PrivateInstance));
    }

    [Fact]
    public void CEMOfflineReadinessCardIsVisibleButDoesNotOpenAnECU()
    {
        var doc = Layout();
        var card = Named(doc, "CemPinIdentityCard");
        Assert.Equal("Visible", (string?)card.Attribute("Visibility"));
        var button = Named(doc, "CemPartPrerequisiteButton");
        Assert.Contains(button, card.Descendants());
        Assert.Equal("CheckCemPrerequisites_Click",
            (string?)button.Attribute("Click"));
        Assert.False(button.Attributes().Any(attribute =>
            attribute.Name.LocalName == "IsEnabled" &&
            attribute.Value == "False"));
        Assert.Contains(Named(doc, "CemPartPrerequisiteExpander"), card.Descendants());
        Assert.Contains(Named(doc, "CemIdentityEvidencePinText"),
            Named(doc, "CemPinPage").Descendants());
    }

    [Fact]
    public void AllExperimentalCEMPinControlsRemainHiddenOrDisabled()
    {
        var doc = Layout();
        foreach (var name in new[] { "CemPartNumberReadButton", "CemPinSavedButton",
            "CemPinSearchCard", "CemPinLivePanel" })
            Assert.Equal("Collapsed", (string?)Named(doc, name).Attribute("Visibility"));
        foreach (var name in new[] { "CemPartNumberReadButton", "CemPinReadButton",
            "CemPinRevealButton" })
            Assert.Equal("False", (string?)Named(doc, name).Attribute("IsEnabled"));
        Assert.Equal("False",
            (string?)Named(doc, "CemPinResultText").Attribute("IsTextSelectionEnabled"));
    }

    [Fact]
    public void ManifestResearchDoesNotEnableConfigurationOrRevealValues()
    {
        var doc = Layout();
        foreach (var name in new[] { "CemConfigBackupButton", "CemConfigCompareButton",
            "CemConfigPrepareButton" })
        {
            var button = Named(doc, name);
            Assert.Equal("False", (string?)button.Attribute("IsEnabled"));
            Assert.Null(button.Attribute("Click"));
        }

        var manifest = App.GetType("Dvinholm.App.VolvoP2CemConfigOptionManifest")
            ?? throw new InvalidOperationException("CEM reference manifest missing.");
        var entries = (System.Collections.IEnumerable?)manifest
            .GetProperty("Entries", AnyStatic)?.GetValue(null);
        Assert.NotNull(entries);
        var items = entries.Cast<object>().ToArray();
        Assert.Equal(14, items.Length);
        foreach (var item in items)
        {
            var type = item.GetType();
            foreach (var property in new[] { "MayReadVehicleConfiguration",
                "MayEditVehicleConfiguration", "MayAcquirePinOrAuthenticate",
                "MayTransmitToVehicle", "HasVerifiedPhysicalIdentity" })
            {
                var p = type.GetProperty(property, BindingFlags.Instance |
                    BindingFlags.NonPublic | BindingFlags.Public);
                Assert.NotNull(p);
                Assert.Equal(false, p.GetValue(item));
            }
        }
    }

    [Fact]
    public void ApprovedPrimaryScreensDoNotRequireLauncherOrSettingsPage()
    {
        var doc = Layout();
        foreach (var name in new[] { "ConnectionPage", "CemPinPage", "CemConfigPage",
            "DtcPage", "EcuPage", "UnifiedUpdatesPage" })
            Assert.NotNull(Named(doc, name));
        Assert.Empty(doc.Descendants().Where(node =>
            (string?)node.Attribute(X + "Name") is "SettingsPage" or "LauncherPage"));
    }

    [Fact]
    public void ActualPublishedZipIsAmd64AndItsManagedAssemblyHasNoDevTypes()
    {
        var zipPath = Path.Combine(RepoRoot(), "artifacts",
            "Dvinholm-Windows-x64.zip");
        Assert.True(File.Exists(zipPath), "Current x64 pilot ZIP must exist.");
        using var zip = ZipFile.OpenRead(zipPath);
        var exe = zip.GetEntry("Dvinholm.exe");
        var dll = zip.GetEntry("Dvinholm.dll");
        Assert.NotNull(exe);
        Assert.NotNull(dll);
        // The visual approval contract requires the real scenic PNG and
        // approved gull/gear SVG in the *published* package, not just sources.
        foreach (var asset in new[]
        {
            "Assets/Dvinholm-Banner.png",
            "Assets/Launcher-Northern-Emblem.svg"
        })
            Assert.Contains(zip.Entries, entry =>
                entry.FullName.Replace('\\', '/').EndsWith(asset,
                    StringComparison.OrdinalIgnoreCase) &&
                entry.Length > 1000);
        Assert.Contains(zip.Entries, e =>
            e.FullName.EndsWith("coreclr.dll", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(zip.Entries, e =>
            e.FullName.EndsWith("Microsoft.UI.Xaml.dll", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(zip.Entries, e =>
            e.FullName.EndsWith("hostfxr.dll", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(zip.Entries, e =>
            e.FullName.EndsWith("Launcher.exe", StringComparison.OrdinalIgnoreCase) ||
            e.FullName.EndsWith("Dvinholm.UpdateHandoffProbe.exe", StringComparison.OrdinalIgnoreCase) ||
            e.FullName.EndsWith("Updater-Banner.ps1", StringComparison.OrdinalIgnoreCase));

        using (var stream = exe.Open())
        using (var bytes = new MemoryStream())
        {
            stream.CopyTo(bytes);
            bytes.Position = 0;
            using var pe = new PEReader(bytes, PEStreamOptions.LeaveOpen);
            Assert.Equal(Machine.Amd64, pe.PEHeaders.CoffHeader.Machine);
        }
        using (var stream = dll.Open())
        using (var bytes = new MemoryStream())
        {
            stream.CopyTo(bytes);
            bytes.Position = 0;
            using var pe = new PEReader(bytes, PEStreamOptions.LeaveOpen);
            Assert.True(pe.HasMetadata);
            var metadata = pe.GetMetadataReader();
            var types = metadata.TypeDefinitions.Select(handle =>
                metadata.GetString(metadata.GetTypeDefinition(handle).Name))
                .ToHashSet(StringComparer.Ordinal);
            foreach (var forbidden in new[] { "CanDemoFrameGenerator",
                "UdsOfflineSample", "UdsOfflineTextReport",
                "ExperimentalCemPinCanProvider", "CemPinStore",
                "CemPinWindowsProtection" })
                Assert.DoesNotContain(forbidden, types);
            Assert.Contains("CemReadOnlyPrerequisiteReport", types);
            Assert.Contains("CemIdentityEvidenceReport", types);
        }
    }
}
