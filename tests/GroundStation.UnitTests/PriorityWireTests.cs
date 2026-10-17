using GroundStation.Api.Domain;
using Xunit;

namespace GroundStation.UnitTests;

public class PriorityWireTests
{
    [Theory(DisplayName = "UT-60 the documented wire values parse")]
    [InlineData("ROUTINE", Priority.Routine)]
    [InlineData("PRIORITY", Priority.Priority)]
    [InlineData("EMERGENCY", Priority.Emergency)]
    public void DocumentedValuesParse(string wire, Priority expected)
    {
        Assert.True(PriorityWire.TryParse(wire, out Priority parsed));
        Assert.Equal(expected, parsed);
    }

    [Theory(DisplayName = "UT-61 the value is case sensitive")]
    [InlineData("Routine")]
    [InlineData("routine")]
    [InlineData("Emergency")]
    [InlineData("emergency")]
    [InlineData("eMeRgEnCy")]
    public void ParsingIsCaseSensitive(string wire)
    {
        Assert.False(PriorityWire.TryParse(wire, out _));
    }

    [Theory(DisplayName = "UT-62 an unknown value is refused")]
    [InlineData("URGENT")]
    [InlineData("1")]
    [InlineData("")]
    [InlineData(" ROUTINE")]
    [InlineData("ROUTINE ")]
    public void UnknownValuesAreRefused(string wire)
    {
        Assert.False(PriorityWire.TryParse(wire, out _));
    }

    [Fact(DisplayName = "UT-63 an absent value falls back to ROUTINE")]
    public void AbsentValueFallsBackToRoutine()
    {
        Assert.True(PriorityWire.TryParse(null, out Priority parsed));
        Assert.Equal(Priority.Routine, parsed);
    }

    [Theory(DisplayName = "UT-64 every priority renders as its documented value")]
    [InlineData(Priority.Routine, "ROUTINE")]
    [InlineData(Priority.Priority, "PRIORITY")]
    [InlineData(Priority.Emergency, "EMERGENCY")]
    public void EveryPriorityRendersToItsWireValue(Priority priority, string expected)
    {
        Assert.Equal(expected, PriorityWire.ToWire(priority));
    }

    [Fact(DisplayName = "UT-65 what the API returns is what the API accepts (DEF-003 regression)")]
    public void WireValuesRoundTrip()
    {
        foreach (Priority priority in Enum.GetValues<Priority>())
        {
            string wire = PriorityWire.ToWire(priority);

            Assert.True(PriorityWire.TryParse(wire, out Priority parsed),
                $"the API renders {priority} as {wire} but will not accept it back");
            Assert.Equal(priority, parsed);
        }
    }

    [Fact(DisplayName = "UT-66 every priority is covered by the wire mapping")]
    public void EveryPriorityIsMapped()
    {
        Assert.Equal(Enum.GetValues<Priority>().Length, PriorityWire.All.Count);
    }
}
