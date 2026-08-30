using System.ComponentModel.DataAnnotations;

namespace Api.Controllers.Points;

public sealed record SaveVisitRequest(DateOnly? VisitedOn, TimeOnly? VisitedAt, int? UtcOffsetMinutes = null) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (VisitedAt.HasValue && !VisitedOn.HasValue)
        {
            yield return new ValidationResult(
                "A visit time requires a visit date.",
                [nameof(VisitedAt)]);
            yield break;
        }

        if (!VisitedOn.HasValue)
        {
            yield break;
        }

        if (UtcOffsetMinutes is < -840 or > 840)
        {
            yield return new ValidationResult(
                "The UTC offset must be between -840 and 840 minutes.",
                [nameof(UtcOffsetMinutes)]);
            yield break;
        }

        var localNow = UtcOffsetMinutes.HasValue
            ? DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromMinutes(UtcOffsetMinutes.Value)).DateTime
            : DateTime.Now;
        var visit = VisitedOn.Value.ToDateTime(VisitedAt ?? TimeOnly.MinValue);
        if (visit > localNow.AddMinutes(5))
        {
            yield return new ValidationResult(
                "A visit cannot be in the future.",
                [nameof(VisitedOn), nameof(VisitedAt)]);
        }
    }
}
