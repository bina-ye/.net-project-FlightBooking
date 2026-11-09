using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace FlightBooking.API.Middleware;

public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger, IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var correlationId = context.Items[CorrelationIdMiddleware.HeaderName]?.ToString();

            // לוג מלא בצד השרת בלבד — הלקוח מקבל הודעה כללית
            _logger.LogError(ex, "Unhandled exception. CorrelationId={CorrelationId}", correlationId);

            if (context.Response.HasStarted)
            {
                _logger.LogWarning("Response already started, cannot write error body.");
                throw;
            }

            var (status, message) = MapException(ex);

            context.Response.Clear();
            context.Response.StatusCode = (int)status;
            context.Response.ContentType = "application/problem+json";

            var problem = new ProblemDetails
            {
                Status = (int)status,
                Title = message,
                Instance = context.Request.Path,
                Extensions =
                {
                    ["correlationId"] = correlationId
                }
            };

            // פרטים פנימיים רק בפיתוח
            if (_env.IsDevelopment())
                problem.Detail = ex.ToString();

            await context.Response.WriteAsync(JsonSerializer.Serialize(problem));
        }
    }

    private static (HttpStatusCode, string) MapException(Exception ex) => ex switch
    {
        ArgumentException => (HttpStatusCode.BadRequest, "הבקשה אינה תקינה."),
        KeyNotFoundException => (HttpStatusCode.NotFound, "המשאב המבוקש לא נמצא."),
        UnauthorizedAccessException => (HttpStatusCode.Forbidden, "אין הרשאה לביצוע הפעולה."),
        InvalidOperationException => (HttpStatusCode.InternalServerError, "אירעה שגיאה בלתי צפויה. נסו שוב או פנו לתמיכה."),
        _ => (HttpStatusCode.InternalServerError, "אירעה שגיאה בלתי צפויה. נסו שוב או פנו לתמיכה.")
    };
}

public static class ErrorHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseErrorHandling(this IApplicationBuilder app)
        => app.UseMiddleware<ErrorHandlingMiddleware>();
}