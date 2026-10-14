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
        @"SOFTWARE\PassThruSupport.04.04",
        @"SOFTWARE\PassThruSupport.02.02"
    ];

    internal static IReadOnlyList<J2534Device> FindRegisteredDevices()
    {
        var devices = new List<J2534Device>();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
            foreach (var registryPath in RegistryKeys)
            {
                using var root = machine.OpenSubKey(registryPath);
                if (root is null)
                    continue;

                foreach (var subKeyName in root.GetSubKeyNames())
                {
                    try
                    {
                        using var subKey = root.OpenSubKey(subKeyName);
                        if (subKey is null)
                            continue;

                        var name = ReadString(subKey, "Name");
                        var vendor = ReadString(subKey, "Vendor");
                        var dllPath = ReadString(subKey, "FunctionLibrary");
                        if (string.IsNullOrWhiteSpace(dllPath))
                            dllPath = ReadString(subKey, "FunctionLibraryLocation");

                        if (!TryCreateDevice(subKeyName, name, vendor, dllPath, registryPath, out var device))
                            continue;

                        if (!File.Exists(device.DllPath) || !seenPaths.Add(device.DllPath))
                            continue;

                        devices.Add(device);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // One inaccessible vendor key should not hide other adapters.
                    }
                    catch (IOException)
                    {
                        // Registry can change while vendor software is being installed.
                    }
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Report an empty list; the UI remains usable for manual DLL selection.
        }
        catch (IOException)
        {
            // Report an empty list; the UI remains usable for manual DLL selection.
        }

        return devices
            .OrderBy(device => device.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(device => device.Vendor, StringComparer.OrdinalIgnoreCase)
            .ToArray();
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
