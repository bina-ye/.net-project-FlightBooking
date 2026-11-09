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

    public int MealType { get; set; }

    public int KashrutLevel { get; set; }

    [MaxLength(500)]
    public string? SpecialRequests { get; set; }

    public PaymentRequest Payment { get; set; } = new();
}
