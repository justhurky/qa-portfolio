namespace GroundStation.Api.Domain;

public static class BookingStateMachine
{
    private static readonly IReadOnlyDictionary<BookingState, IReadOnlySet<BookingState>> Allowed =
        new Dictionary<BookingState, IReadOnlySet<BookingState>>
        {
            [BookingState.Requested] = new HashSet<BookingState>
                { BookingState.Confirmed, BookingState.Cancelled },
            [BookingState.Confirmed] = new HashSet<BookingState>
                { BookingState.Completed, BookingState.Cancelled },
            [BookingState.Completed] = new HashSet<BookingState>(),
            [BookingState.Cancelled] = new HashSet<BookingState>()
        };

    public static BookingState InitialState => BookingState.Requested;

    public static BookingState TargetOf(BookingAction action) => action switch
    {
        BookingAction.Confirm => BookingState.Confirmed,
        BookingAction.Cancel => BookingState.Cancelled,
        BookingAction.Complete => BookingState.Completed,
        _ => throw new ArgumentOutOfRangeException(nameof(action))
    };

    public static IReadOnlySet<BookingState> TargetsFrom(BookingState state) => Allowed[state];

    public static bool IsTerminal(BookingState state) => Allowed[state].Count == 0;

    public static bool CanTransition(BookingState from, BookingState to) => Allowed[from].Contains(to);

    public static RuleResult Transition(BookingState from, BookingAction action)
    {
        BookingState target = TargetOf(action);
        if (CanTransition(from, target))
        {
            return RuleResult.Allowed;
        }

        return RuleResult.Refused(
            "ILLEGAL_STATE_TRANSITION",
            $"A booking in state {from} cannot move to {target}",
            409);
    }
}
