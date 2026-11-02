using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Dvinholm.UpdateHandoffProbe;

/// <summary>
/// Non-shipping, standalone Windows x64 process-exit contract probe.
/// It CANNOT replace, extract, execute, restart or update an application.
/// Writes one receipt inside a guarded temporary test fixture after the
/// exact original parent process handle has reached the exited state.
/// </summary>
internal static class Program
{
    private const string Prefix = "dvinholm-recovery-fixture-";
    private const string MarkerFile = ".dvinholm-offline-fixture";
    private const string MarkerValue = "Dvinholm recovery simulation v1";
    private const string ReceiptName = "fixture-handoff-ready.txt";

    private static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.Length != 5 || args[0] != "--fixture-probe")
                throw new ArgumentException("Only --fixture-probe PID START_TICKS TIMEOUT_MS FOLDER is supported.");

            if (!int.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out var pid) ||
                pid <= 0 || pid == Environment.ProcessId ||
                !long.TryParse(args[2], NumberStyles.None, CultureInfo.InvariantCulture, out var startTicks) ||
                startTicks <= 0 ||
                !int.TryParse(args[3], NumberStyles.None, CultureInfo.InvariantCulture, out var timeoutMs) ||
                timeoutMs is < 1 or > 30_000)
                throw new ArgumentException("Invalid process identity or timeout.");

            var root = GuardFixture(args[4]);
            using var process = Process.GetProcessById(pid);
            // Process.GetProcessById opens a process handle. Validate its
            // creation time, not just the potentially recycled integer PID.
            if (process.StartTime.ToUniversalTime().Ticks != startTicks)
                throw new InvalidOperationException("Process creation identity mismatch.");

            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs));
            await process.WaitForExitAsync(cts.Token);
            cts.Token.ThrowIfCancellationRequested();
            if (!process.HasExited)
                throw new IOException("Process exit not confirmed.");

            GuardFixture(root);
            if (process.StartTime.ToUniversalTime().Ticks != startTicks)
                throw new InvalidOperationException("Process identity changed.");

            var receipt = Path.Combine(root, ReceiptName);
            var content = "Fixture-only process exit confirmed\n" +
                "PID: " + pid.ToString(CultureInfo.InvariantCulture) + "\n" +
                "StartUtcTicks: " + startTicks.ToString(CultureInfo.InvariantCulture) + "\n";
            // Never overwrite an existing receipt and never write to an
            // application installation or a user-configurable arbitrary path.
            using (var file = new FileStream(receipt, FileMode.CreateNew,
                FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                file.Write(Encoding.ASCII.GetBytes(content));
                file.Flush(flushToDisk: true);
            }
            Console.WriteLine("OFFLINE_FIXTURE_EXIT_CONFIRMED");
            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("OFFLINE_FIXTURE_TIMEOUT");
            return 3;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or
                                   IOException or UnauthorizedAccessException or
                                   System.ComponentModel.Win32Exception)
        {
            Console.Error.WriteLine("OFFLINE_FIXTURE_REFUSED: " + ex.GetType().Name);
            return 2;
        }
    }

    private static string GuardFixture(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Missing fixture path.");
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var temp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
        var leaf = Path.GetFileName(root);
        if (!string.Equals(Path.GetDirectoryName(root), temp, StringComparison.OrdinalIgnoreCase) ||
            !leaf.StartsWith(Prefix, StringComparison.Ordinal) ||
            !Regex.IsMatch(leaf, @"^dvinholm-recovery-fixture-[a-f0-9]{32}$",
                RegexOptions.CultureInvariant))
            throw new InvalidOperationException("Not a dedicated temp fixture.");

        for (DirectoryInfo? parent = new(root); parent is not null; parent = parent.Parent)
        {
            if (!parent.Exists || (parent.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Missing or linked fixture path.");
        }
        var marker = Path.Combine(root, MarkerFile);
        var info = new FileInfo(marker);
        if (!info.Exists || (info.Attributes & FileAttributes.ReparsePoint) != 0 ||
            info.Length != MarkerValue.Length ||
            File.ReadAllText(marker, Encoding.ASCII) != MarkerValue)
            throw new InvalidOperationException("Fixture marker invalid.");
        return root;
    }
}
