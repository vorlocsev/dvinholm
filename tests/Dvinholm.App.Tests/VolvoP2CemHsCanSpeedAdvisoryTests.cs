using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline checks for source-scoped Volvo P2 XC90 HS-CAN speed advice.
/// No car, real adapter, connection, K-line or ECU request is exercised.
/// </summary>
public sealed class VolvoP2CemHsCanSpeedAdvisoryTests
{
    [Fact]
    public void MissingYearOrUnconfirmedYearNeverProducesRate()
    {
        foreach (var assessment in new[]
        {
            Evaluate(null, true, VolvoP2NetworkKind.HighSpeedCan, 250_000),
            Evaluate(2004, false, VolvoP2NetworkKind.HighSpeedCan, 250_000)
        })
        {
            Assert.Equal(VolvoP2HsCanSpeedAssessmentKind.VehicleYearUnconfirmed, assessment.Kind);
            Assert.Null(assessment.ReferenceHsCanBaudRate);
            Assert.False(assessment.AuthorizesAutomaticConnection);
        }
    }

    [Fact]
    public void UnsupportedModelYearsFailClosed()
    {
        foreach (var year in new[] { 2002, 2015, 1900, 2100 })
        {
            var assessment = Evaluate(year, true, VolvoP2NetworkKind.HighSpeedCan, 250_000);
            Assert.Equal(VolvoP2HsCanSpeedAssessmentKind.OutsideDocumentedXc90Years, assessment.Kind);
            Assert.Null(assessment.ReferenceHsCanBaudRate);
        }
    }

    [Fact]
    public void ModelYears2003And2004Use250KOnlyForHighSpeedCan()
    {
        foreach (var year in new[] { 2003, 2004 })
        {
            var assessment = Evaluate(year, true, VolvoP2NetworkKind.HighSpeedCan, 250_000);
            Assert.Equal(VolvoP2HsCanSpeedAssessmentKind.MatchesDocumentedHsCanSpeedOnly, assessment.Kind);
            Assert.Equal(250_000u, assessment.ReferenceHsCanBaudRate);
            Assert.False(assessment.ConfirmsCemDiagnosticTransport);
        }
    }

    [Fact]
    public void ModelYears2005Through2014Use500KOnlyForHighSpeedCan()
    {
        foreach (var year in new[] { 2005, 2007, 2014 })
        {
            var assessment = Evaluate(year, true, VolvoP2NetworkKind.HighSpeedCan, 500_000);
            Assert.Equal(VolvoP2HsCanSpeedAssessmentKind.MatchesDocumentedHsCanSpeedOnly, assessment.Kind);
            Assert.Equal(500_000u, assessment.ReferenceHsCanBaudRate);
            Assert.False(assessment.AuthorizesPinOrConfigurationWrite);
        }
    }

    [Fact]
    public void IncorrectRateIsAlwaysAWarningNotAnAutomaticCorrection()
    {
        var early = Evaluate(2004, true, VolvoP2NetworkKind.HighSpeedCan, 500_000);
        var later = Evaluate(2005, true, VolvoP2NetworkKind.HighSpeedCan, 250_000);
        foreach (var assessment in new[] { early, later })
        {
            Assert.Equal(VolvoP2HsCanSpeedAssessmentKind.MismatchedDocumentedHsCanSpeed, assessment.Kind);
            Assert.Contains("Stop and verify", assessment.Explanation);
            Assert.False(assessment.AuthorizesAutomaticConnection);
        }
        Assert.Equal(250_000u, early.ReferenceHsCanBaudRate);
        Assert.Equal(500_000u, later.ReferenceHsCanBaudRate);
    }

    [Fact]
    public void LowSpeedCanAndKLineNeverReuseHighSpeedCanRates()
    {
        foreach (var network in new[]
        {
            VolvoP2NetworkKind.LowSpeedCan,
            VolvoP2NetworkKind.OtherOrKLine
        })
        {
            var assessment = Evaluate(2007, true, network, 500_000);
            Assert.Equal(VolvoP2HsCanSpeedAssessmentKind.DifferentNetworkNotCovered, assessment.Kind);
            Assert.Null(assessment.ReferenceHsCanBaudRate);
            Assert.False(assessment.ConfirmsCemDiagnosticTransport);
        }
    }

    [Fact]
    public void UnidentifiedBusAndMissingSpeedBothFailClosed()
    {
        var unknown = Evaluate(2007, true, VolvoP2NetworkKind.Unknown, 500_000);
        Assert.Equal(VolvoP2HsCanSpeedAssessmentKind.NetworkUnconfirmed, unknown.Kind);
        Assert.Null(unknown.ReferenceHsCanBaudRate);
        foreach (var speed in new uint?[] { null, 0 })
        {
            var result = Evaluate(2007, true, VolvoP2NetworkKind.HighSpeedCan, speed);
            Assert.Equal(VolvoP2HsCanSpeedAssessmentKind.BaudRateNotSelected, result.Kind);
            Assert.Equal(500_000u, result.ReferenceHsCanBaudRate);
        }
    }

    [Fact]
    public void ActualWinUiSpeedSelectorWarnsAboutYearBusAndNoAutoConnect()
    {
        using var stream = typeof(VolvoP2CemHsCanSpeedAdvisoryTests).Assembly
            .GetManifestResourceStream("Dvinholm.App.Tests.MainWindow.xaml")
            ?? throw new InvalidOperationException("MainWindow.xaml embedded resource not found.");
        var doc = XDocument.Load(stream);
        XNamespace name = "http://schemas.microsoft.com/winfx/2006/xaml";
        var selector = doc.Descendants().Single(e =>
            e.Attribute(name + "Name")?.Value == "BaudRate");
        var tip = selector.Attribute("ToolTipService.ToolTip")?.Value;
        Assert.NotNull(tip);
        Assert.Contains("HS-CAN", tip!);
        Assert.Contains("2004", tip);
        Assert.Contains("2005", tip);
        Assert.Contains("verify", tip, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(new[] { "500000", "250000", "125000" },
            selector.Elements().Where(e => e.Name.LocalName == "ComboBoxItem")
                .Select(e => e.Attribute("Tag")?.Value));
    }

    private static VolvoP2HsCanSpeedAssessment Evaluate(
        int? year, bool verified, VolvoP2NetworkKind network, uint? baud) =>
        VolvoP2CemHsCanSpeedAdvisory.Evaluate(year, verified, network, baud);
}
