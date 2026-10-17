namespace GroundStation.Api.Domain;

public static class RolePolicy
{
    public static bool HasAtLeast(Role actual, Role required) => actual >= required;

    public static Role MinimumRoleFor(Priority priority) => priority switch
    {
        Priority.Routine => Role.Operator,
        Priority.Priority => Role.Operator,
        Priority.Emergency => Role.Supervisor,
        _ => throw new ArgumentOutOfRangeException(nameof(priority))
    };

    public static bool MayOverrideLeadTime(Priority priority) => priority == Priority.Emergency;

    public static RuleResult MayCreateBooking(Role role) =>
        HasAtLeast(role, Role.Operator)
            ? RuleResult.Allowed
            : RuleResult.Refused("ACCESS_DENIED", "Role Operator or higher is required", 403);

    public static RuleResult MayUsePriority(Role role, Priority priority)
    {
        Role required = MinimumRoleFor(priority);
        return HasAtLeast(role, required)
            ? RuleResult.Allowed
            : RuleResult.Refused("PRIORITY_NOT_PERMITTED",
                $"Priority {priority} requires role {required} or higher", 403);
    }

    public static RuleResult MayDecide(Role role) =>
        HasAtLeast(role, Role.Supervisor)
            ? RuleResult.Allowed
            : RuleResult.Refused("ACCESS_DENIED", "Role Supervisor or higher is required", 403);

    public static RuleResult MayCancel(Role role, string actor, string requestedBy)
    {
        if (!HasAtLeast(role, Role.Operator))
        {
            return RuleResult.Refused("ACCESS_DENIED", "Role Operator or higher is required", 403);
        }

        bool ownsBooking = string.Equals(actor, requestedBy, StringComparison.Ordinal);
        return ownsBooking || HasAtLeast(role, Role.Supervisor)
            ? RuleResult.Allowed
            : RuleResult.Refused("ACCESS_DENIED",
                "Only the requester or a supervisor may cancel this booking", 403);
    }

    public static RuleResult MayManageStations(Role role) =>
        HasAtLeast(role, Role.Admin)
            ? RuleResult.Allowed
            : RuleResult.Refused("ACCESS_DENIED", "Role Admin or higher is required", 403);
}
