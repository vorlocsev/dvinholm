namespace Dvinholm.App.Tests;

public sealed class UdsEcuCandidateRequestIdTests
{
    [Theory]
    [InlineData(0x7E0u, false, "7E0")]
    [InlineData(0x18DA10F1u, true, "18DA10F1")]
    public void RequestIdText_FormatsKnownRequestId(uint requestId, bool extended, string expected)
    {
        var candidate = new Dvinholm.App.UdsEcuCandidate(
            extended ? 0x18DAF110u : 0x7E8u,
            requestId,
            extended,
            ResponseCount: 1,
            MaxDataLength: 8);

        Assert.Equal(expected, candidate.RequestIdText);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RequestIdText_IsEmptyWhenRequestIdCannotBeInferred(bool extended)
    {
        var candidate = new Dvinholm.App.UdsEcuCandidate(
            extended ? 0x18DAF111u : 0x700u,
            RequestId: null,
            extended,
            ResponseCount: 1,
            MaxDataLength: 8);

        Assert.Equal(string.Empty, candidate.RequestIdText);
    }
}
