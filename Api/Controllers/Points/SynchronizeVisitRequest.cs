using System.ComponentModel.DataAnnotations;

namespace Api.Controllers.Points;

public sealed record SynchronizeVisitRequest(
    VisitStateRequest? Expected,
    VisitStateRequest? Desired,
    int? UtcOffsetMinutes = null) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Expected is null || Desired is null)
        {
            yield return new ValidationResult(
                "Expected and desired visit states are required.",
                [nameof(Expected), nameof(Desired)]);
            yield break;
        }

        if (UtcOffsetMinutes is < -840 or > 840)
        {
            yield return new ValidationResult(
                "The UTC offset must be between -840 and 840 minutes.",
                [nameof(UtcOffsetMinutes)]);
            yield break;
        }

        if (!Desired.IsVisited || !Desired.VisitedOn.HasValue)
        {
            yield break;
        }

        var localNow = UtcOffsetMinutes.HasValue
            ? DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromMinutes(UtcOffsetMinutes.Value)).DateTime
            : DateTime.Now;
        var visit = Desired.VisitedOn.Value.ToDateTime(Desired.VisitedAt ?? TimeOnly.MinValue);
        if (visit > localNow.AddMinutes(5))
        {
            yield return new ValidationResult(
                "A visit cannot be in the future.",
                [nameof(Desired)]);
        }
    }
}
