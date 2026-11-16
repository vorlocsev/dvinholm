namespace Dvinholm.App.Tests;

public sealed class CemPinProgressPresenterTests
{
    private static CemPinProgressSnapshot Snapshot(
        bool development = true,
        bool connected = true,
        bool ready = true,
        bool observed = true,
        bool source = true,
        bool range = true,
        bool running = false,
        bool candidate = false,
        bool saved = false,
        bool revealed = false) =>
        new(development, connected, ready, observed, source, range, running, candidate, saved, revealed);

    [Fact]
    public void StableNeverClaimsPinAvailabilityEvenWithContradictoryInput()
    {
        var state = Snapshot(development: false, candidate: true, revealed: true);
        var ru = CemPinProgressPresenter.Format(state, false);
        var en = CemPinProgressPresenter.Format(state, true);
        Assert.Contains("недоступен", ru.Search);
        Assert.Contains("unavailable", en.Search);
        Assert.Equal("Нет результата", ru.Result);
        Assert.False(en.PhysicalPinVerified);
        Assert.False(en.MayWriteConfiguration);
    }

    [Fact]
    public void DisconnectedDevelopmentNeverClaimsExperimentReady()
    {
        var output = CemPinProgressPresenter.Format(Snapshot(connected: false), false);
        Assert.Equal("J2534 не подключён", output.Search);
    }

    [Fact]
    public void MissingSessionPrerequisitesAreVisibleWithoutSendingRequests()
    {
        var output = CemPinProgressPresenter.Format(Snapshot(ready: false), true);
        Assert.Equal("Session prerequisites unmet", output.Search);
    }

    [Fact]
    public void ReadOnlyPartNumberObservationIsNotAuthenticatedIdentity()
    {
        var output = CemPinProgressPresenter.Format(Snapshot(observed: false), true);
        Assert.Equal("CEM P/N not read", output.Identity);
        Assert.Equal("Read CEM P/N first", output.Search);
        var seen = CemPinProgressPresenter.Format(Snapshot(), true);
        Assert.Equal("P/N observed, not attested", seen.Identity);
        Assert.False(seen.PhysicalPinVerified);
    }

    [Fact]
    public void UnknownSourceAndInvalidRangeBlockReadinessDisplay()
    {
        Assert.Equal("Source route unverified",
            CemPinProgressPresenter.Format(Snapshot(source: false), true).Search);
        Assert.Equal("Check attempt range",
            CemPinProgressPresenter.Format(Snapshot(range: false), true).Search);
    }

    [Fact]
    public void PreflightSuccessIsStillNotPhysicalAcceptance()
    {
        var copy = CemPinProgressPresenter.Format(Snapshot(), false);
        Assert.Contains("не проверен", copy.Search);
        Assert.False(copy.PhysicalPinVerified);
        Assert.False(copy.MayWriteConfiguration);
    }

    [Fact]
    public void RunningStateNeverClaimsCompletedPin()
    {
        var copy = CemPinProgressPresenter.Format(Snapshot(running: true), true);
        Assert.Equal("Experiment running", copy.Search);
        Assert.Equal("No result", copy.Result);
    }

    [Fact]
    public void AdapterCandidateRemainsUnverifiedWhenHiddenOrRevealed()
    {
        var hidden = CemPinProgressPresenter.Format(Snapshot(candidate: true), true);
        var shown = CemPinProgressPresenter.Format(
            Snapshot(candidate: true, revealed: true), true);
        Assert.Equal("Unverified candidate hidden", hidden.Result);
        Assert.Equal("Unverified candidate temporarily shown", shown.Result);
        Assert.False(shown.PhysicalPinVerified);
    }

    [Fact]
    public void SavedCandidateIsDistinctFromLiveAdapterObservation()
    {
        var hidden = CemPinProgressPresenter.Format(
            Snapshot(connected: false, observed: false, ready: false,
                candidate: true, saved: true), false);
        var shown = CemPinProgressPresenter.Format(
            Snapshot(candidate: true, saved: true, revealed: true), true);
        Assert.Equal("Сохранённый кандидат скрыт", hidden.Result);
        Assert.Equal("Saved candidate temporarily shown", shown.Result);
        Assert.False(shown.MayWriteConfiguration);
    }

    [Fact]
    public void SnapshotHasNoSensitiveBytesAndIsPresentationOnly()
    {
        var names = typeof(CemPinProgressSnapshot).GetProperties().Select(x => x.Name).ToArray();
        Assert.Equal(10, names.Length);
        Assert.All(typeof(CemPinProgressSnapshot).GetProperties(),
            property => Assert.Equal(typeof(bool), property.PropertyType));
        var src = typeof(CemPinProgressPresenter).GetMethod("Format",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(src);
    }
}
