namespace Dvinholm.App;

/// <summary>
/// Counts mutually exclusive RESEARCH dispositions for changed raw byte
/// ordinals. It does not count catalog rows, authenticate a CEM or identify
/// manufacturer option meanings. Incompatible claims are not OEM failures.
/// </summary>
internal sealed record CemConfigurationRevisionResearchStatistics(
    int ExactUnverifiedClaimCount,
    int OtherSoftwareRevisionClaimCount,
    int OtherHardwarePartClaimCount,
    int MissingResearchClaimCount,
    int InvalidClaimedScopeCount)
{
    internal int ChangedOrdinalCount =>
        ExactUnverifiedClaimCount + OtherSoftwareRevisionClaimCount +
        OtherHardwarePartClaimCount + MissingResearchClaimCount +
        InvalidClaimedScopeCount;

    internal int IncompatibleClaimScopeCount =>
        OtherSoftwareRevisionClaimCount + OtherHardwarePartClaimCount;

    internal bool HasVerifiedOptionMeanings => false;
    internal bool HasAuthenticatedPhysicalCem => false;
    internal bool MayReadOrWriteVehicle => false;

    internal static CemConfigurationRevisionResearchStatistics Count(
        IReadOnlyList<CemConfigurationRevisionResearchRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var totals = new int[5];
        foreach (var row in rows)
        {
            // Explicit enum gate: unknown statuses must never be silently
            // omitted from the sum (or reported as compatible).
            if (!Enum.IsDefined(row.Disposition))
                throw new InvalidDataException(
                    "Unknown CEM research disposition in changed ordinals.");
            totals[(int)row.Disposition] = checked(totals[(int)row.Disposition] + 1);
        }

        return new(
            totals[(int)CemConfigurationRevisionResearchDisposition.ExactUnverifiedUserClaim],
            totals[(int)CemConfigurationRevisionResearchDisposition.OnlyOtherSoftwareRevisionClaim],
            totals[(int)CemConfigurationRevisionResearchDisposition.OnlyOtherHardwarePartClaim],
            totals[(int)CemConfigurationRevisionResearchDisposition.NoClaimForChangedOrdinal],
            totals[(int)CemConfigurationRevisionResearchDisposition.AssertedScopeMissingOrInvalid]);
    }
}
