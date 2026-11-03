using Microsoft.Win32;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline J2534 driver-readiness audit contracts. No native DLL is
/// loaded or executed; registry fixtures are in-memory synthetic records.
/// </summary>
public sealed class J2534AdapterAuditReportTests
{
    private static J2534DeviceDiscovery.RegisteredAudit Entry(
        string name, RegistryView view, J2534PeArchitecture architecture,
        string path = @"C:\PRIVATE\vendor\driver.dll") =>
        new(new J2534Device(name, "FixtureVendor", path, @"SOFTWARE\PRIVATE\J2534"),
            view, architecture);

    private static J2534AdapterAuditSummary Assess(
        bool x64 = true,
        params J2534DeviceDiscovery.RegisteredAudit[] entries) =>
        J2534AdapterAuditReport.Assess(entries, x64);

    [Fact]
    public void EmptyRegistrationSetShowsNoDeviceWithoutFalseHardwareSuccess()
    {
        var summary = Assess();
        Assert.Equal(0, summary.Inspected);
        Assert.Equal(0, summary.Compatible);
        Assert.Equal(0, summary.Rejected);
        Assert.Empty(summary.Items);
        Assert.False(summary.ConfirmsPhysicalAdapter);
        Assert.False(summary.MayOpenVehicleConnection);
        Assert.Contains("No existing registered DLL",
            J2534AdapterAuditReport.Format(summary, true, true));
    }

    [Fact]
    public void X64RegistryAndPeMustBothMatch()
    {
        var report = Assess(true,
            Entry("x64-good", RegistryView.Registry64, J2534PeArchitecture.X64),
            Entry("x86-old", RegistryView.Registry32, J2534PeArchitecture.X86));
        Assert.Equal(2, report.Inspected);
        Assert.Equal(1, report.Compatible);
        Assert.Equal(1, report.Rejected);
        Assert.Equal(J2534AdapterAuditOutcome.CompatibleArchitecture,
            report.Items[0].Outcome);
        Assert.Equal(J2534AdapterAuditOutcome.WrongBitness,
            report.Items[1].Outcome);
    }

    [Fact]
    public void LegacyX86AppAcceptsOnlyMatchingViewAndMachine()
    {
        var report = Assess(false,
            Entry("legacy-x86", RegistryView.Registry32, J2534PeArchitecture.X86),
            Entry("wrong-x64", RegistryView.Registry64, J2534PeArchitecture.X64));
        Assert.Equal(1, report.Compatible);
        Assert.Equal(1, report.Rejected);
        Assert.Equal(J2534AdapterAuditOutcome.CompatibleArchitecture,
            report.Items[0].Outcome);
        Assert.Contains("App architecture: x86",
            J2534AdapterAuditReport.Format(report, true, false));
    }

    [Fact]
    public void MatchingPeInWrongRegistryViewIsNotCompatible()
    {
        var result = Assess(true,
            Entry("x64-wrong-view", RegistryView.Registry32, J2534PeArchitecture.X64));
        Assert.Equal(0, result.Compatible);
        Assert.Equal(J2534AdapterAuditOutcome.WrongRegistryView,
            Assert.Single(result.Items).Outcome);
        Assert.Contains("other Windows registry view",
            J2534AdapterAuditReport.Format(result, true, true));
    }

    [Fact]
    public void Arm64AndUnknownHeadersHaveDistinctNonExecutableFailures()
    {
        var result = Assess(true,
            Entry("arm", RegistryView.Registry64, J2534PeArchitecture.Arm64),
            Entry("unknown", RegistryView.Registry64, J2534PeArchitecture.Unknown));
        Assert.Equal(2, result.Rejected);
        Assert.Contains(result.Items, item =>
            item.Outcome == J2534AdapterAuditOutcome.Arm64Unsupported);
        Assert.Contains(result.Items, item =>
            item.Outcome == J2534AdapterAuditOutcome.UnreadableOrUnknown);
        Assert.False(result.HasInstalledVerifiedVendorDriver);
    }

    [Fact]
    public void PrivatePathsAndForgedRegistryLabelsAreNeverEchoed()
    {
        var report = Assess(true,
            Entry(@"C:\PRIVATE\drivers\vorloc.dll", RegistryView.Registry64,
                J2534PeArchitecture.X64),
            Entry("name\nSECRET", RegistryView.Registry64, J2534PeArchitecture.Unknown),
            Entry("normal-adapter", RegistryView.Registry64, J2534PeArchitecture.X86,
                @"D:\SECRET\nested\custom.dll"));
        var en = J2534AdapterAuditReport.Format(report, true, true);
        var ru = J2534AdapterAuditReport.Format(report, false, true);
        foreach (var secret in new[] { "PRIVATE", "SECRET", "C:\\", "D:\\", "SOFTWARE" })
        {
            Assert.DoesNotContain(secret, en);
            Assert.DoesNotContain(secret, ru);
        }
        Assert.Contains("J2534 adapter", en);
        Assert.Contains("normal-adapter", en);
        Assert.Contains("DLL производителя не загружались", ru);
    }

    [Fact]
    public void LargeAuditCapsVisibleEntriesAndNeverClaimsTruncatedCompatibility()
    {
        var candidates = Enumerable.Range(0, 300).Select(i =>
            Entry("driver-" + i, RegistryView.Registry64, J2534PeArchitecture.X64));
        var summary = J2534AdapterAuditReport.Assess(candidates, true);
        Assert.Equal(J2534AdapterAuditReport.MaximumAuditEntries, summary.Inspected);
        Assert.True(summary.Truncated);
        Assert.Equal(J2534AdapterAuditReport.MaximumRows, summary.Items.Count);
        Assert.Equal(256, summary.Compatible);
        Assert.False(summary.ConfirmsPhysicalAdapter);
        Assert.Contains("truncated",
            J2534AdapterAuditReport.Format(summary, true, true));
    }

    [Fact]
    public void InvalidSummariesRejectedAndAllStatusCategoriesAreDisplayable()
    {
        Assert.Throws<ArgumentNullException>(() =>
            J2534AdapterAuditReport.Assess(null!, true));
        var invalid = new J2534AdapterAuditSummary(3, 0, 1, false,
            Array.Empty<J2534AdapterAuditItem>());
        Assert.Throws<InvalidDataException>(() =>
            J2534AdapterAuditReport.Format(invalid, true, true));
        Assert.Throws<ArgumentNullException>(() =>
            J2534AdapterAuditReport.Format(null!, true, true));

        foreach (var outcome in Enum.GetValues<J2534AdapterAuditOutcome>())
        {
            var result = new J2534AdapterAuditSummary(1,
                outcome == J2534AdapterAuditOutcome.CompatibleArchitecture ? 1 : 0,
                outcome == J2534AdapterAuditOutcome.CompatibleArchitecture ? 0 : 1,
                false, Array.AsReadOnly(new[]
                {
                    new J2534AdapterAuditItem("fixture", outcome)
                }));
            Assert.Contains("fixture", J2534AdapterAuditReport.Format(result, false, true));
        }
    }
}
