using System.Text.Json;
using System.Text.Json.Serialization;
using Api.Extensions;
using Api.Managers;
using Api.Repositories;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using TourEd.Lib.Abstractions;
using TourEd.Lib.Abstractions.Interfaces.Services;
using TourEd.Lib.Abstractions.Models;
using TourEd.Lib.Abstractions.Options;
using TourEd.Lib.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .Configure<TouringenWebsiteConfiguration>(builder.Configuration.GetSection("touringen"))
    .Configure<HarzerWandernadelConfiguration>(builder.Configuration.GetSection("harzerWandernadel"))
    .AddSingleton(serviceProvider => serviceProvider
        .GetRequiredService<Microsoft.Extensions.Options.IOptions<HarzerWandernadelConfiguration>>().Value)
    .AddOpenApi("toured")
    .AddHttpClient<IHtmlParsingService, HtmlParsingService>().Services
    .AddHttpClient<ITouringenStampingPointImportService, TouringenStampingPointImportService>(client =>
    {
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; TourEd/1.0; +https://baelgun.de/toured/)");
        client.Timeout = TimeSpan.FromSeconds(30);
    }).Services
    .AddSingleton(serviceProvider => serviceProvider
        .GetRequiredService<Microsoft.Extensions.Options.IOptions<TouringenWebsiteConfiguration>>().Value)
    .AddHttpClient<IHarzerWandernadelImportService, HarzerWandernadelImportService>(client =>
    {
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; TourEd/1.0; +https://baelgun.de/toured/)");
        client.Timeout = TimeSpan.FromSeconds(30);
    }).Services
    .AddImportServices()
    .AddRepositories()
    .AddTransient<IUnitOfWork, UnitOfWork>()
    .AddTransient<TourDataManager>()
    .AddTransient<StampingProviderManager>()
    .AddTouredAuthentication(builder.Configuration)
    .AddTouredDataProtection(builder.Configuration)
    .AddSingleton<IHttpContextAccessor, HttpContextAccessor>()
    .AddEndpointsApiExplorer()
    .AddDbContext<DataContext>(options => options.UseSqlite(builder.Configuration.GetConnectionString("TouredDb")))
    .AddTouredHealthChecks()
    .AddControllers().AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });
    

var app = builder.Build();

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
