using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Dvinholm.App.Tests;

public sealed class UnifiedX64PackageVerifierTests
{
    private const string ZipName = "Dvinholm-Windows-x64.zip";

    private static byte[] Pe(ushort machine)
    {
        var bytes = new byte[512];
        bytes[0] = (byte)'M'; bytes[1] = (byte)'Z';
        BitConverter.GetBytes(0x80).CopyTo(bytes, 0x3C);
        bytes[0x80] = (byte)'P'; bytes[0x81] = (byte)'E';
        BitConverter.GetBytes(machine).CopyTo(bytes, 0x84);
        return bytes;
    }

    private static string Zip(params (string Name, byte[] Bytes)[] entries)
    {
        var file = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".zip");
        using (var zip = ZipFile.Open(file, ZipArchiveMode.Create))
        {
            foreach (var (name, content) in entries)
            {
                var entry = zip.CreateEntry(name);
                using var stream = entry.Open();
                stream.Write(content);
            }
        }
        return file;
    }

    private static (string Name, byte[] Bytes)[] Normal(ushort machine = 0x8664) =>
    [
        ("Dvinholm.exe", Pe(machine)),
        ("coreclr.dll", new byte[] { 1 }),
        ("hostfxr.dll", new byte[] { 2 }),
        ("Microsoft.UI.Xaml.dll", new byte[] { 3 })
    ];

    private static string Manifest(string file) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant() +
        "  " + ZipName + "\n";

    private static void WithZip(Action<string> act, params (string Name, byte[] Bytes)[] entries)
    {
        var path = Zip(entries);
        try { act(path); }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ValidAmd64SelfContainedArchivePassesOffline()
    {
        WithZip(path =>
        {
            var evidence = UnifiedX64PackageVerifier.Verify(
                path, Manifest(path), new FileInfo(path).Length);
            Assert.Equal(4, evidence.EntryCount);
            Assert.Equal(515, evidence.ExpandedBytes);
            Assert.Equal(64, evidence.Sha256.Length);
        }, Normal());
    }

    [Fact]
    public void WrongDigestNeverPasses()
    {
        WithZip(path =>
        {
            var corrupt = new string('0', 64) + "  " + ZipName;
            Assert.Throws<InvalidDataException>(() =>
                UnifiedX64PackageVerifier.Verify(path, corrupt, new FileInfo(path).Length));
        }, Normal());
    }

    [Fact]
    public void WrongAdvertisedPackageSizeNeverPasses()
    {
        WithZip(path => Assert.Throws<InvalidDataException>(() =>
            UnifiedX64PackageVerifier.Verify(path, Manifest(path), new FileInfo(path).Length + 1)),
            Normal());
    }

    [Theory]
    [InlineData("../outside.dll")]
    [InlineData("folder/../../outside")]
    [InlineData("C:/path.dll")]
    [InlineData("A\\outside")]
    [InlineData("/absolute/path")]
    public void TraversalAndAbsoluteEntriesAreRejected(string name)
    {
        WithZip(path => Assert.Throws<InvalidDataException>(() =>
            UnifiedX64PackageVerifier.Verify(path, Manifest(path), new FileInfo(path).Length)),
            [..Normal(), (name, new byte[] { 1 })]);
    }

    [Fact]
    public void DuplicateCaseInsensitiveEntriesAreRejected()
    {
        WithZip(path => Assert.Throws<InvalidDataException>(() =>
            UnifiedX64PackageVerifier.Verify(path, Manifest(path), new FileInfo(path).Length)),
            [..Normal(), ("CORECLR.DLL", new byte[] { 1 })]);
    }

    [Fact]
    public void RenamedX86ExecutableIsNotAnX64Release()
    {
        WithZip(path => Assert.Throws<InvalidDataException>(() =>
            UnifiedX64PackageVerifier.Verify(path, Manifest(path), new FileInfo(path).Length)),
            Normal(0x14c));
    }

    [Fact]
    public void MissingNativeRuntimeOrExtraLauncherIsRejected()
    {
        var withoutRuntime = Normal().Where(x => x.Name != "Microsoft.UI.Xaml.dll").ToArray();
        WithZip(path => Assert.Throws<InvalidDataException>(() =>
            UnifiedX64PackageVerifier.Verify(path, Manifest(path), new FileInfo(path).Length)),
            withoutRuntime);
        WithZip(path => Assert.Throws<InvalidDataException>(() =>
            UnifiedX64PackageVerifier.Verify(path, Manifest(path), new FileInfo(path).Length)),
            [..Normal(), ("Launcher.exe", Pe(0x8664))]);
        WithZip(path => Assert.Throws<InvalidDataException>(() =>
            UnifiedX64PackageVerifier.Verify(path, Manifest(path), new FileInfo(path).Length)),
            [..Normal(), ("Dvinholm.UpdateHandoffProbe.exe", Pe(0x8664))]);
    }

    [Fact]
    public void MalformedDuplicateAndWrongArchitectureManifestsFailClosed()
    {
        var valid = new string('a', 64) + "  " + ZipName;
        Assert.Throws<InvalidDataException>(() =>
            UnifiedX64PackageVerifier.GetExpectedDigest(valid + "\n" + valid));
        Assert.Throws<InvalidDataException>(() =>
            UnifiedX64PackageVerifier.GetExpectedDigest(
                new string('a', 64) + "  Dvinholm-Windows-x86.zip"));
        Assert.Throws<InvalidDataException>(() =>
            UnifiedX64PackageVerifier.GetExpectedDigest("NOT A SHA256SUMS FILE"));
        Assert.False(UnifiedX64PackageVerifier.ValidEntryName("folder/./file"));
        Assert.False(UnifiedX64PackageVerifier.ValidEntryName("folder/../file"));
    }
}
