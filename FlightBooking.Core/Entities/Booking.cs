using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FlightBooking.Core.Enums;

namespace FlightBooking.Core.Entities;

public class Booking
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public string BookingReference { get; set; } = string.Empty;   // קוד ייחודי ללקוח
    public BookingStatus Status { get; set; } = BookingStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public decimal TotalAmount { get; set; }

    // פרטי הנוסע
    public string PassengerFullName { get; set; } = string.Empty;
    public string PassportNumber { get; set; } = string.Empty;

    // טופס העדפות שאחרי ההזמנה
    public MealType MealType { get; set; } = MealType.None;
    public KashrutLevel KashrutLevel { get; set; } = KashrutLevel.None;
    public string? SpecialRequests { get; set; }

    public ICollection<Seat> Seats { get; set; } = new List<Seat>();
    public ICollection<BookingFlight> BookingFlights { get; set; } = new List<BookingFlight>();
}
