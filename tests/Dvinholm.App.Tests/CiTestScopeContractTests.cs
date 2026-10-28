namespace Dvinholm.App.Tests;

/// <summary>
/// Protects PR test selection against silently skipping active safety coverage.
/// This contract is never tagged ArchivedWorkflow.
/// </summary>
public sealed class CiTestScopeContractTests
{
    [Fact]
    public void MainRunsFullSuiteAndPrDefersOnlyExplicitlyTaggedArchivedWorkflows()
    {
        static string Read(string relative)
        {
            for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
                 dir is not null; dir = dir.Parent)
            {
                var file = Path.Combine(dir.FullName,
                    relative.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(file))
                    return File.ReadAllText(file);
            }
            throw new FileNotFoundException(relative);
        }

        var ci = Read(".github/workflows/ci.yml");
        Assert.Contains("if: github.event_name == 'pull_request'", ci);
        Assert.Contains("--filter \"Category!=ArchivedWorkflow\"", ci);
        Assert.Contains("if: github.event_name != 'pull_request'", ci);
        Assert.Contains("Test complete regression (main/manual)", ci);
        Assert.Contains("Test actual Stable feature profile (eight offline smoke gates)", ci);
        Assert.Contains("Package and validate Lite only", ci);
        Assert.Contains("Test-PublishedDevelopmentRelease.ps1 -LiteOnly", ci);

        string[] archived =
        [
            "WindowsLiteMsixArtifactRoundtripTests",
            "WindowsLiteMsixLtscManifestContractTests",
            "WindowsLiteMsixNegativeContractTests",
            "WindowsLiteMsixOfflinePrerequisiteTests",
            "WindowsLiteMsixRuntimeDependencyTests",
            "WindowsLiteMsixSha256SidecarTests",
            "WindowsLiteMsixSigningCertificatePreflightTests",
            "Rc024AcceptanceIsolationTests",
            "Rc024OneClickLauncherTests",
            "ReleaseCandidate024ContractTests",
            "ReleaseCandidate024LiteContractTests",
            "StableAcceptance024ContractTests",
            "StableAcceptance024LauncherTests",
            "StableReadiness024ContractTests",
            "StableStaging024ContractTests",
        ];
        foreach (var name in archived)
        {
            var source = Read($"tests/Dvinholm.App.Tests/{name}.cs");
            Assert.Contains("[Trait(\"Category\", \"ArchivedWorkflow\")]", source);
        }

        // These safety-critical modules must NOT be filtered out of PR CI.
        string[] active =
        [
            "IsoTpChannelProtocolTests", "CanRxDispatcherTests",
            "UdsClientTests", "LatestBuildUpdaterFailureTests",
            "CanMonitorRxMetadataJsonTests", "WindowsDistributionArchiveTests",
            "WindowsLitePrerequisiteTests"
        ];
        foreach (var name in active)
        {
            var source = Read($"tests/Dvinholm.App.Tests/{name}.cs");
            Assert.DoesNotContain("[Trait(\"Category\", \"ArchivedWorkflow\")]", source);
        }
    }
}
