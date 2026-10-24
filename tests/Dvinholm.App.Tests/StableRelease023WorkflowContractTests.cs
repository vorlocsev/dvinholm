using System.Text.RegularExpressions;

namespace Dvinholm.App.Tests;

/// <summary>Eight guardrails for automated v0.2.3 staging and manual-only publication.</summary>
public sealed class StableRelease023WorkflowContractTests
{
    private static string Workflow()
    {
        for (DirectoryInfo? d = new(AppContext.BaseDirectory); d is not null; d = d.Parent)
        {
            var path = Path.Combine(d.FullName, ".github", "workflows", "release-v0.2.3.yml");
            if (File.Exists(path)) return File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal);
        }
        throw new FileNotFoundException("Release workflow not found.");
    }

    [Fact]
    public void OnlyMainPushCanStageAndOnlyManualDispatchCanPublish()
    {
        var source = Workflow();
        Assert.DoesNotMatch(new Regex(@"(?m)^  create:"), source);
        Assert.Contains("  push:\n    branches: [\"main\"]", source);
        Assert.Contains("pull_request:", source);
        var publishIndex = source.IndexOf("\n  publish:", StringComparison.Ordinal);
        Assert.True(publishIndex > 0);
        Assert.Contains("github.event_name == 'push' && github.ref == 'refs/heads/main'",
            source[..publishIndex]);
        Assert.Contains("github.event_name == 'workflow_dispatch'",
            source[publishIndex..]);
        Assert.DoesNotContain("github.event_name == 'push'", source[publishIndex..]);
    }

    [Fact]
    public void ManualRunDefaultsToDryRun()
    {
        Assert.Contains("workflow_dispatch:", Workflow());
        Assert.Contains("default: dry-run", Workflow());
    }

    [Fact]
    public void PublicationRequiresExplicitLiteralAuthorization()
    {
        var source = Workflow();
        Assert.Contains("APPROVE-DVINHOLM-V0.2.3", source);
        Assert.Contains("inputs.authorization ==", source);
    }

    [Fact]
    public void MainShaMustMatchApprovedSha()
    {
        var source = Workflow();
        Assert.Contains("refs/heads/main", source);
        Assert.Contains("inputs.approved_sha", source);
        Assert.Contains("Approved SHA does not equal checked-out main SHA", source);
    }

    [Fact]
    public void WritePermissionIsLimitedToPublishJob()
    {
        var source = Workflow();
        var publishIndex = source.IndexOf("\n  publish:", StringComparison.Ordinal);
        Assert.True(publishIndex > 0);
        Assert.Contains("permissions:\n  contents: read", source[..publishIndex]);
        Assert.DoesNotContain("contents: write", source[..publishIndex]);
        Assert.Contains("contents: write", source[publishIndex..]);
    }

    [Fact]
    public void StableBuildCompilesOutDevFeaturesAndDoesNotTrim()
    {
        var source = Workflow();
        Assert.Contains("-p:DvinholmDevelopmentFeatures=false", source);
        Assert.Contains("-p:PublishTrimmed=false", source);
        Assert.Contains("-p:PublishAot=false", source);
        Assert.Contains("-p:Version=0.2.3", source);
        Assert.DoesNotContain("--no-restore -p:Platform=x86 -p:DvinholmDevelopmentFeatures=true --logger", source);
        Assert.DoesNotContain("dotnet publish src/Dvinholm.App/Dvinholm.App.csproj --configuration Release --no-restore", source);
    }

    [Fact]
    public void VerifiedStagedArtifactMustRoundtripBeforePublish()
    {
        var source = Workflow();
        Assert.Contains("actions/upload-artifact@v7", source);
        Assert.Contains("actions/download-artifact@v7", source);
        Assert.Contains("-Channel stable", source);
        Assert.Contains("needs: validate", source);
        Assert.Contains("Test-StableAcceptance023Safety.ps1", source);
        Assert.Contains("Test eight acceptance-kit safety cases on real staged ZIP", source);
        Assert.Contains("Test-RealStableUpgrade023.ps1", source);
        Assert.Contains("gh release download v0.2.2", source);
        Assert.Contains("START-ACCEPTANCE.cmd", source);
        Assert.Contains("BUILD_COMMIT.txt", source);
        Assert.Contains("Dry-run one-click acceptance launcher against downloaded ZIP", source);
        Assert.Contains("Rehearse real v0.2.2 to staged v0.2.3 byte-exact mirror and rollback", source);
        Assert.True(source.IndexOf("Test-StableAcceptance023Safety.ps1 -ArchiveDirectory", StringComparison.Ordinal)
            < source.IndexOf("\n  publish:", StringComparison.Ordinal));
    }

    [Fact]
    public void ReleaseStartsAsDraftAndBecomesPublicAfterAudit()
    {
        var source = Workflow();
        Assert.Contains("--draft --title", source);
        Assert.Contains("gh release download v0.2.3", source);
        Assert.Contains("gh release edit v0.2.3", source);
        Assert.True(source.IndexOf("gh release download v0.2.3", StringComparison.Ordinal) < source.IndexOf("gh release edit v0.2.3", StringComparison.Ordinal));
    }
}
