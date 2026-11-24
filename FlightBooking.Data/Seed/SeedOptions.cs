namespace FlightBooking.Data.Seed;

/// <summary>
/// פרטי משתמשי הדמו שנוצרים ב-seed.
/// הערכים מגיעים מ-appsettings (פיתוח) או ממשתני סביבה (פריסה),
/// ולכן סיסמת המנהל אינה כתובה בקוד.
/// </summary>
public class SeedOptions
{
    public string AdminEmail { get; set; } = "admin@flightbooking.local";
    public string AdminPassword { get; set; } = string.Empty;

    public string CustomerEmail { get; set; } = "customer@flightbooking.local";
    public string CustomerPassword { get; set; } = string.Empty;
}
