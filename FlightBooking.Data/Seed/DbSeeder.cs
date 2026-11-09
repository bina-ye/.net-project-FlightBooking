using FlightBooking.Core.Entities;
using FlightBooking.Core.Enums;
using FlightBooking.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace FlightBooking.Data.Seed;

public static class DbSeeder
{
    public static async Task SeedAsync(FlightBookingDbContext context, CancellationToken ct = default)
    {
        await context.Database.MigrateAsync(ct);

        if (await context.Airports.AnyAsync(ct))
            return;   // כבר זרוע — לא לדרוס

        var tlv = new Airport { Code = "TLV", City = "תל אביב", Country = "ישראל", Name = "נתב\"ג" };
        var jfk = new Airport { Code = "JFK", City = "ניו יורק", Country = "ארה\"ב", Name = "John F. Kennedy" };
        var lhr = new Airport { Code = "LHR", City = "לונדון", Country = "אנגליה", Name = "Heathrow" };
        var cdg = new Airport { Code = "CDG", City = "פריז", Country = "צרפת", Name = "Charles de Gaulle" };

        context.Airports.AddRange(tlv, jfk, lhr, cdg);
        await context.SaveChangesAsync(ct);

        var baseDate = DateTime.UtcNow.Date.AddDays(30);

        var flights = new List<Flight>
        {
            NewFlight("LY001", tlv.Id, jfk.Id, baseDate.AddHours(6), 11, 1290m),
            NewFlight("LY002", jfk.Id, tlv.Id, baseDate.AddDays(1).AddHours(18), 11, 1340m),
            NewFlight("LY315", tlv.Id, lhr.Id, baseDate.AddHours(9), 5, 780m),
            NewFlight("LY316", lhr.Id, tlv.Id, baseDate.AddDays(1).AddHours(13), 5, 810m),
            NewFlight("LY321", tlv.Id, cdg.Id, baseDate.AddDays(2).AddHours(8), 5, 690m),
            NewFlight("LY322", cdg.Id, tlv.Id, baseDate.AddDays(9).AddHours(15), 5, 720m),
            NewFlight("LY003", tlv.Id, jfk.Id, baseDate.AddDays(3).AddHours(22), 12, 1190m),
            NewFlight("LY004", jfk.Id, tlv.Id, baseDate.AddDays(4).AddHours(11), 12, 1250m)
        };

        context.Flights.AddRange(flights);
        await context.SaveChangesAsync(ct);

        var rows = new[] { "1", "2", "3", "4", "5", "6" };
        var letters = new[] { "A", "B", "C", "D", "E" };

        var seats = new List<Seat>();
        foreach (var flight in flights)
        {
            foreach (var row in rows)
            {
                foreach (var letter in letters)
                {
                    seats.Add(new Seat
                    {
                        FlightId = flight.Id,
                        SeatNumber = row + letter,
                        Status = SeatStatus.Available
                    });
                }
            }
        }

        context.Seats.AddRange(seats);

        context.Users.AddRange(
            new AppUser
            {
                Email = "admin@flightbooking.local",
                FullName = "מנהל המערכת",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
                Role = UserRole.Admin
            },
            new AppUser
            {
                Email = "customer@flightbooking.local",
                FullName = "לקוח לדוגמה",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
                Role = UserRole.Customer
            });

        await context.SaveChangesAsync(ct);
    }

    private static Flight NewFlight(
        string number, int originId, int destinationId,
        DateTime departure, int hours, decimal price)
    {
        return new Flight
        {
            FlightNumber = number,
            OriginAirportId = originId,
            DestinationAirportId = destinationId,
            DepartureTime = departure,
            ArrivalTime = departure.AddHours(hours),
            CheckInOpensAt = departure.AddHours(-3),
            BoardingTime = departure.AddMinutes(-40),
            Gate = "A" + Random.Shared.Next(1, 20),
            Price = price,
            AircraftType = "Boeing 787"
        };
    }
}