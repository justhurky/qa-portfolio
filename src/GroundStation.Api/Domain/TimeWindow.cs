namespace GroundStation.Api.Domain;

public readonly record struct TimeWindow(DateTimeOffset StartsAt, DateTimeOffset EndsAt)
{
    public TimeSpan Duration => EndsAt - StartsAt;

    public double DurationMinutes => Duration.TotalMinutes;

    public bool IsOrdered => EndsAt > StartsAt;

    public double LeadMinutesFrom(DateTimeOffset now) => (StartsAt - now).TotalMinutes;

    public bool Overlaps(TimeWindow other) => StartsAt < other.EndsAt && other.StartsAt < EndsAt;

    public bool Touches(TimeWindow other) => EndsAt == other.StartsAt || other.EndsAt == StartsAt;
}
