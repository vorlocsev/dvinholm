using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Stage 566: registry-name classification is a local hint only.
/// These tests never load vendor DLLs, open J2534 channels or send CAN frames.
/// </summary>
public sealed class J2534RegistrationIdentityTests
{
    private static J2534Device Device(string name, string vendor = "Vendor") =>
        new(name, vendor, @"C:\Drivers\j2534-x64.dll",
            @"SOFTWARE\PassThruSupport.04.04\Adapter");

    [Theory]
    [InlineData("SETEK - DiCE", "SETEK")]
    [InlineData("DiCE64", "SETEK")]
    [InlineData("J2534", "Volvo DiCE")]
    public void DiceTokenIsARegistrationNameHintOnly(string name, string vendor)
    {
        var device = Device(name, vendor);
        Assert.Equal(J2534RegistrationFamily.DiCE,
            J2534RegistrationIdentity.Classify(device, device.DllPath));
        var text = J2534RegistrationIdentity.Caption(device, device.DllPath, true);
        Assert.Contains("DiCE", text);
        Assert.Contains("registration name alone cannot verify", text);
        Assert.DoesNotContain(device.DllPath, text);
    }

    [Theory]
    [InlineData("Mongoose JLR Pro")]
    [InlineData("DrewTech MongoosePro")]
    [InlineData("MONGOOSE - J2534")]
    public void MongooseTokenIsNotProofOfJlrProOrHardware(string name)
    {
        var device = Device(name);
        Assert.Equal(J2534RegistrationFamily.Mongoose,
            J2534RegistrationIdentity.Classify(device, device.DllPath));
        var caption = J2534RegistrationIdentity.Caption(device, device.DllPath, true);
        Assert.Contains("Mongoose", caption);
        Assert.DoesNotContain("JLR Pro", caption);
        Assert.DoesNotContain("connected", caption, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OtherRegistrationIsLabeledGenericWithoutInferredVendor()
    {
        var device = Device("PassThru generic 04.04", "Third-party");
        Assert.Equal(J2534RegistrationFamily.GenericJ2534,
            J2534RegistrationIdentity.Classify(device, device.DllPath));
        Assert.Contains("generic J2534 / other label",
            J2534RegistrationIdentity.Caption(device, device.DllPath, true));
    }

    [Fact]
    public void ConflictingRegistrationNamesDoNotPreferEitherBrand()
    {
        var device = Device("DiCE / Mongoose");
        Assert.Equal(J2534RegistrationFamily.Ambiguous,
            J2534RegistrationIdentity.Classify(device, device.DllPath));
        Assert.Contains("ambiguous", J2534RegistrationIdentity.Caption(
            device, device.DllPath, true));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(@"C:\Drivers\other.dll")]
    public void NoSelectionOrManuallyChangedDllNeverCarriesStaleIdentity(string? dll)
    {
        var device = Device("DiCE");
        var text = J2534RegistrationIdentity.Caption(device, dll, true);
        Assert.Contains("no matching registration / manual DLL path", text);
        Assert.Equal(J2534RegistrationFamily.NoMatchingRegistration,
            J2534RegistrationIdentity.Classify(device, dll));
        Assert.Equal(J2534RegistrationFamily.NoMatchingRegistration,
            J2534RegistrationIdentity.Classify(null, device.DllPath));
    }

    [Fact]
    public void SelectedPathMatchIsCaseInsensitiveButStillExact()
    {
        var device = Device("DiCE");
        Assert.Equal(J2534RegistrationFamily.DiCE,
            J2534RegistrationIdentity.Classify(device,
                @" c:\DRIVERS\J2534-X64.DLL "));
        Assert.Equal(J2534RegistrationFamily.NoMatchingRegistration,
            J2534RegistrationIdentity.Classify(device,
                device.DllPath + ".bak"));
    }

    [Theory]
    [InlineData("Medice J2534")]
    [InlineData("Supermongoose adapter")]
    public void EmbeddedProductFragmentsDoNotMisidentifyOtherLabels(string name)
    {
        var device = Device(name);
        Assert.Equal(J2534RegistrationFamily.GenericJ2534,
            J2534RegistrationIdentity.Classify(device, device.DllPath));
    }

    [Fact]
    public void BilingualCaptionsNeverEchoUntrustedRegistryTextOrPaths()
    {
        var device = Device("DiCE SECRET_VIN_123",
            @"C:\Users\someone\private\adapter.dll");
        foreach (var english in new[] { true, false })
        {
            var caption = J2534RegistrationIdentity.Caption(
                device, device.DllPath, english);
            Assert.DoesNotContain("SECRET_VIN_123", caption);
            Assert.DoesNotContain(@"C:\Users\someone", caption);
            Assert.DoesNotContain(device.DllPath, caption);
            Assert.Contains("DiCE", caption);
        }
        Assert.Contains("Подсказка по записи реестра:",
            J2534RegistrationIdentity.Caption(device, device.DllPath, false));
    }

    [Fact]
    public void ConnectionCardAndEventsRefreshHintWithoutOpeningAdapter()
    {
        var xaml = Source("src/Dvinholm.App/MainWindow.xaml");
        var source = Source("src/Dvinholm.App/MainWindow.xaml.cs");
        var ui = XDocument.Parse(xaml);
        var field = ui.Descendants().Single(x =>
            (string?)x.Attribute(XName.Get("Name",
                "http://schemas.microsoft.com/winfx/2006/xaml")) ==
            "ConnectionRegistrationHintText");
        Assert.Equal("TextBlock", field.Name.LocalName);
        Assert.Equal("Wrap", (string?)field.Attribute("TextWrapping"));
        Assert.Contains("Registration name hint", (string?)field.Attribute("Text"));
        Assert.Contains("RefreshJ2534RegistrationHint();", source);
        var refresh = source.IndexOf("private void RefreshJ2534Devices(", StringComparison.Ordinal);
        var metadata = source.IndexOf("private void RefreshLocalAdapterMetadataLanguage(",
            StringComparison.Ordinal);
        Assert.True(refresh > 0 && metadata > refresh);
        Assert.Contains("RefreshJ2534RegistrationHint();", source[refresh..metadata]);
        var selected = source.IndexOf("private void J2534Device_SelectionChanged(",
            StringComparison.Ordinal);
        var end = source.IndexOf("// Passive ECU observations", selected,
            StringComparison.Ordinal);
        Assert.True(selected > metadata && end > selected);
        Assert.Contains("RefreshJ2534RegistrationHint();", source[selected..end]);
        var pathChanged = source.IndexOf("private void DllPath_TextChanged(",
            StringComparison.Ordinal);
        Assert.Contains("RefreshJ2534RegistrationHint();",
            source[pathChanged..refresh]);
        var localeEnd = source.IndexOf("private void J2534Device_SelectionChanged(",
            metadata, StringComparison.Ordinal);
        Assert.Contains("RefreshJ2534RegistrationHint();",
            source[metadata..localeEnd]);
    }

    private static string Source(string relative)
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
        {
            var file = Path.Combine(dir.FullName,
                relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(file)) return File.ReadAllText(file);
        }
        throw new FileNotFoundException(relative);
    }
}
