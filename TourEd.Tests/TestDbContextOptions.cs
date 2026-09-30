using Api.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace TourEd.Tests;

/// <summary>Builds <see cref="DataContext"/> options the same way <c>Program</c> does.</summary>
internal static class TestDbContextOptions
{
    public static DbContextOptions<DataContext> For(IConfiguration configuration)
        => new DbContextOptionsBuilder<DataContext>()
            .UseSqlite(configuration.GetConnectionString("TouredDb"))
            .Options;
}
