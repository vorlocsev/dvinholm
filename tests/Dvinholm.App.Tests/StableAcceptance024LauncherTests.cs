namespace Dvinholm.App.Tests;

/// <summary>
/// Eight deterministic offline one-click Stable 0.2.4 acceptance contracts.
/// Real Windows CMD prepare-only must also run against downloaded staged ZIPs.
/// </summary>
[Trait("Category", "ArchivedWorkflow")]
public sealed class StableAcceptance024LauncherTests
{
    private static string Read(string path)
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var file = Path.Combine(dir.FullName, path.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(file)) return File.ReadAllText(file);
        }
        throw new FileNotFoundException("Stable 0.2.4 launcher contract missing", path);
    }

    private static string Cmd => Read("START-STABLE-ACCEPTANCE-024.cmd");
    private static string Script => Read("eng/Start-StableAcceptance024.ps1");
    private static string Workflow => Read(".github/workflows/stable-staging-v0.2.4.yml");

    [Fact]
    public void CmdAcceptsOnlyNoArgsOrPrepareOnly()
    {
        Assert.Contains(@"if ""%~1""=="""" goto launch", Cmd);
        Assert.Contains(@"if /I ""%~1""==""--prepare-only"" if ""%~2""=="""" goto prepare", Cmd);
        Assert.Contains("Unsupported arguments", Cmd);
        Assert.Contains("exit /b 2", Cmd);
    }

    [Fact]
    public void CmdNeverInterpolatesShaOrUntrustedPathsIntoPowerShell()
    {
        Assert.Contains("setlocal DisableDelayedExpansion", Cmd);
        Assert.Contains(@"-File ""%~dp0eng\Start-StableAcceptance024.ps1"" -PrepareOnly", Cmd);
        Assert.Contains(@"-File ""%~dp0eng\Start-StableAcceptance024.ps1""", Cmd);
        Assert.DoesNotContain("%EXPECTED_COMMIT%", Cmd);
        Assert.DoesNotContain("%*", Cmd);
        Assert.DoesNotContain("start \"\" /D", Cmd);
    }

    [Fact]
    public void WholeCommitFileMustMatchStrictLowercaseShaBeforeUse()
    {
        var script = Script;
        Assert.Contains("BUILD_COMMIT_024_STABLE.txt", script);
        Assert.Contains("[IO.File]::ReadAllText($provenance)", script);
        Assert.Contains(@"'\A[0-9a-f]{40}(\r?\n)?\z'", script);
        Assert.Contains("$raw.Substring(0, 40)", script);
        Assert.Contains("Invalid exact lowercase 40-character Stable 0.2.4 commit receipt", script);
        Assert.True(script.IndexOf("-cnotmatch", StringComparison.Ordinal)
            < script.IndexOf("$commit = $raw.Substring", StringComparison.Ordinal));
    }

    [Fact]
    public void OnlySiblingCombinedCandidateAndNewIsolatedTargetAreUsed()
    {
        var script = Script;
        Assert.Contains("Join-Path $parent 'candidate'", script);
        Assert.Contains("Join-Path $parent 'Dvinholm-024-stable-acceptance'", script);
        Assert.Contains("Missing sibling candidate folder", script);
        Assert.Contains("Stable acceptance destination already exists: refusing overwrite", script);
        Assert.True(script.IndexOf("if (Test-Path -LiteralPath $destination)", StringComparison.Ordinal)
            < script.IndexOf("& $prepare -ArchiveDirectory", StringComparison.Ordinal));
    }

    [Fact]
    public void VerifierGetsExactShaAndNeverSkipsFullTwoFlavorAudit()
    {
        var script = Script;
        Assert.Contains("Prepare-StableAcceptance024.ps1", script);
        Assert.Contains("& $prepare -ArchiveDirectory $candidate -ExpectedCommit $commit -DestinationDirectory $destination", script);
        Assert.Contains("Stable 0.2.4 verification and private extraction did not complete", script);
        Assert.Contains("only Portable", script);
    }

    [Fact]
    public void ReceiptPrivateDataAndExeAreMandatoryBeforeAnyLaunch()
    {
        var script = Script;
        Assert.Contains("Join-Path $destination 'Dvinholm.App.exe'", script);
        Assert.Contains("Join-Path $destination 'ACCEPTANCE-0.2.4.txt'", script);
        Assert.Contains("Join-Path $destination 'acceptance-user-data'", script);
        Assert.Contains("Stable acceptance output missing verified EXE, receipt or private data folder", script);
        Assert.True(script.IndexOf("Stable acceptance output missing", StringComparison.Ordinal)
            < script.IndexOf("Start-Process -FilePath", StringComparison.Ordinal));
    }

    [Fact]
    public void PrepareOnlyExitsBeforeAllGuiActions()
    {
        var script = Script;
        var prepare = script.IndexOf("if ($PrepareOnly)", StringComparison.Ordinal);
        var exit = script.IndexOf("return", prepare, StringComparison.Ordinal);
        var gui = script.IndexOf("Start-Process -FilePath", StringComparison.Ordinal);
        Assert.True(prepare >= 0 && exit > prepare && gui > exit);
        Assert.Contains("WinUI NOT launched", script);
        Assert.Contains("$env:DVINHOLM_ACCEPTANCE_DATA_DIR = $private", script);
        Assert.Contains("-WorkingDirectory $destination", script);
    }

    [Fact]
    public void CiRehearsesTheRealDownloadedZipAndDoesNotPublish()
    {
        var workflow = Workflow;
        Assert.Contains("START-STABLE-ACCEPTANCE-024.cmd", workflow);
        Assert.Contains("eng/Start-StableAcceptance024.ps1", workflow);
        Assert.Contains("artifacts/stable024-downloaded", workflow);
        Assert.Contains("One-click Stable 0.2.4 acceptance", workflow);
        Assert.Contains("START-STABLE-ACCEPTANCE-024.cmd --prepare-only", workflow);
        // The standalone Lite verifier checks the actual Windows App SDK line
        // using the original project file relative to the kit's eng directory.
        // Missing this file failed the very first real one-click staging CI.
        Assert.Contains("src/Dvinholm.App/Dvinholm.App.csproj", workflow);
        Assert.Contains("$sourceProject = 'src/Dvinholm.App/Dvinholm.App.csproj'", workflow);
        Assert.Contains("Join-Path $kit 'src/Dvinholm.App'", workflow);
        Assert.Contains("Copy-Item -LiteralPath $sourceProject -Destination $kitProjectDirectory", workflow);
        Assert.Contains("destination already exists", workflow);
        Assert.Contains("Dvinholm-v0.2.4-STABLE-WINDOWS-ACCEPTANCE-KIT", workflow);
        Assert.Contains("contents: read", workflow);
        Assert.DoesNotContain("contents: write", workflow);
        Assert.DoesNotContain("gh release create", workflow);
        Assert.DoesNotContain("Start-Process -FilePath", workflow);
    }
}
