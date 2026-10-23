namespace Dvinholm.App.Tests;

/// <summary>
/// Guards the CI Actions upload contract: artifacts must contain the exact
/// already-validated Portable/Lite ZIPs and checksum manifest, never the raw
/// publish directories. No network or Windows runtime is required.
/// </summary>
public sealed class WindowsDevelopmentArtifactUploadContractTests
{
    [Theory]
    [InlineData("valid", true)]
    [InlineData("portable-raw-folder", false)]
    [InlineData("lite-raw-folder", false)]
    [InlineData("wrong-checksum-path", false)]
    [InlineData("silent-missing-file", false)]
    [InlineData("double-compress", false)]
    [InlineData("missing-package-gate", false)]
    [InlineData("release-zip-drift", false)]
    public void Workflow_UploadsOnlyFinalVerifiedReleaseBytes(
        string scenario, bool expectedValid)
    {
        var workflow = File.ReadAllText(FindWorkflow());
        workflow = scenario switch
        {
            "portable-raw-folder" => workflow.Replace(
                "path: artifacts/Dvinholm-Windows-x86.zip\n",
                "path: artifacts/app\n", StringComparison.Ordinal),
            "lite-raw-folder" => workflow.Replace(
                "path: artifacts/Dvinholm-Windows-x86-Lite.zip\n",
                "path: artifacts/app-lite\n", StringComparison.Ordinal),
            "wrong-checksum-path" => workflow.Replace(
                "path: artifacts/SHA256SUMS.txt\n",
                "path: artifacts/checksums.txt\n", StringComparison.Ordinal),
            "silent-missing-file" => workflow.Replace(
                "          if-no-files-found: error\n", "",
                StringComparison.Ordinal),
            "double-compress" => workflow.Replace(
                "          compression-level: 0\n",
                "          compression-level: 6\n", StringComparison.Ordinal),
            "missing-package-gate" => workflow.Replace(
                "- name: Package Portable and Lite (exclude debug tools)",
                "- name: Skip archive validation", StringComparison.Ordinal),
            "release-zip-drift" => workflow.Replace(
                "\"artifacts/Dvinholm-Windows-x86.zip\" \"artifacts/Dvinholm-Windows-x86-Lite.zip\" \"artifacts/SHA256SUMS.txt\"",
                "\"artifacts/app\" \"artifacts/Dvinholm-Windows-x86-Lite.zip\" \"artifacts/SHA256SUMS.txt\"",
                StringComparison.Ordinal),
            _ => workflow
        };
        if (expectedValid)
            VerifyWorkflow(workflow);
        else
            Assert.Throws<InvalidOperationException>(() => VerifyWorkflow(workflow));
    }

    private static void VerifyWorkflow(string workflow)
    {
        var package = Step("Package Portable and Lite (exclude debug tools)");
        var portable = Step("Upload CI artifact");
        var lite = Step("Upload Lite artifact");
        var sums = Step("Upload Development ZIP checksums");
        var release = Step("Publish public CI asset");

        var packageIndex = workflow.IndexOf("- name: Package Portable and Lite (exclude debug tools)",
            StringComparison.Ordinal);
        var firstUploadIndex = workflow.IndexOf("- name: Upload CI artifact",
            StringComparison.Ordinal);
        var releaseIndex = workflow.IndexOf("- name: Publish public CI asset",
            StringComparison.Ordinal);
        Require(packageIndex >= 0 && packageIndex < firstUploadIndex &&
                firstUploadIndex < releaseIndex, "Upload must follow final ZIP validation.");
        Require(package.Contains("./eng/Test-WindowsDistributionChecksums.ps1 -Directory artifacts",
            StringComparison.Ordinal), "Checksum verification gate must precede upload.");
        Require(package.Contains("./eng/Test-WindowsDistributionExtraction.ps1 -Flavor Lite",
            StringComparison.Ordinal), "Extracted Lite smoke must precede upload.");

        CheckUpload(portable, "Dvinholm-Windows-x86", "Dvinholm-Windows-x86.zip", true);
        CheckUpload(lite, "Dvinholm-Windows-x86-Lite",
            "Dvinholm-Windows-x86-Lite.zip", true);
        CheckUpload(sums, "Dvinholm-Distribution-Checksums", "SHA256SUMS.txt", false);

        Require(release.Contains(
            "\"artifacts/Dvinholm-Windows-x86.zip\" \"artifacts/Dvinholm-Windows-x86-Lite.zip\" \"artifacts/SHA256SUMS.txt\"",
            StringComparison.Ordinal),
            "Public prerelease assets must match uploaded final ZIPs and manifest.");

        string Step(string name)
        {
            var label = "- name: " + name;
            var pos = workflow.IndexOf(label, StringComparison.Ordinal);
            Require(pos >= 0 && workflow.IndexOf(label, pos + label.Length,
                StringComparison.Ordinal) < 0, "Missing or duplicate workflow step: " + name);
            var end = workflow.IndexOf("\n      - name:", pos + label.Length,
                StringComparison.Ordinal);
            return workflow[pos..(end < 0 ? workflow.Length : end)];
        }
    }

    private static void CheckUpload(
        string step, string artifactName, string fileName, bool zipped)
    {
        Require(step.Contains("uses: actions/upload-artifact@v7", StringComparison.Ordinal),
            "Actions upload action is required.");
        Require(step.Contains("\n          name: " + artifactName + "\n",
            StringComparison.Ordinal), "Unexpected artifact name: " + artifactName);
        Require(step.Contains("\n          path: artifacts/" + fileName + "\n",
            StringComparison.Ordinal), "Upload must contain final file: " + fileName);
        Require(step.Contains("\n          if-no-files-found: error",
            StringComparison.Ordinal), "Missing asset must fail CI.");
        if (zipped)
            Require(step.Contains("\n          compression-level: 0",
                StringComparison.Ordinal), "Precompressed ZIP must not be compressed twice.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static string FindWorkflow()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory);
             directory != null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName,
                ".github", "workflows", "ci.yml");
            if (File.Exists(path)) return path;
        }
        throw new FileNotFoundException("Cannot find repository CI workflow.");
    }
}
