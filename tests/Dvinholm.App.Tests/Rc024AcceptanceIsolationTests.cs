using Dvinholm.App;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight read-only/offline tests for 0.2.4 RC acceptance isolation and
/// compatibility with the previously verified v0.2.3 acceptance receipt.
/// No J2534 adapter or WinUI launch is required.
/// </summary>
public sealed class Rc024AcceptanceIsolationTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "Dvinholm-rc024-acceptance", Guid.NewGuid().ToString("N"));

    public Rc024AcceptanceIsolationTests() => Directory.CreateDirectory(_root);

    private string Private => Path.Combine(_root, DvinholmUserDataDirectory.AcceptanceFolder);

    private static string RepoFile(string relativePath)
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var file = Path.Combine(dir.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(file))
                return File.ReadAllText(file);
        }
        throw new FileNotFoundException("RC acceptance contract file not found", relativePath);
    }

    [Fact]
    public void MissingReceiptDoesNotActivateAcceptanceMode()
    {
        Assert.False(DvinholmUserDataDirectory.HasAcceptanceReceiptInDirectory(_root));
        Assert.Equal(Path.Combine(_root, "Dvinholm"),
            DvinholmUserDataDirectory.Resolve(_root, _root, null, false));
    }

    [Fact]
    public void Rc024ReceiptActivatesIsolatedAcceptanceMode()
    {
        File.WriteAllText(Path.Combine(_root, DvinholmUserDataDirectory.Rc024AcceptanceReceipt),
            "RC 0.2.4 test receipt");
        Assert.True(DvinholmUserDataDirectory.HasAcceptanceReceiptInDirectory(_root));
    }

    [Fact]
    public void ExistingStable023AcceptanceReceiptRemainsSupported()
    {
        File.WriteAllText(Path.Combine(_root, DvinholmUserDataDirectory.AcceptanceReceipt),
            "Stable 0.2.3 test receipt");
        Assert.True(DvinholmUserDataDirectory.HasAcceptanceReceiptInDirectory(_root));
    }

    [Fact]
    public void UnknownFutureVersionReceiptDoesNotActivateIsolation()
    {
        File.WriteAllText(Path.Combine(_root, "ACCEPTANCE-0.2.5.txt"), "unknown version");
        Assert.False(DvinholmUserDataDirectory.HasAcceptanceReceiptInDirectory(_root));
    }

    [Fact]
    public void NestedReceiptMustNotEnableTheApplicationRoot()
    {
        Directory.CreateDirectory(Private);
        File.WriteAllText(
            Path.Combine(Private, DvinholmUserDataDirectory.Rc024AcceptanceReceipt),
            "wrong folder");
        Assert.False(DvinholmUserDataDirectory.HasAcceptanceReceiptInDirectory(_root));
    }

    [Fact]
    public void DirectRcExeRestartResolvesToThePrivateFolder()
    {
        File.WriteAllText(Path.Combine(_root, DvinholmUserDataDirectory.Rc024AcceptanceReceipt),
            "RC fixture");
        var receiptPresent = DvinholmUserDataDirectory.HasAcceptanceReceiptInDirectory(_root);
        Assert.Equal(Private,
            DvinholmUserDataDirectory.Resolve(_root, Path.Combine(_root, "real-user-local"), null, receiptPresent));
    }

    [Fact]
    public void OverrideWithoutReceiptOrOutsidePrivateFolderFailsClosed()
    {
        Assert.Throws<InvalidOperationException>(() =>
            DvinholmUserDataDirectory.Resolve(_root, _root, Private, false));
        Assert.Throws<InvalidOperationException>(() =>
            DvinholmUserDataDirectory.Resolve(_root, _root, _root, true));
        Assert.Throws<InvalidOperationException>(() =>
            DvinholmUserDataDirectory.Resolve(_root, _root, "relative-path", true));
    }

    [Fact]
    public void WorkflowChecksDownloadedZipAndNeverLaunchesOrPublishes()
    {
        var script = RepoFile("eng/Prepare-RcAcceptance024.ps1");
        var workflow = RepoFile(".github/workflows/release-candidate-v0.2.4.yml");

        var verify = script.IndexOf("Test-ReleaseCandidate024.ps1", StringComparison.Ordinal);
        var receipt = script.IndexOf("ACCEPTANCE-0.2.4.txt", StringComparison.Ordinal);
        var move = script.IndexOf("[IO.Directory]::Move($temp, $target)", StringComparison.Ordinal);
        Assert.True(verify >= 0 && receipt > verify && move > receipt);
        Assert.Contains("-Channel rc", script);
        Assert.Contains("Destination exists: never modify an installed application", script);
        Assert.Contains("acceptance-user-data", script);
        Assert.Contains("NOT PUBLISHED", script);

        Assert.Contains("Prepare-RcAcceptance024.ps1", workflow);
        Assert.Contains("artifacts/rc-v0.2.4-downloaded", workflow);
        Assert.Contains("acceptance-user-data", workflow);
        Assert.Contains("contents: read", workflow);
        Assert.DoesNotContain("gh release create", workflow);
        Assert.DoesNotContain("Start-Process Dvinholm.App.exe", workflow);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
