using System.ComponentModel.DataAnnotations;

namespace FlightBooking.Core.DTOs.Auth;

public class LoginRequest
{
    [Required(ErrorMessage = "אימייל חובה")]
    [EmailAddress(ErrorMessage = "כתובת אימייל אינה תקינה")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "סיסמה חובה")]
    public string Password { get; set; } = string.Empty;
}