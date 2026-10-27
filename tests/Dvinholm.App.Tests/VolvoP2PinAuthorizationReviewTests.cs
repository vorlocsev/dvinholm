using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight deterministic software-only checks for an explicitly non-operational
/// P2 CEM PIN readiness workflow, including cancellable checklist UI.
/// </summary>
public sealed class VolvoP2PinAuthorizationReviewTests
{
    [Fact]
    public void NewReviewStartsAtFirstOfFourExplanatoryStages()
    {
        var review = new VolvoP2PinAuthorizationReview();
        Assert.Equal(VolvoP2PinPreviewState.Reviewing, review.State);
        Assert.Equal(1, review.StepNumber);
        Assert.Equal(4, review.TotalSteps);
        Assert.Contains("P2/CEM identity evidence", review.DescribeCurrent());
        Assert.Contains("step 1/4", review.DescribeCurrent());
    }

    [Fact]
    public void AllStepsRemainUnverifiedAndNeverIssueEcuActions()
    {
        var review = new VolvoP2PinAuthorizationReview();
        for (var step = 1; step <= review.TotalSteps; step++)
        {
            Assert.Equal(step, review.StepNumber);
            Assert.Contains("Current state:", review.DescribeCurrent());
            Assert.Contains("PIN lookup: DISABLED", review.DescribeCurrent());
            Assert.False(review.CanStartPinSearch);
            Assert.False(review.CanTransmitToEcu);
            Assert.False(review.CanEditOrWriteConfiguration);
            if (step < review.TotalSteps)
                Assert.True(review.Advance());
        }
    }

    [Fact]
    public void ChecklistCompletionDoesNotClaimSuccessfulPinSearch()
    {
        var review = new VolvoP2PinAuthorizationReview();
        for (var step = 1; step < review.TotalSteps; step++)
            Assert.True(review.Advance());
        Assert.False(review.Advance());
        Assert.Equal(VolvoP2PinPreviewState.ReviewFinishedUnsupported, review.State);
        Assert.Contains("UNSUPPORTED", review.DescribeCurrent());
        Assert.False(review.HasPinResult);
        Assert.False(review.CanStartPinSearch);
        Assert.False(review.CanUseServiceCredentials);
        Assert.False(review.Advance());
    }

    [Fact]
    public void CancellationIsImmediateTerminalAndIdempotent()
    {
        var review = new VolvoP2PinAuthorizationReview();
        Assert.True(review.Advance());
        review.Cancel();
        review.Cancel();
        Assert.Equal(VolvoP2PinPreviewState.Cancelled, review.State);
        Assert.Equal(2, review.StepNumber);
        Assert.Contains("CANCELLED", review.DescribeCurrent());
        Assert.False(review.Advance());
        Assert.False(review.CanTransmitToEcu);
    }

    [Fact]
    public void FinishingCannotBeReclassifiedByLateCancellation()
    {
        var review = new VolvoP2PinAuthorizationReview();
        for (var step = 0; step < review.TotalSteps; step++)
            review.Advance();
        review.Cancel();
        Assert.Equal(VolvoP2PinPreviewState.ReviewFinishedUnsupported, review.State);
        Assert.False(review.HasEstimatedPinSearchTime);
        Assert.False(review.CanEditOrWriteConfiguration);
    }

    [Fact]
    public void SeparateConfigurationCredentialsFromImmobilizerSecrets()
    {
        var review = new VolvoP2PinAuthorizationReview();
        while (review.StepNumber < 4)
            review.Advance();
        var details = review.DescribeCurrent();
        Assert.Contains("configuration service authorization", details);
        Assert.Contains("immobilizer", details);
        Assert.Contains("no credential collection", details);
        Assert.False(review.CanAccessImmobilizerSecrets);
        Assert.False(review.CanUseServiceCredentials);
    }

    [Fact]
    public void XamlShowsDedicatedCancellableOfflineChecklistAction()
    {
        var button = Named("P2PinReviewButton");
        Assert.Equal("Button", button.Name.LocalName);
        Assert.Equal("P2PinReview_Click", button.Attribute("Click")?.Value);
        Assert.Equal("4", button.Attribute("Grid.Row")?.Value);
        Assert.Contains("offline", button.Attribute("Content")?.Value,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("No PIN extraction", button.Attribute("ToolTipService.ToolTip")?.Value);
    }

    [Fact]
    public void PinPreviewDoesNotRemoveOriginalSevenProfileActionsOrCatalog()
    {
        var old = Named("P2ConfigCatalogButton");
        Assert.Equal("3", old.Attribute("Grid.Row")?.Value);
        var panel = Named("ProfileActionsPanel");
        Assert.Equal(7, panel.Descendants().Count(x => x.Name.LocalName == "Button"));
        var metadata = Named("EcuInfoText").Parent!;
        Assert.Equal("76", metadata.Attribute("MaxHeight")?.Value);
        Assert.Equal("2", metadata.Attribute("Grid.Row")?.Value);
        var review = new VolvoP2PinAuthorizationReview();
        Assert.False(review.HasEstimatedPinSearchTime);
    }

    private static XElement Named(string name)
    {
        using var stream = typeof(VolvoP2PinAuthorizationReviewTests).Assembly
            .GetManifestResourceStream("Dvinholm.App.Tests.MainWindow.xaml")
            ?? throw new InvalidOperationException("MainWindow.xaml resource missing.");
        return XDocument.Load(stream).Descendants().Single(e =>
            e.Attribute(XName.Get("Name",
                "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == name);
    }
}
