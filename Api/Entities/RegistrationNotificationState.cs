using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Entities;

public sealed class RegistrationNotificationState
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;
    public DateTime? LastSentAt { get; set; }

    internal sealed class Configuration : IEntityTypeConfiguration<RegistrationNotificationState>
    {
        public void Configure(EntityTypeBuilder<RegistrationNotificationState> builder)
        {
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Id).ValueGeneratedNever();
            builder.HasData(new RegistrationNotificationState());
        }
    }
}
