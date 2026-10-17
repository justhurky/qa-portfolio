using GroundStation.Api.Domain;
using Xunit;

namespace GroundStation.UnitTests;

public class BookingRulesDurationTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);

    private static TimeWindow WindowOf(double minutes) =>
        new(Start, Start.AddMinutes(minutes));

    [Theory(DisplayName = "UT-01 a duration inside the range is accepted")]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(30)]
    [InlineData(119)]
    [InlineData(120)]
    public void DurationsInsideTheRangeAreAccepted(double minutes)
    {
        RuleResult result = BookingRules.CheckDuration(WindowOf(minutes));

        Assert.True(result.IsAllowed);
    }

    [Theory(DisplayName = "UT-02 a duration outside the range is refused")]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(4.9)]
    [InlineData(120.1)]
    [InlineData(121)]
    [InlineData(1440)]
    public void DurationsOutsideTheRangeAreRefused(double minutes)
    {
        RuleResult result = BookingRules.CheckDuration(WindowOf(minutes));

        Assert.False(result.IsAllowed);
        Assert.Equal("DURATION_OUT_OF_RANGE", result.Code);
        Assert.Equal(422, result.Status);
    }

    [Fact(DisplayName = "UT-03 the duration boundaries are inclusive at both ends")]
    public void BoundariesAreInclusive()
    {
        Assert.True(BookingRules.CheckDuration(WindowOf(BookingRules.MinDurationMinutes)).IsAllowed);
        Assert.True(BookingRules.CheckDuration(WindowOf(BookingRules.MaxDurationMinutes)).IsAllowed);
        Assert.False(BookingRules.CheckDuration(WindowOf(BookingRules.MinDurationMinutes - 0.1)).IsAllowed);
        Assert.False(BookingRules.CheckDuration(WindowOf(BookingRules.MaxDurationMinutes + 0.1)).IsAllowed);
    }

    [Fact(DisplayName = "UT-04 a reversed window fails the ordering rule")]
    public void ReversedWindowIsRefused()
    {
        RuleResult result = BookingRules.CheckWindowOrder(new TimeWindow(Start, Start.AddMinutes(-30)));

        Assert.False(result.IsAllowed);
        Assert.Equal("endsAt", result.Field);
        Assert.Equal(400, result.Status);
    }

    [Fact(DisplayName = "UT-05 a zero length window fails ordering, not duration")]
    public void ZeroLengthWindowFailsOrderingFirst()
    {
        TimeWindow window = new(Start, Start);

        Assert.False(BookingRules.CheckWindowOrder(window).IsAllowed);
        Assert.False(BookingRules.CheckDuration(window).IsAllowed);
        Assert.Equal(400, BookingRules.CheckWindowOrder(window).Status);
        Assert.Equal(422, BookingRules.CheckDuration(window).Status);
    }
}

public class BookingRulesPurposeTests
{
    [Theory(DisplayName = "UT-06 a purpose inside the length range is accepted")]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(100)]
    [InlineData(199)]
    [InlineData(200)]
    public void PurposeLengthsInsideTheRangeAreAccepted(int length)
    {
        Assert.True(BookingRules.CheckPurpose(new string('x', length)).IsAllowed);
    }

    [Theory(DisplayName = "UT-07 a purpose outside the length range is refused")]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(201)]
    [InlineData(500)]
    public void PurposeLengthsOutsideTheRangeAreRefused(int length)
    {
        RuleResult result = BookingRules.CheckPurpose(new string('x', length));

        Assert.False(result.IsAllowed);
        Assert.Equal("purpose", result.Field);
    }

    [Theory(DisplayName = "UT-08 an absent purpose is refused")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void AbsentPurposeIsRefused(string? purpose)
    {
        RuleResult result = BookingRules.CheckPurpose(purpose);

        Assert.False(result.IsAllowed);
        Assert.Equal("purpose is required", result.Message);
    }

    [Fact(DisplayName = "UT-09 the length is measured after trimming")]
    public void LengthIsMeasuredAfterTrimming()
    {
        Assert.False(BookingRules.CheckPurpose("  ab  ").IsAllowed);
        Assert.True(BookingRules.CheckPurpose("  abc  ").IsAllowed);
        Assert.False(BookingRules.CheckPurpose("  " + new string('x', 201) + "  ").IsAllowed);
    }
}

public class BookingRulesLeadTimeTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);

    private static TimeWindow StartingIn(double minutes) =>
        new(Now.AddMinutes(minutes), Now.AddMinutes(minutes + 30));

    [Theory(DisplayName = "UT-10 a routine booking inside the lead window is accepted")]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(60)]
    [InlineData(43200)]
    public void LeadTimesInsideTheWindowAreAccepted(double leadMinutes)
    {
        RuleResult result = BookingRules.CheckLeadTime(StartingIn(leadMinutes), Priority.Routine, Now);

        Assert.True(result.IsAllowed);
    }

    [Theory(DisplayName = "UT-11 a routine booking below the minimum lead time is refused")]
    [InlineData(14.9)]
    [InlineData(14)]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-1440)]
    public void LeadTimesBelowTheMinimumAreRefused(double leadMinutes)
    {
        RuleResult result = BookingRules.CheckLeadTime(StartingIn(leadMinutes), Priority.Routine, Now);

        Assert.False(result.IsAllowed);
        Assert.Equal("LEAD_TIME_TOO_SHORT", result.Code);
    }

    [Fact(DisplayName = "UT-12 the minimum lead time boundary is inclusive")]
    public void MinimumLeadBoundaryIsInclusive()
    {
        Assert.True(BookingRules
            .CheckLeadTime(StartingIn(BookingRules.MinLeadMinutes), Priority.Routine, Now).IsAllowed);
        Assert.False(BookingRules
            .CheckLeadTime(StartingIn(BookingRules.MinLeadMinutes - 0.01), Priority.Routine, Now).IsAllowed);
    }

    [Theory(DisplayName = "UT-13 a booking beyond the far horizon is refused")]
    [InlineData(43201)]
    [InlineData(50000)]
    public void LeadTimesBeyondTheHorizonAreRefused(double leadMinutes)
    {
        RuleResult result = BookingRules.CheckLeadTime(StartingIn(leadMinutes), Priority.Routine, Now);

        Assert.False(result.IsAllowed);
        Assert.Equal("LEAD_TIME_TOO_LONG", result.Code);
    }

    [Theory(DisplayName = "UT-14 an emergency may start inside the minimum lead time")]
    [InlineData(14)]
    [InlineData(1)]
    [InlineData(0)]
    public void EmergencyOverridesTheMinimumLeadTime(double leadMinutes)
    {
        RuleResult result = BookingRules.CheckLeadTime(StartingIn(leadMinutes), Priority.Emergency, Now);

        Assert.True(result.IsAllowed);
    }

    [Theory(DisplayName = "UT-15 an emergency may still not start in the past (DEF-001)")]
    [Trait("Category", "KnownDefect")]
    [InlineData(-1)]
    [InlineData(-4320)]
    [InlineData(-525600)]
    public void EmergencyMayNotBeBackdated(double leadMinutes)
    {
        RuleResult result = BookingRules.CheckLeadTime(StartingIn(leadMinutes), Priority.Emergency, Now);

        Assert.False(result.IsAllowed);
    }

    [Fact(DisplayName = "UT-16 the far horizon applies to every priority")]
    public void FarHorizonAppliesToEmergencyToo()
    {
        RuleResult result = BookingRules.CheckLeadTime(StartingIn(50000), Priority.Emergency, Now);

        Assert.False(result.IsAllowed);
        Assert.Equal("LEAD_TIME_TOO_LONG", result.Code);
    }
}

public class BookingRulesCapacityTests
{
    private static readonly DateTimeOffset Base = new(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);

    private static TimeWindow At(double offsetMinutes, double durationMinutes = 30) =>
        new(Base.AddMinutes(offsetMinutes), Base.AddMinutes(offsetMinutes + durationMinutes));

    [Fact(DisplayName = "UT-20 an empty station accepts a booking")]
    public void EmptyStationAcceptsABooking()
    {
        Assert.True(BookingRules.CheckCapacity(At(0), 1, []).IsAllowed);
    }

    [Fact(DisplayName = "UT-21 a single slot station refuses an overlap")]
    public void SingleSlotStationRefusesAnOverlap()
    {
        RuleResult result = BookingRules.CheckCapacity(At(10), 1, [At(0)]);

        Assert.False(result.IsAllowed);
        Assert.Equal("STATION_AT_CAPACITY", result.Code);
        Assert.Equal(409, result.Status);
    }

    [Fact(DisplayName = "UT-22 a two slot station accepts a second overlap and refuses a third")]
    public void CapacityIsRespectedExactly()
    {
        Assert.True(BookingRules.CheckCapacity(At(10), 2, [At(0)]).IsAllowed);
        Assert.False(BookingRules.CheckCapacity(At(10), 2, [At(0), At(5)]).IsAllowed);
    }

    [Fact(DisplayName = "UT-23 windows that only touch do not overlap")]
    public void TouchingWindowsDoNotOverlap()
    {
        TimeWindow first = At(0);
        TimeWindow second = At(30);

        Assert.False(first.Overlaps(second));
        Assert.True(first.Touches(second));
        Assert.True(BookingRules.CheckCapacity(second, 1, [first]).IsAllowed);
    }

    [Fact(DisplayName = "UT-24 a booking that is fully contained in another overlaps it")]
    public void ContainedWindowOverlaps()
    {
        TimeWindow outer = At(0, 120);
        TimeWindow inner = At(30, 10);

        Assert.True(outer.Overlaps(inner));
        Assert.True(inner.Overlaps(outer));
        Assert.False(BookingRules.CheckCapacity(inner, 1, [outer]).IsAllowed);
    }

    [Fact(DisplayName = "UT-25 a booking that overlaps by one minute still clashes")]
    public void OneMinuteOverlapStillClashes()
    {
        Assert.False(BookingRules.CheckCapacity(At(29), 1, [At(0)]).IsAllowed);
    }

    [Fact(DisplayName = "UT-26 only overlapping windows count towards the capacity")]
    public void NonOverlappingWindowsAreIgnored()
    {
        Assert.True(BookingRules.CheckCapacity(At(0), 1, [At(60), At(120), At(180)]).IsAllowed);
    }
}
