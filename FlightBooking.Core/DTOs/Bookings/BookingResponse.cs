using FlightBooking.Core.DTOs.Seats;

namespace FlightBooking.Core.DTOs.Bookings;

public class BookingResponse
{
    public int Id { get; set; }
    public string BookingReference { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public decimal TotalAmount { get; set; }

    public string PassengerFullName { get; set; } = string.Empty;
    public string MealType { get; set; } = string.Empty;
    public string KashrutLevel { get; set; } = string.Empty;
    public string? SpecialRequests { get; set; }

    public List<FlightSegmentResponse> Flights { get; set; } = new();
    public List<SeatResponse> Seats { get; set; } = new();
}

