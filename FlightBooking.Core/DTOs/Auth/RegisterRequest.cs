using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.ComponentModel.DataAnnotations;

namespace FlightBooking.Core.DTOs.Auth;

public class RegisterRequest
{
    [Required(ErrorMessage = "אימייל חובה")]
    [EmailAddress(ErrorMessage = "כתובת אימייל אינה תקינה")]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "סיסמה חובה")]
    [MinLength(8, ErrorMessage = "סיסמה חייבת להכיל לפחות 8 תווים")]
    [MaxLength(100)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "שם מלא חובה")]
    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;
}
