using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace FlightBooking.Core.DTOs.Flights;

public class FlightResponse
{
    public int Id { get; set; }
    public string FlightNumber { get; set; } = string.Empty;

    public string OriginCode { get; set; } = string.Empty;
    public string OriginCity { get; set; } = string.Empty;

    public string DestinationCode { get; set; } = string.Empty;
    public string DestinationCity { get; set; } = string.Empty;

    public DateTime DepartureTime { get; set; }
    public DateTime ArrivalTime { get; set; }

    public DateTime CheckInOpensAt { get; set; }
    public DateTime BoardingTime { get; set; }
    public string Gate { get; set; } = string.Empty;

    public decimal Price { get; set; }
    public string AircraftType { get; set; } = string.Empty;

    public int AvailableSeatsCount { get; set; }
}
