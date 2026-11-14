using System.Reflection;
using System.Text.RegularExpressions;

namespace Dvinholm.App.Tests;

/// <summary>
/// Stage 558 contracts: only the PR-only x64 pilot gets a stamped
/// Development CI run and exact source head. Stable release jobs are
/// untouched. No network, desktop capture or J2534 hardware involved.
/// </summary>
public sealed class AuroraDevelopmentBuildVersionContracts
{
    private static string Source(string path)
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
        {
            var file = Path.Combine(dir.FullName,
                path.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(file))
                return File.ReadAllText(file);
        }
        throw new FileNotFoundException(path);
    }

    private static string PilotWorkflow() =>
        Source(".github/workflows/winui3-unified-x64.yml");

    private static string Formatted(string informationalVersion)
    {
        var updater = typeof(Dvinholm.App.MainWindow).Assembly
            .GetType("Dvinholm.App.LatestBuildUpdater", throwOnError: true)!;
        var method = updater.GetMethod("FormatInstalledVersion",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return (string)method.Invoke(null, new object[] { informationalVersion })!;
    }

    private const string SourceCommit = "0123456789abcdef0123456789abcdef01234567";

    [Fact]
    public void DevelopmentFormatShowsRunAndShortSevenCharacterCommit()
    {
        Assert.Equal("CI #602 · 0123456",
            Formatted("0.2.4+" + SourceCommit + ".ci602"));
    }

    [Fact]
    public void StableFormatRetainsOnlyReleaseNumber()
    {
        Assert.Equal("0.2.4",
            Formatted("0.2.4+" + SourceCommit + ".stable"));
    }

    [Fact]
    public void UnstampedOldBuildExplainsHistoricalMissingCiNumber()
    {
        Assert.Equal("0.2.4 · 0123456",
            Formatted("0.2.4+" + SourceCommit));
    }

    [Fact]
    public void PilotStampsTruePrHeadAndRunNumberWithoutMergeShaAppend()
    {
        var yaml = PilotWorkflow();
        var publish = yaml.IndexOf(
            "- name: Publish WinUI 3 x64 self-contained application",
            StringComparison.Ordinal);
        var packaged = yaml.IndexOf(
            "- name: Verify packaged native x64 WinUI 3 runtime",
            publish, StringComparison.Ordinal);
        Assert.True(publish >= 0 && packaged > publish);
        var step = yaml[publish..packaged];
        Assert.Contains("AURORA_PR_HEAD_SHA: ${{ github.event.pull_request.head.sha }}",
            step);
        Assert.Contains("$env:GITHUB_EVENT_NAME -eq 'pull_request'", step);
        Assert.Contains("$env:GITHUB_RUN_NUMBER", step);
        Assert.Contains("$sourceSha.ToLowerInvariant()", step);
        Assert.Contains("SelectSingleNode('/Project/PropertyGroup/Version')", step);
        Assert.Contains("-p:InformationalVersion=$buildVersion", step);
        Assert.Contains("-p:IncludeSourceRevisionInInformationalVersion=false", step);
        Assert.Equal(1, Regex.Matches(yaml,
            "- name: Publish WinUI 3 x64 self-contained application").Count);
    }

    [Fact]
    public void ProductionZipIsIndependentlyCheckedForTheEmbeddedVersion()
    {
        var yaml = PilotWorkflow();
        var smoke = Source(
            "tests/Dvinholm.UnifiedX64.Tests/UnifiedX64ProductionSmokeTests.cs");
        Assert.Contains("AURORA_PR_HEAD_SHA: ${{ github.event.pull_request.head.sha }}",
            yaml);
        Assert.Contains("AssemblyInformationalVersionAttribute", smoke);
        Assert.Contains("metadata.GetAssemblyDefinition().GetCustomAttributes()", smoke);
        Assert.Contains("var actualVersion = infoReader.ReadSerializedString();", smoke);
        Assert.Contains("Environment.GetEnvironmentVariable(\"GITHUB_RUN_NUMBER\")", smoke);
        Assert.Contains("Assert.Equal($\"{baseVersion}+{sourceSha.ToLowerInvariant()}.ci{runNumber}\"", smoke);
        // A source-only assertion would pass even if the package still had
        // a merge SHA. The smoke test explicitly opens the shipped ZIP.
        Assert.Contains("using var zip = ZipFile.OpenRead(zipPath);", smoke);
        Assert.Contains("using (var stream = dll.Open())", smoke);
        Assert.Equal(1, Regex.Matches(yaml,
            "- name: Verify actual x64 Release ZIP and forbidden developer types").Count);
    }
}
