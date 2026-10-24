namespace Dvinholm.App.Tests;

/// <summary>Eight fail-closed checks for real v0.2.3 LTSC user-data isolation.</summary>
public sealed class AcceptanceUserDataIsolationTests
{
    private const string App = @"C:\acceptance\Dvinholm-023-acceptance";
    private const string Local = @"C:\Users\Test\AppData\Local";
    private static string Private => Path.Combine(App, "acceptance-user-data");

    [Fact]
    public void NormalInstallationUsesTheUnchangedUserCatalogDirectory() =>
        Assert.Equal(Path.Combine(Local, "Dvinholm"),
            DvinholmUserDataDirectory.Resolve(App, Local, null, false));

    [Fact]
    public void VerifiedOneClickAcceptanceUsesItsOwnDataDirectory() =>
        Assert.Equal(Private, DvinholmUserDataDirectory.Resolve(App, Local, Private, true));

    [Fact]
    public void DirectExeRestartWithTheReceiptRemainsIsolated() =>
        Assert.Equal(Private, DvinholmUserDataDirectory.Resolve(App, Local, null, true));

    [Fact]
    public void ExplicitOverrideWithoutReceiptFailsRatherThanUsingRealSettings() =>
        Assert.Throws<InvalidOperationException>(() =>
            DvinholmUserDataDirectory.Resolve(App, Local, Private, false));

    [Fact]
    public void RejectsTheAlreadyInstalledProductionDataDirectory() =>
        Assert.Throws<InvalidOperationException>(() =>
            DvinholmUserDataDirectory.Resolve(App, Local, Path.Combine(Local, "Dvinholm"), true));

    [Fact]
    public void RejectsAnyAbsoluteLocationOutsideTheReservedAcceptanceFolder() =>
        Assert.Throws<InvalidOperationException>(() =>
            DvinholmUserDataDirectory.Resolve(App, Local, @"C:\acceptance\unrelated", true));

    [Fact]
    public void RejectsRelativeAcceptanceDataPath() =>
        Assert.Throws<InvalidOperationException>(() =>
            DvinholmUserDataDirectory.Resolve(App, Local, "acceptance-user-data", true));

    [Fact]
    public void RejectsBlankAcceptancePath() =>
        Assert.Throws<InvalidOperationException>(() =>
            DvinholmUserDataDirectory.Resolve(App, Local, " ", true));
}
