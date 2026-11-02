using System.IO.Compression;
using System.Security.Cryptography;

namespace Dvinholm.App.Tests;

public sealed class UnifiedX64ExtractionSandboxTests
{
    private static byte[] Exe(ushort machine = 0x8664)
    {
        var bytes = new byte[1024];
        bytes[0] = 77; bytes[1] = 90;
        BitConverter.GetBytes(128).CopyTo(bytes, 60);
        bytes[128] = 80; bytes[129] = 69;
        BitConverter.GetBytes(machine).CopyTo(bytes, 132);
        return bytes;
    }

    private static void Put(ZipArchive archive, string name, byte[] payload)
    {
        using var stream = archive.CreateEntry(name).Open();
        stream.Write(payload);
    }

    private static void InFixture(Action<string, UnifiedX64StagedPackage> test,
        Action<ZipArchive>? inject = null, ushort machine = 0x8664)
    {
        var root = Path.Combine(Path.GetTempPath(),
            "dvinholm-extract-fixture-" + Guid.NewGuid().ToString("N"));
        UnifiedX64ExtractionSandbox.MarkForTests(root);
        try
        {
            var pending = Path.Combine(root, "pending-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(pending);
            var zipPath = Path.Combine(pending, UnifiedX64ReleaseFeed.PackageAsset);
            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                Put(archive, "Dvinholm.exe", Exe(machine));
                Put(archive, "coreclr.dll", [1, 2]);
                Put(archive, "hostfxr.dll", [3]);
                Put(archive, "Microsoft.UI.Xaml.dll", [4, 5]);
                inject?.Invoke(archive);
            }
            var zipBytes = new FileInfo(zipPath).Length;
            var hash = Convert.ToHexString(
                SHA256.HashData(File.ReadAllBytes(zipPath))).ToLowerInvariant();
            int files;
            long total;
            using (var archive = ZipFile.OpenRead(zipPath))
            {
                files = archive.Entries.Count;
                total = archive.Entries.Sum(entry => entry.Length);
            }
            var stage = new UnifiedX64StagedPackage(pending,
                zipPath, hash, zipBytes, files, total);
            test(root, stage);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void CompleteExtractionCopiesOnlyVerifiedX64PayloadIntoScratch()
    {
        InFixture((root, stage) =>
        {
            var receipt = UnifiedX64ExtractionSandbox.ExtractFixture(stage, root);
            Assert.True(UnifiedX64ExtractionSandbox.VerifyFixture(receipt, stage, root));
            Assert.Equal(4, receipt.Files);
            Assert.Equal(1024 + 2 + 1 + 2, receipt.Bytes);
            Assert.Equal(Exe(), File.ReadAllBytes(
                Path.Combine(receipt.Directory, "Dvinholm.exe")));
            Assert.Equal(1, Directory.EnumerateDirectories(root, "expanded-*").Count());
            Assert.Empty(Directory.EnumerateDirectories(root, ".extract-*"));
        });
    }

    [Fact]
    public void ModifiedExtractedFileCannotPassTheByteForByteAudit()
    {
        InFixture((root, stage) =>
        {
            var receipt = UnifiedX64ExtractionSandbox.ExtractFixture(stage, root);
            File.WriteAllBytes(Path.Combine(receipt.Directory, "coreclr.dll"), [9, 9]);
            Assert.Throws<InvalidDataException>(() =>
                UnifiedX64ExtractionSandbox.VerifyFixture(receipt, stage, root));
        });
    }

    [Fact]
    public void ChangedZipIsRejectedBeforeExtractionOrDirectoryCreation()
    {
        InFixture((root, stage) =>
        {
            File.AppendAllText(stage.ZipPath, "tamper");
            Assert.Throws<InvalidDataException>(() =>
                UnifiedX64ExtractionSandbox.ExtractFixture(stage, root));
            Assert.Empty(Directory.EnumerateDirectories(root, ".extract-*"));
            Assert.Empty(Directory.EnumerateDirectories(root, "expanded-*"));
        });
    }

    [Fact]
    public void PathTraversalAndAbsoluteOrAlternatePathNamesCannotEscapeFixture()
    {
        foreach (var name in new[] {
            "../escaped.dat", "folder/../../escaped.dat", "C:/override.txt", "/root.txt"
        })
            InFixture((root, stage) =>
            {
                Assert.Throws<InvalidDataException>(() =>
                    UnifiedX64ExtractionSandbox.ExtractFixture(stage, root));
                Assert.Empty(Directory.EnumerateDirectories(root, "expanded-*"));
            }, zip => Put(zip, name, [1]));
    }

    [Fact]
    public void DuplicateCaseInsensitiveArchiveEntryIsRefused()
    {
        InFixture((root, stage) =>
        {
            Assert.Throws<InvalidDataException>(() =>
                UnifiedX64ExtractionSandbox.ExtractFixture(stage, root));
        }, zip => Put(zip, "CORECLR.DLL", [0]));
    }

    [Fact]
    public void SymlinkAndWindowsDeviceAliasAreNeverExtracted()
    {
        InFixture((root, stage) =>
        {
            Assert.Throws<InvalidDataException>(() =>
                UnifiedX64ExtractionSandbox.ExtractFixture(stage, root));
        }, zip =>
        {
            var link = zip.CreateEntry("symlink.dat");
            link.ExternalAttributes = unchecked((int)0xA0000000);
        });

        InFixture((root, stage) =>
        {
            Assert.Throws<InvalidDataException>(() =>
                UnifiedX64ExtractionSandbox.ExtractFixture(stage, root));
        }, zip => Put(zip, "CON.txt", [1, 2]));
    }

    [Fact]
    public void RenamedX86ZipFailsBeforeWritingExpandedFiles()
    {
        InFixture((root, stage) =>
        {
            Assert.Throws<InvalidDataException>(() =>
                UnifiedX64ExtractionSandbox.ExtractFixture(stage, root));
            Assert.Empty(Directory.EnumerateDirectories(root, "expanded-*"));
        }, machine: 0x14c);
    }

    [Fact]
    public void MissingMarkerOrForeignStagingFolderRefusesExtraction()
    {
        InFixture((root, stage) =>
        {
            var wrong = stage with {
                Directory = Path.GetDirectoryName(stage.Directory)!
            };
            Assert.Throws<InvalidDataException>(() =>
                UnifiedX64ExtractionSandbox.ExtractFixture(wrong, root));
            File.Delete(Path.Combine(root, ".dvinholm-offline-extract"));
            Assert.Throws<InvalidOperationException>(() =>
                UnifiedX64ExtractionSandbox.ExtractFixture(stage, root));
            Assert.Empty(Directory.EnumerateDirectories(root, "expanded-*"));
        });
    }
}
