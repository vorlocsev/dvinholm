namespace Dvinholm.App.Tests;

/// <summary>
/// Eight synthetic UI gating checks. No vehicle or physical J2534 is used.
/// </summary>
public sealed class EcuSnapshotCaptureUiGateTests
{
    private static EcuProfile Profile(params ushort[] dids) =>
        new("offline.ecm", "Synthetic ECM", 0x7E0, 0x7E8, false, dids,
            "Synthetic test only", false);

    private static bool Allowed(EcuProfile? profile,
        bool connected = true, bool acceptance = false, bool busy = false,
        uint? requestId = 0x7E0, uint? responseId = 0x7E8, bool? extended = false) =>
        EcuSnapshotCaptureUiGate.CanCapture(profile, connected, acceptance, busy,
            requestId, responseId, extended);

    [Fact]
    public void MatchingSelectedProfileCanReadDids() =>
        Assert.True(Allowed(Profile(0xF187, 0xF190)));

    [Fact]
    public void DisconnectedSessionNeverReads() =>
        Assert.False(Allowed(Profile(0xF187), connected: false));

    [Fact]
    public void OfflineAcceptanceCannotAccessPhysicalAdapter() =>
        Assert.False(Allowed(Profile(0xF187), acceptance: true));

    [Fact]
    public void ActiveDiagnosticOrDiscoveryCannotOverlapCapture() =>
        Assert.False(Allowed(Profile(0xF187), busy: true));

    [Fact]
    public void ProfileIsMandatoryAndMustContainDids()
    {
        Assert.False(Allowed(null));
        Assert.False(Allowed(Profile()));
    }

    [Fact]
    public void EditedCanRequestOrResponseCannotRedirectCapture()
    {
        Assert.False(Allowed(Profile(0xF187), requestId: 0x7E1));
        Assert.False(Allowed(Profile(0xF187), responseId: 0x7E9));
        Assert.False(Allowed(Profile(0xF187), requestId: null));
        Assert.False(Allowed(Profile(0xF187), responseId: null));
    }

    [Fact]
    public void WrongOrMissingFrameFormatCannotRedirectCapture()
    {
        Assert.False(Allowed(Profile(0xF187), extended: true));
        Assert.False(Allowed(Profile(0xF187), extended: null));
    }

    [Fact]
    public void MoreThan128DidsMustNotBeReadFromUi()
    {
        var dids = Enumerable.Range(0, 129).Select(x => (ushort)(0xF100 + x)).ToArray();
        Assert.False(Allowed(Profile(dids)));
    }
}
