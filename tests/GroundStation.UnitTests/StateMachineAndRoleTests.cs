using GroundStation.Api.Domain;
using Xunit;

namespace GroundStation.UnitTests;

public class BookingStateMachineTests
{
    [Fact(DisplayName = "UT-30 a new booking starts in Requested")]
    public void InitialStateIsRequested()
    {
        Assert.Equal(BookingState.Requested, BookingStateMachine.InitialState);
    }

    [Theory(DisplayName = "UT-31 the documented transitions are permitted")]
    [InlineData(BookingState.Requested, BookingAction.Confirm, BookingState.Confirmed)]
    [InlineData(BookingState.Requested, BookingAction.Cancel, BookingState.Cancelled)]
    [InlineData(BookingState.Confirmed, BookingAction.Complete, BookingState.Completed)]
    [InlineData(BookingState.Confirmed, BookingAction.Cancel, BookingState.Cancelled)]
    public void PermittedTransitionsAreAllowed(BookingState from, BookingAction action, BookingState to)
    {
        Assert.True(BookingStateMachine.Transition(from, action).IsAllowed);
        Assert.True(BookingStateMachine.CanTransition(from, to));
        Assert.Equal(to, BookingStateMachine.TargetOf(action));
    }

    [Theory(DisplayName = "UT-32 every other transition is refused")]
    [InlineData(BookingState.Requested, BookingAction.Complete)]
    [InlineData(BookingState.Confirmed, BookingAction.Confirm)]
    [InlineData(BookingState.Completed, BookingAction.Confirm)]
    [InlineData(BookingState.Completed, BookingAction.Cancel)]
    [InlineData(BookingState.Completed, BookingAction.Complete)]
    [InlineData(BookingState.Cancelled, BookingAction.Confirm)]
    [InlineData(BookingState.Cancelled, BookingAction.Cancel)]
    [InlineData(BookingState.Cancelled, BookingAction.Complete)]
    public void ForbiddenTransitionsAreRefused(BookingState from, BookingAction action)
    {
        RuleResult result = BookingStateMachine.Transition(from, action);

        Assert.False(result.IsAllowed);
        Assert.Equal("ILLEGAL_STATE_TRANSITION", result.Code);
        Assert.Equal(409, result.Status);
    }

    [Theory(DisplayName = "UT-33 outcome states are terminal")]
    [InlineData(BookingState.Completed)]
    [InlineData(BookingState.Cancelled)]
    public void OutcomeStatesAreTerminal(BookingState state)
    {
        Assert.True(BookingStateMachine.IsTerminal(state));
        Assert.Empty(BookingStateMachine.TargetsFrom(state));
    }

    [Theory(DisplayName = "UT-34 in-flight states are not terminal")]
    [InlineData(BookingState.Requested)]
    [InlineData(BookingState.Confirmed)]
    public void InFlightStatesAreNotTerminal(BookingState state)
    {
        Assert.False(BookingStateMachine.IsTerminal(state));
        Assert.NotEmpty(BookingStateMachine.TargetsFrom(state));
    }

    [Fact(DisplayName = "UT-35 no state may transition to itself")]
    public void NoSelfTransitions()
    {
        foreach (BookingState state in Enum.GetValues<BookingState>())
        {
            Assert.False(BookingStateMachine.CanTransition(state, state));
        }
    }

    [Fact(DisplayName = "UT-36 every state is declared in the transition table")]
    public void EveryStateIsDeclared()
    {
        foreach (BookingState state in Enum.GetValues<BookingState>())
        {
            Assert.NotNull(BookingStateMachine.TargetsFrom(state));
        }
    }

    [Fact(DisplayName = "UT-37 the refusal names the state it came from")]
    public void RefusalNamesTheState()
    {
        RuleResult result = BookingStateMachine.Transition(BookingState.Completed, BookingAction.Cancel);

        Assert.Contains("Completed", result.Message);
        Assert.Contains("Cancelled", result.Message);
    }
}

public class RolePolicyTests
{
    [Theory(DisplayName = "UT-40 a role satisfies itself and everything below it")]
    [InlineData(Role.Admin, Role.Viewer, true)]
    [InlineData(Role.Admin, Role.Admin, true)]
    [InlineData(Role.Supervisor, Role.Operator, true)]
    [InlineData(Role.Operator, Role.Operator, true)]
    [InlineData(Role.Operator, Role.Supervisor, false)]
    [InlineData(Role.Viewer, Role.Operator, false)]
    public void RoleInheritanceIsOrdered(Role actual, Role required, bool expected)
    {
        Assert.Equal(expected, RolePolicy.HasAtLeast(actual, required));
    }

    [Theory(DisplayName = "UT-41 creating a booking requires Operator or higher")]
    [InlineData(Role.Viewer, false)]
    [InlineData(Role.Operator, true)]
    [InlineData(Role.Supervisor, true)]
    [InlineData(Role.Admin, true)]
    public void BookingCreationRequiresOperator(Role role, bool expected)
    {
        Assert.Equal(expected, RolePolicy.MayCreateBooking(role).IsAllowed);
    }

