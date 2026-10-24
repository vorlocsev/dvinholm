namespace Dvinholm.App.Tests;

public sealed class CanMonitorCsvSafeFieldsTests
{
    private static CanMonitorRow Row(string id = "7E8",
        string type = "11-bit", string data = "01 02") =>
        new(123, id, type, 2, data);

    [Fact]
    public void RealFrame_PreservesExistingFiveColumnFormat()
    {
        Assert.Equal(new[]
        {
            "timestamp,id,type,dlc,data",
            "123,7E8,11-bit,2,\"01 02\""
        }, CanMonitorCsv.BuildLines([Row()], syntheticDemo: false));
    }

    [Fact]
    public void DemoFrame_LabelsSyntheticSourceWithoutChangingNormalFields()
    {
        Assert.Equal(new[]
        {
            "timestamp,id,type,dlc,data,source",
            "123,7E8,11-bit,2,\"01 02\",SYNTHETIC_DEMO"
        }, CanMonitorCsv.BuildLines([Row()], syntheticDemo: true));
    }

    [Fact]
    public void EmbeddedComma_IsQuotedToAvoidCreatingAnotherColumn()
    {
        var actual = CanMonitorCsv.BuildLines([Row(id: "7E8,EVIL")], false);
        Assert.Equal("123,\"7E8,EVIL\",11-bit,2,\"01 02\"", actual[1]);
    }

    [Fact]
    public void EmbeddedQuotes_AreDoubledInDataField()
    {
        var actual = CanMonitorCsv.BuildLines([Row(data: "01 \"quoted\" 02")], false);
        Assert.Equal("123,7E8,11-bit,2,\"01 \"\"quoted\"\" 02\"", actual[1]);
    }

    [Fact]
    public void MultilineType_StaysInsideQuotedCsvCell()
    {
        var actual = CanMonitorCsv.BuildLines([Row(type: "11-bit\r\nunexpected")], false);
        Assert.Equal("123,7E8,\"11-bit\r\nunexpected\",2,\"01 02\"", actual[1]);
    }

    [Fact]
    public void FormulaAtStartOfId_IsNeutralizedWithoutExecuting()
    {
        var actual = CanMonitorCsv.BuildLines([Row(id: "=1+1")], false);
        Assert.Equal("123,\"'=1+1\",11-bit,2,\"01 02\"", actual[1]);
    }

    [Fact]
    public void LeadingWhitespaceThenFormula_IsNeutralized()
    {
        var actual = CanMonitorCsv.BuildLines([Row(type: " \t@SUM(A1)")], false);
        Assert.Equal("123,7E8,\"' \t@SUM(A1)\",2,\"01 02\"", actual[1]);
    }

    [Fact]
    public void EmptyAndNullInputs_HaveDefinedBehavior()
    {
        Assert.Equal(["timestamp,id,type,dlc,data"],
            CanMonitorCsv.BuildLines([], false));
        Assert.Throws<ArgumentNullException>(() =>
            CanMonitorCsv.BuildLines(null!, false));
        Assert.Throws<ArgumentNullException>(() =>
            CanMonitorCsv.BuildLines([null!], false));
    }
}
