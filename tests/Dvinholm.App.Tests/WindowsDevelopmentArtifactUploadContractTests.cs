namespace Dvinholm.App.Tests;

/// <summary>
/// Guards one final validated Lite ZIP and its matching SHA256SUMS manifest.
/// No release, package installation or real J2534 adapter is exercised.
/// </summary>
public sealed class WindowsDevelopmentArtifactUploadContractTests
{
    [Theory]
    [InlineData("valid", true)]
    [InlineData("raw-directory", false)]
    [InlineData("wrong-checksum-path", false)]
    [InlineData("silent-missing-file", false)]
    [InlineData("double-compress", false)]
    [InlineData("missing-package-gate", false)]
    [InlineData("release-drift", false)]
    [InlineData("portable-returns", false)]
    public void Workflow_OnlyPublishesVerifiedLiteAndHash(string scenario, bool expectedValid)
    {
        var workflow = File.ReadAllText(FindWorkflow())
            .Replace("\r\n", "\n", StringComparison.Ordinal);
        workflow = scenario switch
        {
            "raw-directory" => workflow.Replace(
                "path: artifacts/Dvinholm-Windows-x86-Lite.zip",
                "path: artifacts/app-lite", StringComparison.Ordinal),
            "wrong-checksum-path" => workflow.Replace(
                "path: artifacts/SHA256SUMS.txt", "path: artifacts/checksums.txt",
                StringComparison.Ordinal),
            "silent-missing-file" => workflow.Replace(
                "          if-no-files-found: error\n", "", StringComparison.Ordinal),
            "double-compress" => workflow.Replace(
                "          compression-level: 0", "          compression-level: 6",
                StringComparison.Ordinal),
            "missing-package-gate" => workflow.Replace(
                "- name: Package and validate main application", "- name: Skip package verification",
                StringComparison.Ordinal),
            "release-drift" => workflow.Replace(
                "\"artifacts/Dvinholm-Windows-x86-Lite.zip\" \"artifacts/SHA256SUMS.txt\"",
                "\"artifacts/app-lite\" \"artifacts/SHA256SUMS.txt\"",
                StringComparison.Ordinal),
            "portable-returns" => workflow + "\n      - name: Publish Portable\n",
            _ => workflow
        };
        if (expectedValid) Verify(workflow);
        else Assert.Throws<InvalidOperationException>(() => Verify(workflow));
    }

    private static void Verify(string workflow)
    {
        void Require(bool ok) { if (!ok) throw new InvalidOperationException("Lite-only CI contract violated."); }
        Require(workflow.Contains("- name: Package and validate main application", StringComparison.Ordinal));
        Require(workflow.Contains("./eng/Test-WindowsDistribution.ps1 -Flavor Lite", StringComparison.Ordinal));
        Require(workflow.Contains("./eng/Test-WindowsDistributionExtraction.ps1 -Flavor Lite", StringComparison.Ordinal));
        Require(workflow.Contains("./eng/Test-LiteBundledPreflight.ps1 -Flavor Lite", StringComparison.Ordinal));
        Require(workflow.Contains("./eng/Test-WindowsDistributionChecksums.ps1 -Directory artifacts -LiteOnly", StringComparison.Ordinal));
        Require(workflow.Contains("path: artifacts/Dvinholm-Windows-x86-Lite.zip", StringComparison.Ordinal));
        Require(workflow.Contains("path: artifacts/SHA256SUMS.txt", StringComparison.Ordinal));
        Require(workflow.Contains("          if-no-files-found: error", StringComparison.Ordinal));
        Require(workflow.Contains("          compression-level: 0", StringComparison.Ordinal));
        Require(workflow.Contains("\"artifacts/Dvinholm-Windows-x86-Lite.zip\" \"artifacts/SHA256SUMS.txt\"", StringComparison.Ordinal));
        Require(workflow.Contains("- name: Publish public CI asset", StringComparison.Ordinal));
        Require(!workflow.Contains("      - name: Publish Portable", StringComparison.Ordinal));
        // New canonical launcher bundle is now the main archive, while the
        // framework-dependent legacy update feed remains for old clients.
        Require(workflow.Contains("path: artifacts/Dvinholm-Windows-x86.zip", StringComparison.Ordinal));
        Require(workflow.Contains("- name: Upload main Dvinholm distribution", StringComparison.Ordinal));
        Require(workflow.IndexOf("- name: Package and validate main application", StringComparison.Ordinal) <
            workflow.IndexOf("- name: Upload compatibility update feed", StringComparison.Ordinal));
    }

    private static string FindWorkflow()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, ".github", "workflows", "ci.yml");
            if (File.Exists(path)) return path;
        }
        throw new FileNotFoundException("Missing CI workflow.");
    }
}
