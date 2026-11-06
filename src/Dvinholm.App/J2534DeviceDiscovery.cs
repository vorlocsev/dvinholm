using Microsoft.Win32;

namespace Dvinholm.App;

internal sealed record J2534Device(string Name, string Vendor, string DllPath, string RegistryKey)
{
    public string Label => string.IsNullOrWhiteSpace(Vendor) || string.Equals(Name, Vendor, StringComparison.OrdinalIgnoreCase)
        ? Name
        : $"{Name} — {Vendor}";
}

internal static class J2534DeviceDiscovery
{
    private static readonly string[] RegistryKeys =
    [
        @"SOFTWARE\PassThruSupport.05.00",
        @"SOFTWARE\PassThruSupport.04.04",
        @"SOFTWARE\PassThruSupport.02.02"
    ];

    // The native PassThru DLL is loaded in-process. An x64 app MUST NOT
    // list x86-only registry entries as if they were directly usable.
    internal static RegistryView RegistryViewForProcess(bool is64BitProcess) =>
        is64BitProcess ? RegistryView.Registry64 : RegistryView.Registry32;

    // An audit examines both registry views without opening a J2534 device.
    // Registered does not imply installed, usable, trusted, or compatible.
    internal sealed record RegisteredAudit(J2534Device Device, RegistryView View,
        J2534PeArchitecture Architecture);

    internal static IReadOnlyList<RegisteredAudit> AuditRegisteredDevices()
    {
        var found = new List<RegisteredAudit>();
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                foreach (var registryPath in RegistryKeys)
                {
                    // A corrupt or inaccessible vendor key cannot hide other keys.
                    IgnoreRegistryRootFailure(() =>
                    {
                        using var root = machine.OpenSubKey(registryPath);
                        if (root is null) return;
                        foreach (var subKeyName in root.GetSubKeyNames())
                        {
                            try
                            {
                                using var subKey = root.OpenSubKey(subKeyName);
                                if (subKey is null) continue;
                                var name = ReadString(subKey, "Name");
                                var vendor = ReadString(subKey, "Vendor");
                                var dllPath = ReadString(subKey, "FunctionLibrary");
                                if (string.IsNullOrWhiteSpace(dllPath))
                                    dllPath = ReadString(subKey, "FunctionLibraryLocation");
                                if (!TryCreateDevice(subKeyName, name, vendor, dllPath,
                                        registryPath, out var device) ||
                                    !File.Exists(device.DllPath) ||
                                    !seenPaths.Add(device.DllPath))
                                    continue;
                                found.Add(new RegisteredAudit(device, view,
                                    J2534PeInspector.Inspect(device.DllPath)));
                            }
                            catch (UnauthorizedAccessException) { }
                            catch (IOException) { }
                            catch (System.Security.SecurityException) { }
                        }
                    });
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
            catch (System.Security.SecurityException) { }
            // A 32-bit Windows host may not expose Registry64 at all.
            // Preserve legacy x86 discovery instead of failing the scan.
            catch (PlatformNotSupportedException) { }
            catch (ArgumentException) { }
        }
        return found;
    }

    // A 32-bit registry entry or DLL must NEVER be offered as a loadable
    // adapter for the self-contained x64 app. The same holds in reverse for
    // old x86 installations. The audit still keeps those entries visible.
    internal static IReadOnlyList<J2534Device> SelectCompatible(
        IEnumerable<RegisteredAudit> audit, bool is64BitProcess) =>
        audit.Where(entry => entry.View == RegistryViewForProcess(is64BitProcess) &&
                J2534PeInspector.CanLoad(entry.Architecture, is64BitProcess))
            .Select(entry => entry.Device)
            .DistinctBy(device => device.DllPath, StringComparer.OrdinalIgnoreCase)
            .OrderBy(device => device.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(device => device.Vendor, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    internal static IReadOnlyList<J2534Device> FindRegisteredDevices() =>
        SelectCompatible(AuditRegisteredDevices(), Environment.Is64BitProcess);

    internal static void IgnoreRegistryRootFailure(Action readRoot)
    {
        try
        {
            readRoot();
        }
        catch (UnauthorizedAccessException)
        {
            // Continue with other registry roots.
        }
        catch (IOException)
        {
            // Continue with other registry roots.
        }
        catch (System.Security.SecurityException)
        {
            // Continue with other registry roots.
        }
    }

    internal static bool TryCreateDevice(
        string registryName,
        string? name,
        string? vendor,
        string? dllPath,
        string registryPath,
        out J2534Device device)
    {
        device = new J2534Device(string.Empty, string.Empty, string.Empty, string.Empty);

        if (string.IsNullOrWhiteSpace(dllPath))
            return false;

        // Registry values are sometimes quoted. Normalize the outside quotes
        // and any whitespace inside them, then reject values that become empty.
        var normalizedPath = Environment.ExpandEnvironmentVariables(dllPath.Trim().Trim('"')).Trim();
        if (string.IsNullOrWhiteSpace(normalizedPath))
            return false;

        var displayName = string.IsNullOrWhiteSpace(name) ? registryName : name.Trim();
        var displayVendor = vendor?.Trim() ?? string.Empty;

        device = new J2534Device(displayName, displayVendor, normalizedPath, registryPath + @"\" + registryName);
        return true;
    }

    private static string? ReadString(RegistryKey key, string valueName)
    {
        var value = key.GetValue(valueName);
        return value is string text ? text.Trim() : null;
    }
}
