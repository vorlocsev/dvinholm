namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline identity-status contracts. All data is synthetic. This
/// suite never touches J2534, the PIN store, VINs or a physical vehicle.
/// </summary>
public sealed class CemIdentityEvidenceReportTests
{
    private static CemIdentityEvidenceAssessment Review(
        uint? partNumber, bool guardCurrent = true,
        bool connected = true, bool development = true) =>
        CemIdentityEvidenceReport.Assess(partNumber, guardCurrent,
            connected, development);

    private static void AssertNoCapabilities(CemIdentityEvidenceAssessment status)
    {
        Assert.False(status.HasAuthenticatedPhysicalCem);
        Assert.False(status.HasVerifiedFirmwareRevision);
        Assert.False(status.MayAcquirePin);
        Assert.False(status.MayReadConfiguration);
        Assert.False(status.MayWriteConfiguration);
    }

    [Fact]
    public void MissingObservationNeverInventsACemIdentity()
    {
        var result = Review(null);
        Assert.Equal(CemIdentityEvidenceKind.NoCurrentEvidence, result.Kind);
        AssertNoCapabilities(result);
    }

    [Fact]
    public void SourceCompatiblePartNumberRemainsUnattested()
    {
        var result = Review(30_682_981);
        Assert.Equal(CemIdentityEvidenceKind.UnattestedPartNumberObservation, result.Kind);
        AssertNoCapabilities(result);
    }

    [Fact]
    public void InvalidatedSessionGuardImmediatelyRevokesEvidence()
    {
        var result = Review(30_682_981, guardCurrent: false);
        Assert.Equal(CemIdentityEvidenceKind.NoCurrentEvidence, result.Kind);
        AssertNoCapabilities(result);
    }

    [Fact]
    public void DisconnectedAdapterCannotKeepOldIdentityEvidence()
    {
        var result = Review(30_682_981, connected: false);
        Assert.Equal(CemIdentityEvidenceKind.NoCurrentEvidence, result.Kind);
        AssertNoCapabilities(result);
    }

    [Fact]
    public void StableOrOfflineContextNeverPromotesExperimentalEvidence()
    {
        var result = Review(30_682_981, development: false);
        Assert.Equal(CemIdentityEvidenceKind.NoCurrentEvidence, result.Kind);
        AssertNoCapabilities(result);
        Assert.Equal(CemIdentityEvidenceKind.NoCurrentEvidence,
            Review(30_682_981, guardCurrent: false, connected: false, development: false).Kind);
    }

    [Theory]
    [InlineData(0u, false)]
    [InlineData(999_999u, false)]
    [InlineData(1_000_000u, true)]
    [InlineData(99_999_999u, true)]
    [InlineData(100_000_000u, false)]
    public void PartNumberShapeCannotGrantEvidenceOutsideBounds(uint value, bool expected)
    {
        var result = Review(value);
        Assert.Equal(expected, result.Kind ==
            CemIdentityEvidenceKind.UnattestedPartNumberObservation);
        AssertNoCapabilities(result);
    }

    [Fact]
    public void BilingualStatusCannotLeakPartNumberOrClaimFirmwareAuthentication()
    {
        var result = Review(30_682_981);
        var ru = CemIdentityEvidenceReport.Format(result, english: false);
        var en = CemIdentityEvidenceReport.Format(result, english: true);
        Assert.Contains("не подтверждён", ru);
        Assert.Contains("unattested", en);
        Assert.Contains("PIN", ru);
        Assert.Contains("PIN", en);
        Assert.DoesNotContain("30682981", ru);
        Assert.DoesNotContain("30682981", en);
        Assert.DoesNotContain("7E0", en);
        Assert.DoesNotContain("0x27", en);
        Assert.Contains("generic DID snapshots", en);
        Assert.Contains("снимки DID", ru);
        Assert.True(ru.Length < 450);
        Assert.True(en.Length < 450);
    }

    [Fact]
    public void UnknownIdentityStateRejectedAndBothApprovedScreensUseSameProjection()
    {
        Assert.Throws<InvalidDataException>(() =>
            CemIdentityEvidenceReport.Format(
                new CemIdentityEvidenceAssessment((CemIdentityEvidenceKind)42), true));
        Assert.Throws<ArgumentNullException>(() =>
            CemIdentityEvidenceReport.Format(null!, true));

        static string Source(string file)
        {
            for (DirectoryInfo? directory = new(AppContext.BaseDirectory);
                 directory is not null; directory = directory.Parent)
            {
                var path = Path.Combine(directory.FullName,
                    file.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(path)) return File.ReadAllText(path);
            }
            throw new FileNotFoundException(file);
        }

        var xaml = Source("src/Dvinholm.App/MainWindow.xaml");
        var pin = Source("src/Dvinholm.App/MainWindow.CemPin.cs");
        var window = Source("src/Dvinholm.App/MainWindow.xaml.cs");
        var locale = Source("src/Dvinholm.App/MainWindow.UnifiedUpdates.cs");
        Assert.Contains("x:Name=\"CemIdentityEvidencePinText\"", xaml);
        Assert.Contains("x:Name=\"CemIdentityEvidenceConfigText\"", xaml);
        Assert.Contains("CemIdentityEvidenceReport.Assess(", pin);
        Assert.Contains("CemIdentityEvidencePinText.Text = text", pin);
        Assert.Contains("CemIdentityEvidenceConfigText.Text = text", pin);
        Assert.Contains("const bool developmentContext = false", pin);
        Assert.Contains("RefreshCemIdentityEvidencePresentation();", window);
        Assert.Contains("RefreshCemIdentityEvidencePresentation();", locale);

        var start = pin.IndexOf("private void RefreshCemIdentityEvidencePresentation()", StringComparison.Ordinal);
        var end = pin.IndexOf("private void CemPinRangeChanged(", start, StringComparison.Ordinal);
        Assert.True(start > 0 && end > start);
        var region = pin[start..end];
        Assert.DoesNotContain("Task.Run", region);
        Assert.DoesNotContain("PassThru", region);
        Assert.DoesNotContain("Send(", region);
        Assert.DoesNotContain("TryReveal", region);
        Assert.DoesNotContain("ReadCemPin_Click", region);
    }
}
