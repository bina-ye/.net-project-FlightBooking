using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FlightBooking.Core.Enums;

namespace FlightBooking.Core.Entities;

public class Seat
{
    public int Id { get; set; }

    public int FlightId { get; set; }
    public Flight Flight { get; set; } = null!;

    public string SeatNumber { get; set; } = string.Empty;   // למשל "12A"
    public SeatStatus Status { get; set; } = SeatStatus.Available;

    // concurrency token — ממופה ל-xmin של PostgreSQL ב-Fluent API
    public uint RowVersion { get; set; }

    public DateTime? BookedAt { get; set; }

    public int? BookingId { get; set; }
    public Booking? Booking { get; set; }
}