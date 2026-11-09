namespace FlightBooking.API.Middleware;

public class ShabbatMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ShabbatMiddleware> _logger;
    private readonly IConfiguration _configuration;

    public ShabbatMiddleware(RequestDelegate next, ILogger<ShabbatMiddleware> logger, IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // ניתן לכבות את הבדיקה בפיתוח ובבדיקות
        var enabled = _configuration.GetValue("Shabbat:Enabled", true);
        if (!enabled || IsExempt(context.Request.Path))
        {
            await _next(context);
            return;
        }

        if (IsShabbat(DateTime.UtcNow))
        {
            _logger.LogInformation("Request blocked: Shabbat. Path={Path}", context.Request.Path);

            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "application/json";

            await context.Response.WriteAsync("""
            {
              "status": 503,
              "title": "המערכת אינה פעילה בשבת.",
              "detail": "ניתן לחזור ולבצע הזמנות במוצאי שבת."
            }
            """);
            return;
        }

        await _next(context);
    }

    private static bool IsExempt(PathString path) =>
        path.StartsWithSegments("/swagger") ||
        path.StartsWithSegments("/health");

    // מקום לחיבור לוגיקה מדויקת של זמני שבת (שקיעה/צאת כוכבים לפי מיקום)
    private bool IsShabbat(DateTime utcNow)
    {
        var tzId = _configuration["Shabbat:TimeZone"] ?? "Asia/Jerusalem";
        var tz = TimeZoneInfo.FindSystemTimeZoneById(tzId);
        var local = TimeZoneInfo.ConvertTimeFromUtc(utcNow, tz);

        // TODO: להחליף בחישוב זמני שבת אמיתי, או לקרוא זמנים מקונפיגורציה
        var start = _configuration.GetValue<TimeSpan?>("Shabbat:Start") ?? TimeSpan.FromHours(15);
        var end = _configuration.GetValue<TimeSpan?>("Shabbat:End") ?? TimeSpan.FromHours(19);

        return local.DayOfWeek switch
        {
            DayOfWeek.Friday => local.TimeOfDay >= start,
            DayOfWeek.Saturday => local.TimeOfDay < end,
            _ => false
        };
    }
}

public static class ShabbatMiddlewareExtensions
{
    public static IApplicationBuilder UseShabbat(this IApplicationBuilder app)
        => app.UseMiddleware<ShabbatMiddleware>();
}