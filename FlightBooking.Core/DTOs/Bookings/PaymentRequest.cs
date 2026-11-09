using System.ComponentModel.DataAnnotations;

namespace FlightBooking.Core.DTOs.Bookings;

public class PaymentRequest
{
    [Required(ErrorMessage = "מספר כרטיס חובה")]
    [StringLength(16, MinimumLength = 16, ErrorMessage = "מספר כרטיס חייב להיות 16 ספרות")]
    public string CardNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "תוקף הכרטיס חובה")]
    [RegularExpression(@"^(0[1-9]|1[0-2])\/\d{2}$", ErrorMessage = "תוקף בפורמט MM/YY")]
    public string Expiry { get; set; } = string.Empty;

    [Required(ErrorMessage = "CVV חובה")]
    [StringLength(3, MinimumLength = 3, ErrorMessage = "CVV חייב להיות 3 ספרות")]
    public string Cvv { get; set; } = string.Empty;
}