namespace FlightBooking.Core.DTOs.Bookings;

public class BookingSummaryResponse
{
    public int Id { get; set; }
    public string BookingReference { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public decimal TotalAmount { get; set; }
    public int FlightsCount { get; set; }
    public int SeatsCount { get; set; }
    public DateTime? FirstDepartureTime { get; set; }
}