using System.ComponentModel.DataAnnotations;

namespace Api.Controllers.Points;

public record AddVisitRequest(bool IsVisited, DateTime? Visited) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!IsVisited) yield return new ValidationResult("Cannot unvisit stamping points.", new [] { "isVisited" });
    }
}
