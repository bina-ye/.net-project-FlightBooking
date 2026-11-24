using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


using System.ComponentModel.DataAnnotations;

namespace FlightBooking.Core.DTOs.Bookings;

public class BookingCreateRequest
{
    [Required(ErrorMessage = "חובה לבחור לפחות מושב אחד")]
    [MinLength(1, ErrorMessage = "חובה לבחור לפחות מושב אחד")]
    public List<int> SeatIds { get; set; } = new();

    [Required(ErrorMessage = "שם נוסע חובה")]
    [MaxLength(200)]
    public string PassengerFullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "מספר דרכון חובה")]
    [MaxLength(30)]
    public string PassportNumber { get; set; } = string.Empty;

    // 0–4 לפי MealType
    [Range(0, 4, ErrorMessage = "סוג ארוחה אינו תקין")]
    public int MealType { get; set; }

    // 0–3 לפי KashrutLevel
    [Range(0, 3, ErrorMessage = "רמת כשרות אינו תקינה")]
    public int KashrutLevel { get; set; }

    [MaxLength(500)]
    public string? SpecialRequests { get; set; }

    public PaymentRequest Payment { get; set; } = new();
}
