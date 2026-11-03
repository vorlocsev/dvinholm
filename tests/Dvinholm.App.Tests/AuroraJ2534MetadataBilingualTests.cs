using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline contracts for bilingual J2534 metadata in the private
/// Connection card. No physical DLL, vehicle or CAN data is accessed.
/// </summary>
public sealed class AuroraJ2534MetadataBilingualTests
{
    private static readonly XNamespace X =
        "http://schemas.microsoft.com/winfx/2006/xaml";

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

    private static XDocument Layout()
    {
        using var stream = typeof(AuroraJ2534MetadataBilingualTests).Assembly
            .GetManifestResourceStream("Dvinholm.App.Tests.MainWindow.xaml")
            ?? throw new InvalidOperationException("MainWindow.xaml embedded resource missing.");
        return XDocument.Load(stream);
    }

    private static XElement Named(XDocument doc, string id) =>
        doc.Descendants().Single(e => (string?)e.Attribute(X + "Name") == id);

    [Fact]
    public void DisconnectedStateSwitchesLanguageWithoutClaimingDriverAvailability()
    {
        var en = J2534ConnectionPresentation.DisconnectedDetailsForLanguage(true);
        var ru = J2534ConnectionPresentation.DisconnectedDetailsForLanguage(false);
        Assert.Equal(J2534ConnectionPresentation.DisconnectedDetails, en);
        Assert.Contains("disconnected", en);
        Assert.Contains("нет подключения", ru);
        Assert.NotEqual(en, ru);
        Assert.DoesNotContain("подключён", ru);
    }

    [Fact]
    public void EnglishVersionDisplayRetainsExistingDetailsAndDefaults()
    {
        var version = new J2534Native.AdapterVersionInfo("FW3.5", "DLL2.6", "API04.04");
        var original = J2534ConnectionPresentation.LocalDetails(version, null);
        var explicitEn = J2534ConnectionPresentation.LocalDetails(version, null, true);
        Assert.Equal(original, explicitEn);
        Assert.Contains("Driver-reported (unverified; local workspace only)", explicitEn);
        Assert.Contains("API API04.04", explicitEn);
        Assert.Contains("DLL DLL2.6", explicitEn);
        Assert.Contains("FW FW3.5", explicitEn);
        Assert.Contains("capabilities not reported", explicitEn);
    }

    [Fact]
    public void RussianMetadataRetainsOriginalVersionNumbersAndCautions()
    {
        var version = new J2534Native.AdapterVersionInfo("FW3.5", "DLL2.6", "API04.04");
        var ru = J2534ConnectionPresentation.LocalDetails(version, null, english: false);
        Assert.Contains("Данные драйвера (не проверены; только локально)", ru);
        Assert.Contains("API API04.04", ru);
        Assert.Contains("DLL DLL2.6", ru);
        Assert.Contains("FW FW3.5", ru);
        Assert.Contains("возможности не предоставлены", ru);
        Assert.DoesNotContain("подтверждённый блок ECU", ru);
    }

    [Fact]
    public void RussianCapabilityFlagsAreNotMistakenForConnectedHardware()
    {
        var supported = new J2534Native.AdapterCapabilities(true, true, false);
        var unavailable = new J2534Native.AdapterCapabilities(false, false, true);
        var yes = J2534ConnectionPresentation.LocalDetails(null, supported, false);
        var no = J2534ConnectionPresentation.LocalDetails(null, unavailable, false);
        Assert.Contains("Аппаратные фильтры доступны", yes);
        Assert.Contains("запрос версии доступен", yes);
        Assert.Contains("Аппаратные фильтры недоступны", no);
        Assert.Contains("запрос версии недоступен", no);
        Assert.Contains("версия не предоставлена", yes);
        Assert.Contains("не проверены", no);
    }

    [Fact]
    public void BothLanguagesApplyTheSameVendorStringSanitizationAndLengthLimit()
    {
        const string secretPath = @"C:\Users\Private\driver.dll";
        var source = new J2534Native.AdapterVersionInfo(
            secretPath, "version\nline", new string('Z', 150));
        var en = J2534ConnectionPresentation.LocalDetails(source, null, true);
        var ru = J2534ConnectionPresentation.LocalDetails(source, null, false);
        foreach (var text in new[] { en, ru })
        {
            Assert.DoesNotContain(secretPath, text);
            Assert.DoesNotContain("version\nline", text);
            Assert.Contains("[local path redacted]", text);
            Assert.DoesNotContain(new string('Z', 150), text);
            Assert.Contains("…", text);
        }
    }

