using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlightBooking.Core.Entities;

public class BookingFlight
{
    public int BookingId { get; set; }
    public Booking Booking { get; set; } = null!;

    public int FlightId { get; set; }
    public Flight Flight { get; set; } = null!;

    public int SegmentOrder { get; set; }   // 1 = הלוך, 2 = חזור
}