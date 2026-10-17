namespace GroundStation.Api.Domain;

public sealed record RuleResult(bool IsAllowed, string? Code, string? Message, int Status)
{
    public static RuleResult Allowed { get; } = new(true, null, null, 200);

    public static RuleResult Refused(string code, string message, int status) =>
        new(false, code, message, status);

    public static RuleResult Invalid(string message, string field) =>
        new(false, "VALIDATION_FAILED", message, 400) { Field = field };

    public string? Field { get; init; }
}
