namespace TourEd.Lib.Abstractions.Models;

public sealed class AdminAuditEntry
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public int ActorUserId { get; set; }
    public string Action { get; set; } = null!;
    public int? TargetUserId { get; set; }
    public int? RegistrationRequestId { get; set; }
    public string? ProviderSlug { get; set; }
}