    [Fact]
    public void LanguageProjectionDoesNotPublishVendorDataToPublicActivity()
    {
        var source = Source("src/Dvinholm.App/MainWindow.xaml.cs");
        var start = source.IndexOf("private void RefreshLocalAdapterMetadataLanguage(", StringComparison.Ordinal);
        var end = source.IndexOf("private void J2534Device_SelectionChanged(", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var method = source[start..end];
        Assert.Contains("AdapterMetadataText.Text =", method);
        Assert.Contains("_adapterMetadataConnected && _connected && _session.IsConnected", method);
        Assert.Contains("DisconnectedDetailsForLanguage(_unifiedEnglish)", method);
        Assert.Contains("_session.AdapterVersion, _session.AdapterCapabilities, _unifiedEnglish", method);
        Assert.DoesNotContain("SetStatus(", method);
        Assert.DoesNotContain("AuroraLanding.", method);
        Assert.DoesNotContain("_session.Connect(", method);
    }

    [Fact]
    public void DisconnectAndConnectResetMetadataBeforeAnyNewVendorValueIsShown()
    {
        var source = Source("src/Dvinholm.App/MainWindow.xaml.cs");
        var start = source.IndexOf("private async void Connect_Click(", StringComparison.Ordinal);
        var end = source.IndexOf("private void Start_Click(", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var method = source[start..end];
        Assert.Equal(2, method.Split("_adapterMetadataConnected = false;").Length - 1);
        var disconnect = method[..method.IndexOf("if (string.IsNullOrWhiteSpace(DllPath.Text))",
            StringComparison.Ordinal)];
        Assert.True(disconnect.IndexOf("_adapterMetadataConnected = false;", StringComparison.Ordinal) <
            disconnect.IndexOf("await monitorTask;", StringComparison.Ordinal),
            "Disconnection must invalidate vendor metadata before asynchronous monitor teardown.");
        Assert.Equal(2, method.Split(
            "J2534ConnectionPresentation.DisconnectedDetailsForLanguage(_unifiedEnglish)").Length - 1);
        Assert.Contains("_adapterMetadataConnected = true;", method);
        Assert.Contains("J2534ConnectionPresentation.LocalDetails(", method);
        Assert.Contains("SetStatus(J2534ConnectionPresentation.ConnectedActivity);", method);
        Assert.DoesNotContain("SetStatus(J2534ConnectionPresentation.LocalDetails(", method);
        Assert.DoesNotContain("SetStatus(_session.AdapterVersion", method);
    }

    [Fact]
    public void LocaleToggleUpdatesOnlyPrivateWrappingCardWithSixSafeNavigationRoutes()
    {
        var locale = Source("src/Dvinholm.App/MainWindow.UnifiedUpdates.cs");
        var start = locale.IndexOf("private void ApplyUnifiedLocalization()", StringComparison.Ordinal);
        var end = locale.IndexOf("private void SetPrimaryNavigationLabel", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var method = locale[start..end];
        Assert.Contains("RefreshLocalAdapterMetadataLanguage();", method);
        Assert.DoesNotContain("_session.Connect(", method);
        Assert.DoesNotContain("_session.Disconnect(", method);

        var xaml = Layout();
        var metadata = Named(xaml, "AdapterMetadataText");
        Assert.Contains(Named(xaml, "ConnectionStatusPane"), metadata.Ancestors());
        Assert.Equal("Wrap", (string?)metadata.Attribute("TextWrapping"));
        Assert.Equal("True", (string?)metadata.Attribute("IsTextSelectionEnabled"));
        Assert.Equal(6, Named(xaml, "SidebarNavPanel").Elements()
            .Count(e => e.Name.LocalName == "Button"));
        Assert.Equal("False", (string?)Named(xaml, "CemConfigWriteButton").Attribute("IsEnabled"));
        Assert.Equal("False", (string?)Named(xaml, "DtcClearButton").Attribute("IsEnabled"));
    }
}
