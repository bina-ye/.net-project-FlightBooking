//using FlightBooking.Core.Entities;
//using FlightBooking.Core.Enums;

//namespace FlightBooking.Core.Seats;

//public static class SeatFactory
//{
//    // 6 מושבים בשורה (3+3), 30 שורות = 180 מקומות
//    private const int Rows = 30;
//    private const int SeatsPerRow = 6;

//    public static List<Seat> CreateForFlight(Flight flight)
//    {
//        // A ו-F ליד החלון, C ו-D ליד המעבר
//        char[] letters = ['A', 'B', 'C', 'D', 'E', 'F'];
//        var seats = new List<Seat>(Rows * SeatsPerRow);

//        for (var row = 1; row <= Rows; row++)
//        {
//            for (var i = 0; i < SeatsPerRow; i++)
//            {
//                seats.Add(new Seat
//                {
//                    Flight = flight,          // קשר אובייקטים — מאפשר שמירה אחת
//                    SeatNumber = $"{row}{letters[i]}",
//                    Status = SeatStatus.Available
//                });
//            }
//        }

//        return seats;
//    }
//}