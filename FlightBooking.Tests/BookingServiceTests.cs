using FlightBooking.Core.Common;
using FlightBooking.Core.DTOs.Bookings;
using FlightBooking.Core.Entities;
using FlightBooking.Core.Enums;
using FlightBooking.Core.Interfaces;
using FlightBooking.Service.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FlightBooking.Tests;

public class BookingServiceTests
{
    private const int UserId = 7;

    private readonly Mock<IBookingRepository> _bookings = new();
    private readonly Mock<IFlightRepository> _flights = new();
    private readonly Mock<IPaymentService> _payments = new();

    private BookingService CreateSut() => new(
        _bookings.Object,
        _flights.Object,
        _payments.Object,
        NullLogger<BookingService>.Instance);

    // ---------- עזרי בנייה ----------

    private static Flight BuildFlight(DateTime? departure = null) => new()
    {
        Id = 1,
        FlightNumber = "LY001",
        Price = 1290m,
        DepartureTime = departure ?? DateTime.UtcNow.AddDays(10),
        ArrivalTime = (departure ?? DateTime.UtcNow.AddDays(10)).AddHours(11),
        CheckInOpensAt = DateTime.UtcNow.AddDays(9),
        BoardingTime = DateTime.UtcNow.AddDays(9).AddHours(22),
        Gate = "B12",
        OriginAirport = new Airport { Id = 1, Code = "TLV", Name = "נתב\"ג" },
        DestinationAirport = new Airport { Id = 2, Code = "JFK", Name = "קנדי" }
    };

    private static Seat BuildSeat(int id, string number, SeatStatus status, Flight flight) => new()
    {
        Id = id,
        SeatNumber = number,
        Status = status,
        FlightId = flight.Id,
        Flight = flight
    };

    private static BookingCreateRequest BuildRequest(params int[] seatIds) => new()
    {
        SeatIds = seatIds.ToList(),
        PassengerFullName = "רותי קאופמן",
        PassportNumber = "12345678",
        MealType = (int)MealType.Regular,
        KashrutLevel = (int)KashrutLevel.Kosher,
        SpecialRequests = null,
        Payment = new PaymentRequest
        {
            CardNumber = "4111111111111111",
            Expiry = "12/28",
            Cvv = "123"
        }
    };

    // ---------- בדיקות ----------

    [Fact]
    public async Task CreateAsync_HappyPath_ReturnsCreated()
    {
        // Arrange
        var flight = BuildFlight();
        var seat = BuildSeat(1, "1A", SeatStatus.Available, flight);

        _flights.Setup(r => r.GetSeatWithFlightAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(seat);

        _payments.Setup(p => p.ChargeAsync(It.IsAny<decimal>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new PaymentResult(true, "tx-1", null));

        _bookings.Setup(r => r.ReferenceExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(false);
        _bookings.Setup(r => r.AddAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);
        _bookings.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);

        var sut = CreateSut();

        // Act
        var result = await sut.CreateAsync(UserId, BuildRequest(1));

        // Assert
        Assert.Equal(ResultStatus.Created, result.Status);
        Assert.NotNull(result.Value);
        Assert.Equal(1290m, result.Value!.TotalAmount);
        Assert.Single(result.Value.Seats);
        Assert.Equal(SeatStatus.Booked, seat.Status);

        _bookings.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_SeatAlreadyBooked_ReturnsConflict_AndNoPayment()
    {
        // Arrange
        var flight = BuildFlight();
        var seat = BuildSeat(1, "1A", SeatStatus.Booked, flight);

        _flights.Setup(r => r.GetSeatWithFlightAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(seat);

        var sut = CreateSut();

        // Act
        var result = await sut.CreateAsync(UserId, BuildRequest(1));

        // Assert
        Assert.Equal(ResultStatus.Conflict, result.Status);

        // לא חויב כרטיס כשהמושב תפוס
        _payments.Verify(p => p.ChargeAsync(It.IsAny<decimal>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_FlightAlreadyDeparted_ReturnsBadRequest()
    {
        // Arrange
        var flight = BuildFlight(DateTime.UtcNow.AddDays(-1));
        var seat = BuildSeat(1, "1A", SeatStatus.Available, flight);

        _flights.Setup(r => r.GetSeatWithFlightAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(seat);

        var sut = CreateSut();

        // Act
        var result = await sut.CreateAsync(UserId, BuildRequest(1));

        // Assert
        Assert.Equal(ResultStatus.BadRequest, result.Status);
        _payments.Verify(p => p.ChargeAsync(It.IsAny<decimal>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_PaymentFails_ReturnsBadRequest()
    {
        // Arrange
        var flight = BuildFlight();
        var seat = BuildSeat(1, "1A", SeatStatus.Available, flight);

        _flights.Setup(r => r.GetSeatWithFlightAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(seat);

        _payments.Setup(p => p.ChargeAsync(It.IsAny<decimal>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new PaymentResult(false, null, "התשלום נדחה על ידי חברת האשראי."));

        var sut = CreateSut();

        // Act
        var result = await sut.CreateAsync(UserId, BuildRequest(1));

        // Assert
        Assert.Equal(ResultStatus.BadRequest, result.Status);
        Assert.Contains("נדחה", result.ErrorMessage);

        // לא נשמרה הזמנה
        _bookings.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_SeatNotFound_ReturnsNotFound()
    {
        // Arrange
        _flights.Setup(r => r.GetSeatWithFlightAsync(99, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Seat?)null);

        var sut = CreateSut();

        // Act
        var result = await sut.CreateAsync(UserId, BuildRequest(99));

        // Assert
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task CreateAsync_DuplicateSeatIds_ReturnsBadRequest()
    {
        var sut = CreateSut();

        var result = await sut.CreateAsync(UserId, BuildRequest(1, 1));

        Assert.Equal(ResultStatus.BadRequest, result.Status);
    }

    [Fact]
    public async Task CreateAsync_EmptySeatList_ReturnsBadRequest()
    {
        var sut = CreateSut();

        var result = await sut.CreateAsync(UserId, BuildRequest());

        Assert.Equal(ResultStatus.BadRequest, result.Status);
    }

    [Fact]
    public async Task CreateAsync_SeatsFromDifferentFlights_ReturnsBadRequest()
    {
        // Arrange
        var f1 = BuildFlight();
        var f2 = BuildFlight();
        f2.Id = 2;
        f2.FlightNumber = "LY002";

        _flights.Setup(r => r.GetSeatWithFlightAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(BuildSeat(1, "1A", SeatStatus.Available, f1));
        _flights.Setup(r => r.GetSeatWithFlightAsync(2, It.IsAny<CancellationToken>()))
                .ReturnsAsync(BuildSeat(2, "1A", SeatStatus.Available, f2));

        var sut = CreateSut();

        // Act
        var result = await sut.CreateAsync(UserId, BuildRequest(1, 2));

        // Assert
        Assert.Equal(ResultStatus.BadRequest, result.Status);
    }
}