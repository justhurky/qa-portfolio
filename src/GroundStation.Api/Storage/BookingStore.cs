using GroundStation.Api.Domain;

namespace GroundStation.Api.Storage;

public sealed record Station(string Code, string Name, string Country, int Capacity, bool Active);

public sealed record ApiUser(string ApiKey, string Username, Role Role);

public sealed class Booking
{
    public required Guid Id { get; init; }
    public required string StationCode { get; init; }
    public required string Purpose { get; init; }
    public required Priority Priority { get; init; }
    public required TimeWindow Window { get; init; }
    public required string RequestedBy { get; init; }
    public required DateTimeOffset RequestedAt { get; init; }
    public BookingState State { get; set; } = BookingStateMachine.InitialState;
    public string? DecidedBy { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
}

public sealed class BookingStore
{
    private readonly Lock gate = new();
    private readonly List<Station> stations = [];
    private readonly List<Booking> bookings = [];

    public BookingStore() => Reset();

    public static IReadOnlyList<Station> SeedStations =>
    [
        new("KIR", "Kiruna", "SE", 1, true),
        new("SVB", "Svalbard", "NO", 2, true),
        new("PER", "Perth", "AU", 1, true),
        new("OLD", "Retired Dish", "HU", 1, false)
    ];

    public static IReadOnlyList<ApiUser> SeedUsers =>
    [
        new("viewer-key", "vera.viewer", Role.Viewer),
        new("operator-key", "olga.operator", Role.Operator),
        new("operator2-key", "oscar.operator", Role.Operator),
        new("supervisor-key", "sam.supervisor", Role.Supervisor),
        new("admin-key", "ada.admin", Role.Admin)
    ];

    public void Reset()
    {
        lock (gate)
        {
            stations.Clear();
            stations.AddRange(SeedStations);
            bookings.Clear();
        }
    }

    public ApiUser? FindUser(string? apiKey) =>
        apiKey is null ? null : SeedUsers.FirstOrDefault(user => user.ApiKey == apiKey);

    public IReadOnlyList<Station> ListStations(bool activeOnly)
    {
        lock (gate)
        {
            return stations.Where(station => !activeOnly || station.Active).ToList();
        }
    }

    public Station? FindStation(string? code)
    {
        if (code is null)
        {
            return null;
        }

        lock (gate)
        {
            return stations.FirstOrDefault(station =>
                string.Equals(station.Code, code, StringComparison.OrdinalIgnoreCase));
        }
    }

    public Station AddStation(Station station)
    {
        lock (gate)
        {
            stations.Add(station);
            return station;
        }
    }

    public IReadOnlyList<Booking> ListBookings(string? stationCode, BookingState? state, string? requestedBy)
    {
        lock (gate)
        {
            return bookings
                .Where(booking => stationCode is null ||
                    string.Equals(booking.StationCode, stationCode, StringComparison.OrdinalIgnoreCase))
                .Where(booking => state is null || booking.State == state)
                .Where(booking => requestedBy is null || booking.RequestedBy == requestedBy)
                .OrderBy(booking => booking.Window.StartsAt)
                .ToList();
        }
    }

    public Booking? FindBooking(Guid id)
    {
        lock (gate)
        {
            return bookings.FirstOrDefault(booking => booking.Id == id);
        }
    }

    public IReadOnlyList<TimeWindow> ActiveWindowsFor(string stationCode)
    {
        lock (gate)
        {
            return bookings
                .Where(booking =>
                    string.Equals(booking.StationCode, stationCode, StringComparison.OrdinalIgnoreCase))
                .Where(booking => booking.State is BookingState.Requested or BookingState.Confirmed)
                .Select(booking => booking.Window)
                .ToList();
        }
    }

    public Booking? AddBookingIfCapacityAllows(Booking booking, int capacity, out RuleResult outcome)
    {
        lock (gate)
        {
            List<TimeWindow> active = bookings
                .Where(existing =>
                    string.Equals(existing.StationCode, booking.StationCode, StringComparison.OrdinalIgnoreCase))
                .Where(existing => existing.State is BookingState.Requested or BookingState.Confirmed)
                .Select(existing => existing.Window)
                .ToList();

            outcome = BookingRules.CheckCapacity(booking.Window, capacity, active);
            if (!outcome.IsAllowed)
            {
                return null;
            }

            bookings.Add(booking);
            return booking;
        }
    }

    public RuleResult TryTransition(Guid id, BookingAction action, string actor, Role role,
        DateTimeOffset now, out Booking? booking)
    {
        lock (gate)
        {
            booking = bookings.FirstOrDefault(candidate => candidate.Id == id);
            if (booking is null)
            {
                return RuleResult.Refused("BOOKING_NOT_FOUND", $"No booking with id {id}", 404);
            }

            RuleResult permission = action == BookingAction.Cancel
                ? RolePolicy.MayCancel(role, actor, booking.RequestedBy)
                : RolePolicy.MayDecide(role);
            if (!permission.IsAllowed)
            {
                return permission;
            }

            RuleResult transition = BookingStateMachine.Transition(booking.State, action);
            if (!transition.IsAllowed)
            {
                return transition;
            }

            booking.State = BookingStateMachine.TargetOf(action);
            booking.DecidedBy = actor;
            booking.DecidedAt = now;
            return RuleResult.Allowed;
        }
    }
}
