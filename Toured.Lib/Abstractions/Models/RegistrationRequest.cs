namespace TourEd.Lib.Abstractions.Models;

public enum RegistrationRequestStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}

public sealed class RegistrationRequest
{
    public int Id { get; set; }
    public string GoogleSubject { get; set; } = null!;
    public string Email { get; set; } = null!;
    public RegistrationRequestStatus Status { get; set; } = RegistrationRequestStatus.Pending;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DecidedAt { get; set; }
    public DateTime? AdminNotificationSentAt { get; set; }
}
