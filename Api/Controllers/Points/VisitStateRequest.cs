using System.ComponentModel.DataAnnotations;

namespace Api.Controllers.Points;

public sealed record VisitStateRequest(bool IsVisited, DateOnly? VisitedOn, TimeOnly? VisitedAt) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!IsVisited && (VisitedOn.HasValue || VisitedAt.HasValue))
        {
            yield return new ValidationResult(
                "An open visit state cannot contain a visit date or time.",
                [nameof(VisitedOn), nameof(VisitedAt)]);
        }

        if (VisitedAt.HasValue && !VisitedOn.HasValue)
        {
            yield return new ValidationResult(
                "A visit time requires a visit date.",
                [nameof(VisitedAt)]);
        }
    }
}
