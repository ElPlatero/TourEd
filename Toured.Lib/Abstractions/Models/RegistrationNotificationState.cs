namespace TourEd.Lib.Abstractions.Models;

public sealed class RegistrationNotificationState
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;
    public DateTime? LastSentAt { get; set; }
}
