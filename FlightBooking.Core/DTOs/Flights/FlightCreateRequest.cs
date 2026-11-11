using System.ComponentModel.DataAnnotations;

namespace FlightBooking.Core.DTOs.Flights;

public class FlightCreateRequest
{
    [Required, StringLength(10, MinimumLength = 2)]
    public string FlightNumber { get; set; } = string.Empty;

    [Required, StringLength(3, MinimumLength = 3)]
    public string OriginCode { get; set; } = string.Empty;

    [Required, StringLength(3, MinimumLength = 3)]
    public string DestinationCode { get; set; } = string.Empty;

    [Required] public DateTime DepartureTime { get; set; }
    [Required] public DateTime ArrivalTime { get; set; }

    [Range(1, 100000)] public decimal Price { get; set; }

    [Required, StringLength(10)] public string Gate { get; set; } = string.Empty;
    [Required, StringLength(50)] public string AircraftType { get; set; } = string.Empty;
}