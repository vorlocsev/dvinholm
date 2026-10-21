namespace Dvinholm.App.Tests;

public sealed class ReportExportDestinationGuardTests
{
    private const string App = @"C:\Dvinholm";

    [Fact]
    public void BlocksSyntheticTxtNextToApplicationExe()
    {
        Assert.True(ReportExportDestinationGuard.IsInsideApplicationDirectory(
            @"C:\Dvinholm\uds-SYNTHETIC-example.txt", App));
    }

    [Fact]
    public void BlocksCanMonitorCsvNextToApplicationExe()
    {
        Assert.True(ReportExportDestinationGuard.IsInsideApplicationDirectory(
            @"C:\Dvinholm\can-demo-SYNTHETIC.csv", App));
    }

    [Fact]
    public void BlocksNestedReportsEvenWithTrailingAppSeparator()
    {
        Assert.True(ReportExportDestinationGuard.IsInsideApplicationDirectory(
            @"C:\Dvinholm\MyReports\uds-SYNTHETIC-example.txt",
            @"C:\Dvinholm\"));
    }

    [Fact]
    public void BlocksCaseVariantDestinationOnWindows()
    {
        Assert.True(ReportExportDestinationGuard.IsInsideApplicationDirectory(
            @"c:\DVINHOLM\Report.txt", @"C:\dvinholm"));
    }

    [Fact]
    public void BlocksDotDotPathThatResolvesBackIntoApp()
    {
        Assert.True(ReportExportDestinationGuard.IsInsideApplicationDirectory(
            @"C:\Dvinholm\SomeFolder\..\Report.csv", App));
    }

    [Fact]
    public void AllowsDocumentsFolderOutsideInstallation()
    {
        Assert.False(ReportExportDestinationGuard.IsInsideApplicationDirectory(
            @"C:\Users\Test\Documents\uds-SYNTHETIC-example.txt", App));
    }

    [Fact]
    public void AllowsSiblingFolderSharingInstallationPrefix()
    {
        Assert.False(ReportExportDestinationGuard.IsInsideApplicationDirectory(
            @"C:\Dvinholm-Reports\can-demo-SYNTHETIC.csv", App));
    }

    [Fact]
    public void AllowsDotDotPathThatResolvesOutsideApp()
    {
        Assert.False(ReportExportDestinationGuard.IsInsideApplicationDirectory(
            @"C:\Dvinholm\..\Reports\uds-SYNTHETIC-example.txt", App));
    }
}
