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
    .AddHttpClient<IHtmlParsingService, HtmlParsingService>().Services
    .AddImportServices()
    .AddRepositories()
    .AddTransient<IUnitOfWork, UnitOfWork>()
    .AddTransient<TourDataManager>()
    .AddAuthentication(EmailHeaderAuthenticationOptions.DefaultScheme).AddScheme<EmailHeaderAuthenticationOptions, TouredAuthenticationHandler>(EmailHeaderAuthenticationOptions.DefaultScheme, options => {}).Services
    .AddSingleton<IHttpContextAccessor, HttpContextAccessor>()
    .AddEndpointsApiExplorer()
    .AddSwaggerGen()
    .AddDbContext<DataContext>(options => options.UseSqlite(builder.Configuration.GetConnectionString("TouredDb")))
    .AddControllers().AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    }).Services
    .AddCors();
    

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// app.UseHttpsRedirection();
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseCors(options => options.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
app.MapControllers().WithOpenApi();

app.Run();
