using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FlightBooking.Core.Enums;

namespace FlightBooking.Core.Entities
{

    public class Flight
    {
        public int Id { get; set; }
        public string FlightNumber { get; set; } = string.Empty;

        public int OriginAirportId { get; set; }
        public Airport OriginAirport { get; set; } = null!;

        public int DestinationAirportId { get; set; }
        public Airport DestinationAirport { get; set; } = null!;

        public DateTime DepartureTime { get; set; }
        public DateTime ArrivalTime { get; set; }

        public DateTime CheckInOpensAt { get; set; }
        public DateTime BoardingTime { get; set; }
        public string Gate { get; set; } = string.Empty;

        public decimal Price { get; set; }
        public string AircraftType { get; set; } = string.Empty;

        public ICollection<Seat> Seats { get; set; } = new List<Seat>();

        public ICollection<BookingFlight> BookingFlights { get; set; } = new List<BookingFlight>();
    }

}
