namespace GroundStation.Api.Domain;

public static class BookingRules
{
    public const int MinDurationMinutes = 5;
    public const int MaxDurationMinutes = 120;
    public const int MinLeadMinutes = 15;
    public const int MaxLeadDays = 30;
    public const int MinPurposeLength = 3;
    public const int MaxPurposeLength = 200;

    public static RuleResult CheckPurpose(string? purpose)
    {
        if (string.IsNullOrWhiteSpace(purpose))
        {
            return RuleResult.Invalid("purpose is required", "purpose");
        }

        string trimmed = purpose.Trim();
        if (trimmed.Length < MinPurposeLength || trimmed.Length > MaxPurposeLength)
        {
            return RuleResult.Invalid(
                $"purpose must be between {MinPurposeLength} and {MaxPurposeLength} characters",
                "purpose");
        }

        return RuleResult.Allowed;
    }

    public static RuleResult CheckWindowOrder(TimeWindow window) =>
        window.IsOrdered
            ? RuleResult.Allowed
            : RuleResult.Invalid("endsAt must be after startsAt", "endsAt");

    public static RuleResult CheckDuration(TimeWindow window)
    {
        double minutes = window.DurationMinutes;
        if (minutes < MinDurationMinutes || minutes > MaxDurationMinutes)
        {
            return RuleResult.Refused("DURATION_OUT_OF_RANGE",
                $"Duration must be between {MinDurationMinutes} and {MaxDurationMinutes} minutes", 422);
        }

        return RuleResult.Allowed;
    }

    public static RuleResult CheckLeadTime(TimeWindow window, Priority priority, DateTimeOffset now)
    {
        double lead = window.LeadMinutesFrom(now);

        if (!RolePolicy.MayOverrideLeadTime(priority) && lead < MinLeadMinutes)
        {
            return RuleResult.Refused("LEAD_TIME_TOO_SHORT",
                $"A {priority} booking must start at least {MinLeadMinutes} minutes from now", 422);
        }

        if (lead > MaxLeadDays * 24 * 60)
        {
            return RuleResult.Refused("LEAD_TIME_TOO_LONG",
                $"A booking may not start more than {MaxLeadDays} days from now", 422);
        }

        return RuleResult.Allowed;
    }

    public static RuleResult CheckCapacity(TimeWindow candidate, int capacity,
        IEnumerable<TimeWindow> activeWindows)
    {
        int clashes = activeWindows.Count(existing => candidate.Overlaps(existing));
        if (clashes >= capacity)
        {
            return RuleResult.Refused("STATION_AT_CAPACITY",
                $"The station already has {clashes} overlapping booking(s)", 409);
        }

        return RuleResult.Allowed;
    }
}
