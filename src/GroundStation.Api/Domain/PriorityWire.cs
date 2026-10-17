namespace GroundStation.Api.Domain;

public static class PriorityWire
{
    public const string Routine = "ROUTINE";
    public const string Priority = "PRIORITY";
    public const string Emergency = "EMERGENCY";

    public static IReadOnlyList<string> All => [Routine, Priority, Emergency];

    public static string ToWire(Priority priority) => priority switch
    {
        Domain.Priority.Routine => Routine,
        Domain.Priority.Priority => Priority,
        Domain.Priority.Emergency => Emergency,
        _ => throw new ArgumentOutOfRangeException(nameof(priority))
    };

    public static bool TryParse(string? value, out Priority priority)
    {
        priority = Domain.Priority.Routine;

        if (value is null)
        {
            return true;
        }

        switch (value)
        {
            case Routine:
                priority = Domain.Priority.Routine;
                return true;
            case Priority:
                priority = Domain.Priority.Priority;
                return true;
            case Emergency:
                priority = Domain.Priority.Emergency;
                return true;
            default:
                return false;
        }
    }
}
