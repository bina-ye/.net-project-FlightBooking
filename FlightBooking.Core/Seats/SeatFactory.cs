using FlightBooking.Core.Entities;
using FlightBooking.Core.Enums;

namespace FlightBooking.Core.Seats;

public static class SeatFactory
{
    private const int Rows = 30;
    private const int SeatsPerRow = 6;

    public static List<Seat> CreateForFlight(Flight flight)
    {
        char[] letters = ['A', 'B', 'C', 'D', 'E', 'F'];
        var seats = new List<Seat>(Rows * SeatsPerRow);

        for (var row = 1; row <= Rows; row++)
            for (var i = 0; i < SeatsPerRow; i++)
                seats.Add(new Seat
                {
                    Flight = flight,
                    SeatNumber = $"{row}{letters[i]}",
                    Status = SeatStatus.Available
                });

        return seats;
    }
}