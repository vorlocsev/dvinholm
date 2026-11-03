using Microsoft.Win32;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline architecture checks. No vendor DLL is executed, no driver is
/// installed, and none of these tests opens a J2534 session.
/// </summary>
public sealed class J2534PeInspectorTests
{
    private static void WithPe(ushort machine, Action<string> test,
        int peOffset = 128, bool validDos = true, bool validPe = true)
    {
        var path = Path.Combine(Path.GetTempPath(),
            "dvinholm-j2534-pe-" + Guid.NewGuid().ToString("N") + ".dll");
        var bytes = new byte[512];
        if (validDos) { bytes[0] = (byte)'M'; bytes[1] = (byte)'Z'; }
        BitConverter.GetBytes(peOffset).CopyTo(bytes, 0x3c);
        if (peOffset >= 64 && peOffset < bytes.Length - 6)
        {
            if (validPe) { bytes[peOffset] = (byte)'P'; bytes[peOffset + 1] = (byte)'E'; }
            BitConverter.GetBytes(machine).CopyTo(bytes, peOffset + 4);
        }
        try
        {
            File.WriteAllBytes(path, bytes);
            test(path);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void Amd64DllIsCompatibleOnlyWithX64Process() =>
        WithPe(0x8664, path =>
        {
            var machine = J2534PeInspector.Inspect(path);
            Assert.Equal(J2534PeArchitecture.X64, machine);
            Assert.True(J2534PeInspector.CanLoad(machine, true));
            Assert.False(J2534PeInspector.CanLoad(machine, false));
        });

    [Fact]
    public void I386DllIsCompatibleOnlyWithLegacyX86Process() =>
        WithPe(0x014c, path =>
        {
            var machine = J2534PeInspector.Inspect(path);
            Assert.Equal(J2534PeArchitecture.X86, machine);
            Assert.False(J2534PeInspector.CanLoad(machine, true));
            Assert.True(J2534PeInspector.CanLoad(machine, false));
        });

    [Fact]
    public void Arm64IsRecognizedButNeverClaimedCompatible() =>
        WithPe(0xaa64, path =>
        {
            var machine = J2534PeInspector.Inspect(path);
            Assert.Equal(J2534PeArchitecture.Arm64, machine);
            Assert.False(J2534PeInspector.CanLoad(machine, true));
            Assert.False(J2534PeInspector.CanLoad(machine, false));
        });

    [Fact]
    public void ForgedPeWithoutDosHeaderIsNotLoadable() =>
        WithPe(0x8664, path =>
        {
            Assert.Equal(J2534PeArchitecture.Unknown,
                J2534PeInspector.Inspect(path));
        }, validDos: false);

    [Fact]
    public void InvalidPeSignatureIsNotLoadable() =>
        WithPe(0x8664, path =>
        {
            Assert.Equal(J2534PeArchitecture.Unknown,
                J2534PeInspector.Inspect(path));
        }, validPe: false);

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    [InlineData(1048577)]
    [InlineData(int.MaxValue)]
    public void InvalidPeOffsetsFailClosed(int offset) =>
        WithPe(0x8664, path =>
        {
            Assert.Equal(J2534PeArchitecture.Unknown,
                J2534PeInspector.Inspect(path));
        }, peOffset: offset);

    [Fact]
    public void MissingAndTruncatedDllsNeverPass() 
    {
        Assert.Equal(J2534PeArchitecture.Unknown, J2534PeInspector.Inspect(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".dll")));
        Assert.Equal(J2534PeArchitecture.Unknown, J2534PeInspector.Inspect(""));
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, [0x4d, 0x5a]);
            Assert.Equal(J2534PeArchitecture.Unknown, J2534PeInspector.Inspect(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void RegistryViewAndDllArchitectureMustBothMatch()
    {
        var x64 = new J2534Device("DiCE64", "Vendor", "C:\\drivers\\x64.dll", "key64");
        var x86 = new J2534Device("DiCE32", "Vendor", "C:\\drivers\\x86.dll", "key32");
        var misregistered = new J2534Device("WrongView", "Vendor", "C:\\drivers\\wrong.dll", "key");
        J2534DeviceDiscovery.RegisteredAudit[] audit =
        [
            new(x64, RegistryView.Registry64, J2534PeArchitecture.X64),
            new(x86, RegistryView.Registry32, J2534PeArchitecture.X86),
            new(misregistered, RegistryView.Registry64, J2534PeArchitecture.X86),
            new(x64, RegistryView.Registry64, J2534PeArchitecture.X64)
        ];
        Assert.Equal(new[] { "DiCE64" },
            J2534DeviceDiscovery.SelectCompatible(audit, true).Select(d => d.Name));
        Assert.Equal(new[] { "DiCE32" },
            J2534DeviceDiscovery.SelectCompatible(audit, false).Select(d => d.Name));
    }
}
