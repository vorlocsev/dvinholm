namespace Dvinholm.App.Tests;

public sealed class LatestBuildUpdaterExpandedSizeTests
{
    [Fact]
    public void ExpandedArchiveOverLimit_IsRejectedBeforeExtraction()
    {
        // Test the aggregate-size guard directly, without inconsistent ZIP metadata
        // or allocating a huge payload.
        Assert.Throws<InvalidDataException>(() =>
            LatestBuildUpdater.ValidateExpandedSize([12, 1_048_576_000, 1]));
    }

    [Fact]
    public void ExpandedArchiveWithinLimit_IsAccepted()
    {
        LatestBuildUpdater.ValidateExpandedSize([500_000_000, 548_576_000]);
    }

    [Fact]
    public void NegativeEntrySize_IsRejected()
    {
        Assert.Throws<InvalidDataException>(() =>
            LatestBuildUpdater.ValidateExpandedSize([10, -1]));
    }

    [Fact]
    public void AggregateOverflow_IsRejectedAsInvalidDataException()
    {
        Assert.Throws<InvalidDataException>(() =>
            LatestBuildUpdater.ValidateExpandedSize([1, long.MaxValue]));
    }
}
