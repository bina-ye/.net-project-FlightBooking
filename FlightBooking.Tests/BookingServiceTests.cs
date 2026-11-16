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

    private static Booking BuildBooking(
        int id = 1,
        int userId = UserId,
        BookingStatus status = BookingStatus.Confirmed,
        DateTime? departure = null,
        params int[] seatIds)
    {
        var flight = BuildFlight(departure);

        var booking = new Booking
        {
            Id = id,
            UserId = userId,
            BookingReference = "ABC123",
            Status = status,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            TotalAmount = 1290m,
            PassengerFullName = "רותי קאופמן",
            PassportNumber = "12345678",
            MealType = MealType.Regular,
            KashrutLevel = KashrutLevel.Kosher
        };

        booking.BookingFlights.Add(new BookingFlight
        {
            BookingId = id,
            FlightId = flight.Id,
            Flight = flight,
            SegmentOrder = 1
        });

        foreach (var seatId in seatIds)
        {
            var seat = BuildSeat(seatId, $"1{(char)('A' + seatId)}", SeatStatus.Booked, flight);
            seat.BookingId = id;
            seat.BookedAt = DateTime.UtcNow.AddDays(-2);
            booking.Seats.Add(seat);
        }

        return booking;
    }

    private static BookingUpdateRequest BuildUpdateRequest(params int[] seatIds) => new()
    {
        SeatIds = seatIds.ToList(),
        PassengerFullName = "רותי קאופמן-כהן",
        PassportNumber = "87654321",
        MealType = (int)MealType.Vegetarian,
        KashrutLevel = (int)KashrutLevel.Mehadrin,
        SpecialRequests = "מושב ליד מעבר"
    };

    // ---------- CreateAsync ----------

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

    // ---------- CancelAsync ----------

    [Fact]
    public async Task CancelAsync_HappyPath_ReturnsOk_AndReleasesSeats()
    {
        // Arrange
        var booking = BuildBooking(
            departure: DateTime.UtcNow.AddDays(10),
            seatIds: new[] { 1 });
        var seat = booking.Seats.First();

        _bookings.Setup(r => r.GetByIdForUpdateAsync(1, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(booking);
        _bookings.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);

        var sut = CreateSut();

        // Act
        var result = await sut.CancelAsync(1, UserId, isAdmin: false);

        // Assert
        Assert.Equal(ResultStatus.Ok, result.Status);
        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.NotNull(booking.CancelledAt);
        Assert.Equal(SeatStatus.Available, seat.Status);
        Assert.Null(seat.BookingId);
        Assert.Null(seat.BookedAt);

        _bookings.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelAsync_NotFound_ReturnsNotFound()
    {
        _bookings.Setup(r => r.GetByIdForUpdateAsync(99, It.IsAny<CancellationToken>()))
                 .ReturnsAsync((Booking?)null);

        var sut = CreateSut();

        var result = await sut.CancelAsync(99, UserId, isAdmin: false);

        Assert.Equal(ResultStatus.NotFound, result.Status);
        _bookings.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancelAsync_OtherUser_ReturnsForbidden()
    {
        var booking = BuildBooking(
            userId: UserId + 5,
            departure: DateTime.UtcNow.AddDays(10),
            seatIds: new[] { 1 });

        _bookings.Setup(r => r.GetByIdForUpdateAsync(1, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(booking);

        var sut = CreateSut();

        var result = await sut.CancelAsync(1, UserId, isAdmin: false);

        Assert.Equal(ResultStatus.Forbidden, result.Status);
        _bookings.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancelAsync_Admin_CanCancelOtherUsersBooking()
    {
        var booking = BuildBooking(
            userId: UserId + 5,
            departure: DateTime.UtcNow.AddDays(10),
            seatIds: new[] { 1 });

        _bookings.Setup(r => r.GetByIdForUpdateAsync(1, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(booking);
        _bookings.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);

        var sut = CreateSut();

        var result = await sut.CancelAsync(1, UserId, isAdmin: true);

        Assert.Equal(ResultStatus.Ok, result.Status);
    }

    [Fact]
    public async Task CancelAsync_AlreadyCancelled_ReturnsConflict()
    {
        var booking = BuildBooking(
            status: BookingStatus.Cancelled,
            departure: DateTime.UtcNow.AddDays(10),
            seatIds: new[] { 1 });

        _bookings.Setup(r => r.GetByIdForUpdateAsync(1, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(booking);

        var sut = CreateSut();

        var result = await sut.CancelAsync(1, UserId, isAdmin: false);

        Assert.Equal(ResultStatus.Conflict, result.Status);
        _bookings.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancelAsync_Within24Hours_ReturnsConflict()
    {
        var booking = BuildBooking(
            departure: DateTime.UtcNow.AddHours(5),
            seatIds: new[] { 1 });

        _bookings.Setup(r => r.GetByIdForUpdateAsync(1, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(booking);

        var sut = CreateSut();

        var result = await sut.CancelAsync(1, UserId, isAdmin: false);

        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Contains("24", result.ErrorMessage);
        _bookings.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------- UpdateAsync ----------

    [Fact]
    public async Task UpdateAsync_HappyPath_SameSeat_UpdatesDetails()
    {
        // Arrange
        var booking = BuildBooking(
            departure: DateTime.UtcNow.AddDays(10),
            seatIds: new[] { 1 });
        var seat = booking.Seats.First();

        _bookings.Setup(r => r.GetByIdForUpdateAsync(1, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(booking);
        _bookings.Setup(r => r.GetSeatsByFlightAsync(1, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<Seat> { seat });
        _bookings.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);

        var sut = CreateSut();

        // Act
        var result = await sut.UpdateAsync(1, UserId, isAdmin: false, BuildUpdateRequest(1));

        // Assert
        Assert.Equal(ResultStatus.Ok, result.Status);
        Assert.Equal("רותי קאופמן-כהן", booking.PassengerFullName);
        Assert.Equal("87654321", booking.PassportNumber);
        Assert.Equal(MealType.Vegetarian, booking.MealType);
        Assert.Equal(KashrutLevel.Mehadrin, booking.KashrutLevel);
        Assert.Equal(SeatStatus.Booked, seat.Status);
    }

    [Fact]
    public async Task UpdateAsync_SwitchSeat_ReleasesOldAndBooksNew()
    {
        // Arrange
        var booking = BuildBooking(
            departure: DateTime.UtcNow.AddDays(10),
            seatIds: new[] { 1 });
        var oldSeat = booking.Seats.First();

        var newSeat = BuildSeat(2, "1B", SeatStatus.Available, BuildFlight(DateTime.UtcNow.AddDays(10)));
        newSeat.FlightId = oldSeat.FlightId;

        _bookings.Setup(r => r.GetByIdForUpdateAsync(1, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(booking);
        _bookings.Setup(r => r.GetSeatsByFlightAsync(oldSeat.FlightId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<Seat> { oldSeat, newSeat });
        _bookings.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);

        var sut = CreateSut();

        // Act
        var result = await sut.UpdateAsync(1, UserId, isAdmin: false, BuildUpdateRequest(2));

        // Assert
        Assert.Equal(ResultStatus.Ok, result.Status);

        Assert.Equal(SeatStatus.Available, oldSeat.Status);
        Assert.Null(oldSeat.BookingId);

        Assert.Equal(SeatStatus.Booked, newSeat.Status);
        Assert.Equal(booking.Id, newSeat.BookingId);
        Assert.NotNull(newSeat.BookedAt);

        Assert.Single(booking.Seats);
        Assert.Equal(2, booking.Seats.First().Id);
    }

    [Fact]
    public async Task UpdateAsync_NewSeatAlreadyBooked_ReturnsConflict()
    {
        var booking = BuildBooking(
            departure: DateTime.UtcNow.AddDays(10),
            seatIds: new[] { 1 });
        var oldSeat = booking.Seats.First();

        var taken = BuildSeat(2, "1B", SeatStatus.Booked, BuildFlight(DateTime.UtcNow.AddDays(10)));
        taken.FlightId = oldSeat.FlightId;

        _bookings.Setup(r => r.GetByIdForUpdateAsync(1, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(booking);
        _bookings.Setup(r => r.GetSeatsByFlightAsync(oldSeat.FlightId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<Seat> { oldSeat, taken });

        var sut = CreateSut();

        var result = await sut.UpdateAsync(1, UserId, isAdmin: false, BuildUpdateRequest(2));

        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Equal(SeatStatus.Booked, oldSeat.Status);
        _bookings.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_SeatFromAnotherFlight_ReturnsBadRequest()
    {
        var booking = BuildBooking(
            departure: DateTime.UtcNow.AddDays(10),
            seatIds: new[] { 1 });
        var oldSeat = booking.Seats.First();

        _bookings.Setup(r => r.GetByIdForUpdateAsync(1, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(booking);
        _bookings.Setup(r => r.GetSeatsByFlightAsync(oldSeat.FlightId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<Seat> { oldSeat });

        var sut = CreateSut();

        var result = await sut.UpdateAsync(1, UserId, isAdmin: false, BuildUpdateRequest(999));

        Assert.Equal(ResultStatus.BadRequest, result.Status);
    }

    [Fact]
    public async Task UpdateAsync_CancelledBooking_ReturnsConflict()
    {
        var booking = BuildBooking(
            status: BookingStatus.Cancelled,
            departure: DateTime.UtcNow.AddDays(10),
            seatIds: new[] { 1 });

        _bookings.Setup(r => r.GetByIdForUpdateAsync(1, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(booking);

        var sut = CreateSut();

        var result = await sut.UpdateAsync(1, UserId, isAdmin: false, BuildUpdateRequest(1));

        Assert.Equal(ResultStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task UpdateAsync_Within24Hours_ReturnsConflict()
    {
        var booking = BuildBooking(
            departure: DateTime.UtcNow.AddHours(5),
            seatIds: new[] { 1 });

        _bookings.Setup(r => r.GetByIdForUpdateAsync(1, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(booking);

        var sut = CreateSut();

        var result = await sut.UpdateAsync(1, UserId, isAdmin: false, BuildUpdateRequest(1));

        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Contains("24", result.ErrorMessage);
    }

    [Fact]
    public async Task UpdateAsync_OtherUser_ReturnsForbidden()
    {
        var booking = BuildBooking(
            userId: UserId + 5,
            departure: DateTime.UtcNow.AddDays(10),
            seatIds: new[] { 1 });

        _bookings.Setup(r => r.GetByIdForUpdateAsync(1, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(booking);

        var sut = CreateSut();

        var result = await sut.UpdateAsync(1, UserId, isAdmin: false, BuildUpdateRequest(1));

        Assert.Equal(ResultStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task UpdateAsync_NotFound_ReturnsNotFound()
    {
        _bookings.Setup(r => r.GetByIdForUpdateAsync(99, It.IsAny<CancellationToken>()))
                 .ReturnsAsync((Booking?)null);

        var sut = CreateSut();

        var result = await sut.UpdateAsync(99, UserId, isAdmin: false, BuildUpdateRequest(1));

        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task UpdateAsync_EmptySeatList_ReturnsBadRequest()
    {
        var sut = CreateSut();

        var result = await sut.UpdateAsync(1, UserId, isAdmin: false, BuildUpdateRequest());

        Assert.Equal(ResultStatus.BadRequest, result.Status);
    }

    [Fact]
    public async Task UpdateAsync_DuplicateSeatIds_ReturnsBadRequest()
    {
        var sut = CreateSut();

        var result = await sut.UpdateAsync(1, UserId, isAdmin: false, BuildUpdateRequest(1, 1));

        Assert.Equal(ResultStatus.BadRequest, result.Status);
    }
}