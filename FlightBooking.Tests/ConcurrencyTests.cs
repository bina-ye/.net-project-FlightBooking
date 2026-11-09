using FlightBooking.Core.Entities;
using FlightBooking.Core.Enums;
using FlightBooking.Data.Context;
using Microsoft.EntityFrameworkCore;


namespace FlightBooking.Tests;

/// <summary>
/// בדיקת תחרות אמיתית מול PostgreSQL (xmin).
/// דורשת משתנה סביבה FLIGHTBOOKING_TEST_DB עם מחרוזת חיבור.
/// ללא המשתנה — הבדיקות מדולגות ואינן נכשלות.
/// </summary>
public class ConcurrencyTests
{
    private static string? ConnectionString =>
        Environment.GetEnvironmentVariable("FLIGHTBOOKING_TEST_DB");

    private static bool TestDbMissing => string.IsNullOrWhiteSpace(ConnectionString);

    private static FlightBookingDbContext CreateContext() => new(
        new DbContextOptionsBuilder<FlightBookingDbContext>()
            .UseNpgsql(ConnectionString!)   // מובטח לא-ריק: הבדיקה יוצאת מוקדם אחרת
            .Options);

    // יוצר שני שדות תעופה ומחזיר אותם עם Id אמיתי
    private static async Task<(Airport Origin, Airport Destination)> SeedAirportsAsync(
        FlightBookingDbContext context)
    {
        var origin = new Airport { Code = "TLV", City = "תל אביב", Country = "ישראל", Name = "נתב\"ג" };
        var destination = new Airport { Code = "JFK", City = "ניו יורק", Country = "ארה\"ב", Name = "קנדי" };

        context.Airports.AddRange(origin, destination);
        await context.SaveChangesAsync();

        return (origin, destination);
    }

    [Fact]
    public async Task TwoContexts_BookSameSeat_SecondOne_GetsConcurrencyException()
    {
        // ללא משתנה סביבה — הבדיקה מדולגת ולא נכשלת
        
        if (TestDbMissing)
            return;
        // Arrange
        int seatId;
        await using (var setup = CreateContext())
        {
            await setup.Database.EnsureDeletedAsync();
            await setup.Database.MigrateAsync();

            var (origin, destination) = await SeedAirportsAsync(setup);

            var flight = new Flight
            {
                FlightNumber = "CONC-001",
                Price = 500m,
                AircraftType = "B737",
                DepartureTime = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(5), DateTimeKind.Utc),
                ArrivalTime = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(5).AddHours(4), DateTimeKind.Utc),
                CheckInOpensAt = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(4), DateTimeKind.Utc),
                BoardingTime = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(4).AddHours(23), DateTimeKind.Utc),
                Gate = "C1",
                OriginAirportId = origin.Id,
                DestinationAirportId = destination.Id
            };
            setup.Flights.Add(flight);
            await setup.SaveChangesAsync();

            var seat = new Seat
            {
                FlightId = flight.Id,
                SeatNumber = "9C",
                Status = SeatStatus.Available
            };
            setup.Seats.Add(seat);
            await setup.SaveChangesAsync();

            seatId = seat.Id;
        }

        // Act — שני הקשרים קוראים את אותו מושב, שניהם משנים, הראשון שומר
        await using var ctx1 = CreateContext();
        await using var ctx2 = CreateContext();

        var seat1 = await ctx1.Seats.FirstAsync(s => s.Id == seatId);
        var seat2 = await ctx2.Seats.FirstAsync(s => s.Id == seatId);

        seat1.Status = SeatStatus.Booked;
        seat2.Status = SeatStatus.Booked;

        await ctx1.SaveChangesAsync();   // הראשון מצליח

        // Assert — השני חייב להיכשל
        var ex = await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => ctx2.SaveChangesAsync());

        Assert.NotEmpty(ex.Entries);

        // ולוודא שבבסיס הנתונים אכן נשמר רק שינוי אחד
        await using var verify = CreateContext();
        var final = await verify.Seats.AsNoTracking().FirstAsync(s => s.Id == seatId);
        Assert.Equal(SeatStatus.Booked, final.Status);
    }

    [Fact]
    public async Task TwoContexts_BookDifferentSeats_BothSucceed()
    {
        // ללא משתנה סביבה — הבדיקה מדולגת ולא נכשלת
        if (TestDbMissing)
            return;

        // Arrange
        int seatA, seatB;
        await using (var setup = CreateContext())
        {
            await setup.Database.EnsureDeletedAsync();
            await setup.Database.MigrateAsync();

            var (origin, destination) = await SeedAirportsAsync(setup);

            var flight = new Flight
            {
                FlightNumber = "CONC-002",
                Price = 300m,
                AircraftType = "A320",
                DepartureTime = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(6), DateTimeKind.Utc),
                ArrivalTime = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(6).AddHours(3), DateTimeKind.Utc),
                CheckInOpensAt = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(5), DateTimeKind.Utc),
                BoardingTime = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(5).AddHours(23), DateTimeKind.Utc),
                Gate = "D2",
                OriginAirportId = origin.Id,
                DestinationAirportId = destination.Id
            };
            setup.Flights.Add(flight);
            await setup.SaveChangesAsync();

            var s1 = new Seat { FlightId = flight.Id, SeatNumber = "1A", Status = SeatStatus.Available };
            var s2 = new Seat { FlightId = flight.Id, SeatNumber = "1B", Status = SeatStatus.Available };
            setup.Seats.AddRange(s1, s2);
            await setup.SaveChangesAsync();

            seatA = s1.Id;
            seatB = s2.Id;
        }

        // Act
        await using var ctx1 = CreateContext();
        await using var ctx2 = CreateContext();

        var a = await ctx1.Seats.FirstAsync(s => s.Id == seatA);
        var b = await ctx2.Seats.FirstAsync(s => s.Id == seatB);

        a.Status = SeatStatus.Booked;
        b.Status = SeatStatus.Booked;

        await ctx1.SaveChangesAsync();
        await ctx2.SaveChangesAsync();   // לא אמור לזרוק

        // Assert
        await using var verify = CreateContext();
        Assert.Equal(2, await verify.Seats.CountAsync(s =>
            (s.Id == seatA || s.Id == seatB) && s.Status == SeatStatus.Booked));
    }
}