    [Theory(DisplayName = "UT-42 the priority decision table holds")]
    [InlineData(Role.Viewer, Priority.Routine, false)]
    [InlineData(Role.Operator, Priority.Routine, true)]
    [InlineData(Role.Operator, Priority.Priority, true)]
    [InlineData(Role.Operator, Priority.Emergency, false)]
    [InlineData(Role.Supervisor, Priority.Emergency, true)]
    [InlineData(Role.Admin, Priority.Emergency, true)]
    public void PriorityRequiresItsMinimumRole(Role role, Priority priority, bool expected)
    {
        Assert.Equal(expected, RolePolicy.MayUsePriority(role, priority).IsAllowed);
    }

    [Fact(DisplayName = "UT-43 a refused priority says which role is needed")]
    public void RefusedPrioritySaysWhichRoleIsNeeded()
    {
        RuleResult result = RolePolicy.MayUsePriority(Role.Operator, Priority.Emergency);

        Assert.Equal("PRIORITY_NOT_PERMITTED", result.Code);
        Assert.Contains("Supervisor", result.Message);
        Assert.Equal(403, result.Status);
    }

    [Theory(DisplayName = "UT-44 only an emergency overrides the lead time")]
    [InlineData(Priority.Routine, false)]
    [InlineData(Priority.Priority, false)]
    [InlineData(Priority.Emergency, true)]
    public void OnlyEmergencyOverridesLeadTime(Priority priority, bool expected)
    {
        Assert.Equal(expected, RolePolicy.MayOverrideLeadTime(priority));
    }

    [Theory(DisplayName = "UT-45 deciding a booking requires Supervisor or higher")]
    [InlineData(Role.Viewer, false)]
    [InlineData(Role.Operator, false)]
    [InlineData(Role.Supervisor, true)]
    [InlineData(Role.Admin, true)]
    public void DecidingRequiresSupervisor(Role role, bool expected)
    {
        Assert.Equal(expected, RolePolicy.MayDecide(role).IsAllowed);
    }

    [Theory(DisplayName = "UT-46 cancellation depends on ownership and role")]
    [InlineData(Role.Operator, "olga.operator", "olga.operator", true)]
    [InlineData(Role.Operator, "oscar.operator", "olga.operator", false)]
    [InlineData(Role.Supervisor, "sam.supervisor", "olga.operator", true)]
    [InlineData(Role.Admin, "ada.admin", "olga.operator", true)]
    [InlineData(Role.Viewer, "vera.viewer", "vera.viewer", false)]
    public void CancellationChecksOwnershipThenRole(Role role, string actor, string requestedBy,
        bool expected)
    {
        Assert.Equal(expected, RolePolicy.MayCancel(role, actor, requestedBy).IsAllowed);
    }

    [Theory(DisplayName = "UT-47 only an administrator may manage stations")]
    [InlineData(Role.Viewer, false)]
    [InlineData(Role.Operator, false)]
    [InlineData(Role.Supervisor, false)]
    [InlineData(Role.Admin, true)]
    public void StationManagementRequiresAdmin(Role role, bool expected)
    {
        Assert.Equal(expected, RolePolicy.MayManageStations(role).IsAllowed);
    }
}

public class TimeWindowTests
{
    private static readonly DateTimeOffset Base = new(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "UT-50 a window reports its duration in minutes")]
    public void DurationIsReportedInMinutes()
    {
        Assert.Equal(90, new TimeWindow(Base, Base.AddMinutes(90)).DurationMinutes);
    }

    [Theory(DisplayName = "UT-51 ordering is strict")]
    [InlineData(30, true)]
    [InlineData(0, false)]
    [InlineData(-30, false)]
    public void OrderingIsStrict(double minutes, bool expected)
    {
        Assert.Equal(expected, new TimeWindow(Base, Base.AddMinutes(minutes)).IsOrdered);
    }

    [Fact(DisplayName = "UT-52 lead time is measured from the supplied instant")]
    public void LeadTimeIsMeasuredFromTheSuppliedInstant()
    {
        TimeWindow window = new(Base.AddMinutes(45), Base.AddMinutes(75));

        Assert.Equal(45, window.LeadMinutesFrom(Base));
        Assert.Equal(-15, window.LeadMinutesFrom(Base.AddMinutes(60)));
    }

    [Fact(DisplayName = "UT-53 overlap is symmetric")]
    public void OverlapIsSymmetric()
    {
        TimeWindow first = new(Base, Base.AddMinutes(60));
        TimeWindow second = new(Base.AddMinutes(30), Base.AddMinutes(90));

        Assert.True(first.Overlaps(second));
        Assert.True(second.Overlaps(first));
    }

    [Fact(DisplayName = "UT-54 a window does not overlap one that starts when it ends")]
    public void TouchingWindowsDoNotOverlap()
    {
        TimeWindow first = new(Base, Base.AddMinutes(30));
        TimeWindow second = new(Base.AddMinutes(30), Base.AddMinutes(60));

        Assert.False(first.Overlaps(second));
        Assert.False(second.Overlaps(first));
    }

    [Fact(DisplayName = "UT-55 a window overlaps itself")]
    public void WindowOverlapsItself()
    {
        TimeWindow window = new(Base, Base.AddMinutes(30));

        Assert.True(window.Overlaps(window));
    }
}
