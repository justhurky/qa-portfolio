using GroundStation.Api.Domain;
using GroundStation.Api.Storage;

namespace GroundStation.Api.Contracts;

public sealed record CreateBookingRequest(
    string? StationCode,
    string? Purpose,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt,
    string? Priority);

public sealed record CreateStationRequest(string? Code, string? Name, string? Country, int? Capacity,
    bool? Active);

public sealed record BookingResponse(
    Guid Id,
    string StationCode,
    string Purpose,
    string Priority,
    string State,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string RequestedBy,
    DateTimeOffset RequestedAt,
    string? DecidedBy,
    DateTimeOffset? DecidedAt)
{
    public static BookingResponse From(Booking booking) => new(
        booking.Id,
        booking.StationCode,
        booking.Purpose,
        PriorityWire.ToWire(booking.Priority),
        booking.State.ToString().ToUpperInvariant(),
        booking.Window.StartsAt,
        booking.Window.EndsAt,
        booking.RequestedBy,
        booking.RequestedAt,
        booking.DecidedBy,
        booking.DecidedAt);
}

public sealed record StationResponse(string Code, string Name, string Country, int Capacity, bool Active)
{
    public static StationResponse From(Station station) =>
        new(station.Code, station.Name, station.Country, station.Capacity, station.Active);
}

public sealed record IdentityResponse(string Username, string Role);

public sealed record PageResponse<T>(IReadOnlyList<T> Content, int Page, int Size, int TotalElements,
    int TotalPages)
{
    public static PageResponse<T> Of(IReadOnlyList<T> items, int page, int size) => new(
        items.Skip(page * size).Take(size).ToList(),
        page,
        size,
        items.Count,
        (int)Math.Ceiling(items.Count / (double)size));
}

public sealed record ErrorBody(ErrorDetail Error)
{
    public static ErrorBody Of(string code, string message, IDictionary<string, object?>? details = null) =>
        new(new ErrorDetail(code, message, details));
}

public sealed record ErrorDetail(string Code, string Message, IDictionary<string, object?>? Details);
