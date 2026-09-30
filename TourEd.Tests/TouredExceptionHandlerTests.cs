using System.Text.Json;
using Api.ErrorHandling;
using Api.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using TourEd.Lib.Abstractions.Exceptions;
using TourEd.Lib.Abstractions.Models;

namespace TourEd.Tests;

public sealed class TouredExceptionHandlerTests : IAsyncDisposable
{
    private readonly ServiceProvider _services = new ServiceCollection()
        .AddOptions()
        .AddProblemDetails()
        .BuildServiceProvider();

    public static TheoryData<Exception, int> DomainExceptions => new()
    {
        { new RequestValidationException("Invalid input."), StatusCodes.Status400BadRequest },
        { new AccessDeniedException("Not entitled."), StatusCodes.Status403Forbidden },
        { EntityNotFoundException.Create<StampingPoint>(42), StatusCodes.Status404NotFound },
        { new ConflictException("Already visited."), StatusCodes.Status409Conflict },
        { new RegistrationRequestAlreadyDecidedException(7), StatusCodes.Status409Conflict }
    };

    public static TheoryData<Exception> UnexpectedExceptions => new()
    {
        new InvalidOperationException("Sequence contains more than one element."),
        new UnauthorizedAccessException("Access to the path is denied."),
        new InvalidDataException("Unexpected source data."),
        new NotSupportedException()
    };

    [Theory]
    [MemberData(nameof(DomainExceptions))]
    public async Task MapsDomainExceptionsToTheirStatusCode(Exception exception, int expectedStatus)
    {
        var context = CreateContext();

        var handled = await CreateHandler().TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(expectedStatus, context.Response.StatusCode);
        var problem = await ReadProblemAsync(context);
        Assert.Equal(expectedStatus, problem.Status);
    }

    [Fact]
    public async Task ReturnsValidationMessageAsDetail()
    {
        var context = CreateContext();

        await CreateHandler().TryHandleAsync(context, new RequestValidationException("Invalid latitude."), CancellationToken.None);

        var problem = await ReadProblemAsync(context);
        Assert.Equal("Validation failed", problem.Title);
        Assert.Equal("Invalid latitude.", problem.Detail);
    }

    [Theory]
    [InlineData(StatusCodes.Status403Forbidden)]
    [InlineData(StatusCodes.Status404NotFound)]
    [InlineData(StatusCodes.Status409Conflict)]
    public async Task DoesNotExposeInternalMessagesForOtherStatusCodes(int status)
    {
        Exception exception = status switch
        {
            StatusCodes.Status403Forbidden => new AccessDeniedException("internal access detail"),
            StatusCodes.Status404NotFound => EntityNotFoundException.Create<User>("internal key"),
            _ => new ConflictException("internal conflict detail")
        };
        var context = CreateContext();

        await CreateHandler().TryHandleAsync(context, exception, CancellationToken.None);

        var problem = await ReadProblemAsync(context);
        Assert.Null(problem.Detail);
    }

    [Theory]
    [MemberData(nameof(UnexpectedExceptions))]
    public async Task LeavesUnexpectedExceptionsUnhandled(Exception exception)
    {
        var context = CreateContext();

        var handled = await CreateHandler().TryHandleAsync(context, exception, CancellationToken.None);

        Assert.False(handled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(0, context.Response.Body.Length);
    }

    [Fact]
    public async Task KeepsStatusCodeWhenClientDoesNotAcceptProblemDetails()
    {
        var context = CreateContext();
        context.Request.Headers.Accept = "application/geo+json";

        var handled = await CreateHandler().TryHandleAsync(context, new AccessDeniedException("Not entitled."), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    public ValueTask DisposeAsync() => _services.DisposeAsync();

    private TouredExceptionHandler CreateHandler()
        => new(_services.GetRequiredService<IProblemDetailsService>());

    private DefaultHttpContext CreateContext()
        => new()
        {
            RequestServices = _services,
            Response = { Body = new MemoryStream() }
        };

    private static async Task<ProblemDetails> ReadProblemAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        var problem = await JsonSerializer.DeserializeAsync<ProblemDetails>(
            context.Response.Body,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(problem);
        return problem;
    }
}
