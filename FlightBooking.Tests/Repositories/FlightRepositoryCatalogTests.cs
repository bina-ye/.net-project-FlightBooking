using FlightBooking.Core.Entities;
using FlightBooking.Data.Context;
using FlightBooking.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FlightBooking.Tests.Integration;

public class FlightRepositoryCatalogTests
{
    private static FlightBookingDbContext CreateContext(string dbName) =>
        new(new DbContextOptionsBuilder<FlightBookingDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options);

    [Fact]
    public async Task GetCatalogAsync_HidesDepartedFlights_WhenIncludePastIsFalse()
    {
        var dbName = Guid.NewGuid().ToString();
        var baseTime = new DateTime(2026, 12, 20, 8, 0, 0, DateTimeKind.Utc);

        await using (var ctx = CreateContext(dbName))
        {
            ctx.Airports.AddRange(
                new Airport { Id = 1, Code = "TLV", City = "תל אביב" },
                new Airport { Id = 2, Code = "JFK", City = "ניו יורק" });

            ctx.Flights.AddRange(
                TestFlight(1, "LY001", baseTime, past: true),
                TestFlight(2, "LY002", baseTime.AddDays(30), past: false),
                TestFlight(3, "LY003", baseTime.AddDays(60), past: false));

            await ctx.SaveChangesAsync();
        }

        await using var db = CreateContext(dbName);
        var repo = new FlightRepository(db);

        var (items, total) = await repo.GetCatalogAsync(1, 10, includePast: false);

        Assert.Equal(2, total);
        Assert.DoesNotContain(items, f => f.FlightNumber == "LY001");
        Assert.All(items, f => Assert.True(f.DepartureTime > DateTime.UtcNow));
    }

    [Fact]
    public async Task GetCatalogAsync_IncludesDepartedFlights_WhenIncludePastIsTrue()
    {
        var dbName = Guid.NewGuid().ToString();
        var baseTime = new DateTime(2026, 12, 20, 8, 0, 0, DateTimeKind.Utc);

        await using (var ctx = CreateContext(dbName))
        {
            ctx.Airports.AddRange(
                new Airport { Id = 1, Code = "TLV", City = "תל אביב" },
                new Airport { Id = 2, Code = "JFK", City = "ניו יורק" });

            ctx.Flights.AddRange(
                TestFlight(1, "LY001", baseTime, past: true),
                TestFlight(2, "LY002", baseTime.AddDays(30), past: false));

            await ctx.SaveChangesAsync();
        }

        await using var db = CreateContext(dbName);
        var repo = new FlightRepository(db);

        var (items, total) = await repo.GetCatalogAsync(1, 10, includePast: true);

        Assert.Equal(2, total);
        Assert.Contains(items, f => f.FlightNumber == "LY001");
    }

    [Fact]
    public async Task GetCatalogAsync_PagesCorrectly()
    {
        var dbName = Guid.NewGuid().ToString();
        var baseTime = DateTime.UtcNow.AddDays(10);

        await using (var ctx = CreateContext(dbName))
        {
            ctx.Airports.AddRange(
                new Airport { Id = 1, Code = "TLV", City = "תל אביב" },
                new Airport { Id = 2, Code = "JFK", City = "ניו יורק" });

            for (int i = 1; i <= 7; i++)
                ctx.Flights.Add(TestFlight(i, $"LY{i:000}", baseTime.AddDays(i), past: false));

            await ctx.SaveChangesAsync();
        }

        await using var db = CreateContext(dbName);
        var repo = new FlightRepository(db);

        var (page1, total) = await repo.GetCatalogAsync(1, 3, includePast: false);
        var (page3, _) = await repo.GetCatalogAsync(3, 3, includePast: false);

        Assert.Equal(7, total);
        Assert.Equal(3, page1.Count);
        Assert.Single(page3);
        // סדר עולה לפי זמן המראה
        Assert.True(page1[0].DepartureTime < page1[1].DepartureTime);
    }

    private static Flight TestFlight(int id, string number, DateTime departure, bool past) => new()
    {
        Id = id,
        FlightNumber = number,
        OriginAirportId = 1,
        DestinationAirportId = 2,
        // טיסה "שעברה" — המראה לפני עכשיו; עתידית — אחרי עכשיו
        DepartureTime = past ? DateTime.UtcNow.AddDays(-5) : departure,
        ArrivalTime = (past ? DateTime.UtcNow.AddDays(-5) : departure).AddHours(5),
        CheckInOpensAt = departure.AddHours(-3),
        BoardingTime = departure.AddMinutes(-40),
        Gate = "A1",
        Price = 1000m,
        AircraftType = "Boeing 787"
    };
}