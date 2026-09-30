using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TourEd.Lib.Abstractions.Models;

namespace Api.Repositories.Configurations;

internal sealed class RegistrationNotificationStateConfiguration : IEntityTypeConfiguration<RegistrationNotificationState>
{
    public void Configure(EntityTypeBuilder<RegistrationNotificationState> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.HasData(new RegistrationNotificationState());
    }
}
