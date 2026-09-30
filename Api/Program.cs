using System.Text.Json;
using System.Text.Json.Serialization;
using Api.ErrorHandling;
using Api.Extensions;
using Api.Repositories;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOpenApi("toured")
    .AddSingleton<TimeProvider>(TimeProvider.System)
    .AddImportServices(builder.Configuration)
    .AddRepositories()
    .AddManagers()
    .AddBackgroundServices(builder.Configuration)
    .AddTouredAuthentication(builder.Configuration)
    .AddTouredDataProtection(builder.Configuration)
    .AddEndpointsApiExplorer()
    .AddDbContext<DataContext>(options => options.UseSqlite(builder.Configuration.GetConnectionString("TouredDb")))
    .AddTouredHealthChecks()
    .AddProblemDetails()
    .AddExceptionHandler<TouredExceptionHandler>()
    .AddControllers().AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi("/openapi/{documentName}.json");
}

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                       ForwardedHeaders.XForwardedHost |
                       ForwardedHeaders.XForwardedProto
});
var configuredPathBase = builder.Configuration["PathBase"];
if (!string.IsNullOrWhiteSpace(configuredPathBase))
{
    app.UsePathBase(configuredPathBase);
}
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health").AllowAnonymous();
app.MapControllers();

app.Run();

public partial class Program
{
}
