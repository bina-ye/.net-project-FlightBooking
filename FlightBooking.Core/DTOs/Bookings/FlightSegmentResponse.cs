using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlightBooking.Core.DTOs.Bookings
{

    public class FlightSegmentResponse
    {
        public int FlightId { get; set; }
        public string FlightNumber { get; set; } = string.Empty;
        public string OriginCode { get; set; } = string.Empty;
        public string DestinationCode { get; set; } = string.Empty;
        public DateTime DepartureTime { get; set; }
        public DateTime ArrivalTime { get; set; }

        // פרטי ההגעה המדויקים שהלקוח מקבל באישור
        public DateTime CheckInOpensAt { get; set; }
        public DateTime BoardingTime { get; set; }
        public string Gate { get; set; } = string.Empty;
        public int SegmentOrder { get; set; }
    }

}
