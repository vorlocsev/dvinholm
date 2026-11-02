using System.Diagnostics;

namespace Dvinholm.App;

/// <summary>
/// Demonstration of post-process-exit sequencing, strictly confined to the
/// synthetic temp fixture used by UnifiedX64RecoverySandbox. NOT a production
/// updater, NOT an installed app replacement or an externally callable helper.
/// </summary>
internal static class UnifiedX64PostExitHandoff
{
    internal static async Task WaitAndApplyFixtureAsync(
        string fixtureRoot, Process appProcess, TimeSpan timeout,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(appProcess);
        if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromSeconds(30))
            throw new ArgumentOutOfRangeException(nameof(timeout));

        // Inspect the safe fixture and journal BEFORE waiting. This call is
        // purposefully also made after waiting, to catch mid-wait tampering.
        EnsureReadyFixture(fixtureRoot);
        if (appProcess.Id == Environment.ProcessId)
            throw new InvalidOperationException("The updater must not wait for itself.");

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(timeout);

        // Await a specific open process handle rather than a reused PID. On
        // cancellation or timeout no fixture bytes are changed.
        await appProcess.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
        deadline.Token.ThrowIfCancellationRequested();
        if (!appProcess.HasExited)
            throw new IOException("Application did not terminate.");

        EnsureReadyFixture(fixtureRoot);
        UnifiedX64RecoverySandbox.ApplyFixture(fixtureRoot);
    }

    private static void EnsureReadyFixture(string root)
    {
        // ReadState rejects unmarked fixtures, arbitrary paths, junction
        // ancestors and malformed or inconsistent journal schema.
        if (UnifiedX64RecoverySandbox.ReadState(root) !=
            UnifiedX64RecoverySandbox.StateReady)
            throw new InvalidDataException("Fixture journal is not ready for post-exit handoff.");
    }
}
