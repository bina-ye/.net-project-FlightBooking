using FlightBooking.Core.Common;
using FlightBooking.Core.DTOs.Bookings;
using FlightBooking.Core.DTOs.Seats;
using FlightBooking.Core.Entities;
using FlightBooking.Core.Enums;
using FlightBooking.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FlightBooking.Service.Services;

public class BookingService : IBookingService
{
    private const int MaxPageSize = 50;

    private readonly IBookingRepository _bookings;
    private readonly IFlightRepository _flights;
    private readonly IPaymentService _payments;
    private readonly ILogger<BookingService> _logger;

    public BookingService(
        IBookingRepository bookings,
        IFlightRepository flights,
        IPaymentService payments,
        ILogger<BookingService> logger)
    {
        _bookings = bookings;
        _flights = flights;
        _payments = payments;
        _logger = logger;
    }

    public async Task<Result<BookingResponse>> CreateAsync(
        int userId, BookingCreateRequest request, CancellationToken ct = default)
    {
        if (request.SeatIds.Count == 0)
            return Result<BookingResponse>.BadRequest("יש לבחור לפחות מושב אחד.");

        if (request.SeatIds.Distinct().Count() != request.SeatIds.Count)
            return Result<BookingResponse>.BadRequest("נבחר אותו מושב יותר מפעם אחת.");

        // 1. טעינת המושבים — כאן מתחילה התחרות
        var seats = new List<Seat>();
        foreach (var seatId in request.SeatIds)
        {
            var seat = await _flights.GetSeatWithFlightAsync(seatId, ct);

            if (seat is null)
                return Result<BookingResponse>.NotFound($"המושב {seatId} לא נמצא.");

            // ולידציה עסקית: הטיסה כבר המריאה
            if (seat.Flight.DepartureTime <= DateTime.UtcNow)
                return Result<BookingResponse>.BadRequest(
                    $"לא ניתן להזמין מושב בטיסה {seat.Flight.FlightNumber} — הטיסה כבר המריאה.");

            // ולידציה עסקית: המושב כבר תפוס
            if (seat.Status != SeatStatus.Available)
                return Result<BookingResponse>.Conflict(
                    $"המושב {seat.SeatNumber} אינו פנוי.");

            seats.Add(seat);
        }

        // 2. כל המושבים חייבים להיות באותה טיסה
        if (seats.Select(s => s.FlightId).Distinct().Count() > 1)
            return Result<BookingResponse>.BadRequest("יש לבחור מושבים באותה טיסה.");

        var flight = seats[0].Flight;
        var totalAmount = flight.Price * seats.Count;

        // 3. תשלום — לפני תפיסת המושבים
        var payment = await _payments.ChargeAsync(
            totalAmount,
            request.Payment.CardNumber,
            request.Payment.Expiry,
            request.Payment.Cvv,
            ct);

        if (!payment.IsSuccess)
            return Result<BookingResponse>.BadRequest(
                payment.ErrorMessage ?? "התשלום נכשל.");

        // 4. יצירת ההזמנה
        var booking = new Booking
        {
            UserId = userId,
            BookingReference = await GenerateReferenceAsync(ct),
            Status = BookingStatus.Confirmed,
            CreatedAt = DateTime.UtcNow,
            TotalAmount = totalAmount,
            PassengerFullName = request.PassengerFullName.Trim(),
            PassportNumber = request.PassportNumber.Trim(),
            MealType = (MealType)request.MealType,
            KashrutLevel = (KashrutLevel)request.KashrutLevel,
            SpecialRequests = request.SpecialRequests
        };

        booking.BookingFlights.Add(new BookingFlight
        {
            FlightId = flight.Id,
            SegmentOrder = 1
        });

        foreach (var seat in seats)
        {
            seat.Status = SeatStatus.Booked;
            seat.BookedAt = DateTime.UtcNow;
            booking.Seats.Add(seat);
        }

        await _bookings.AddAsync(booking, ct);

        // 5. השמירה — כאן מתפוצצת התחרות
        try
        {
            await _bookings.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex,
                "התנגשות על מושבים בטיסה {FlightId}. מישהו אחר תפס אותם קודם. תשלום {TransactionId} דורש החזר.",
                flight.Id, payment.TransactionId);

            // במערכת אמיתית: await _payments.RefundAsync(payment.TransactionId, ct);
            // במימוש המדומה אין חיוב אמיתי, ולכן נרשם לוג בלבד.

            return Result<BookingResponse>.Conflict(
                "המושב נתפס על ידי משתמש אחר. נסי שוב.");
        }
        _logger.LogInformation(
            "הזמנה {BookingReference} נוצרה עבור משתמש {UserId} בסכום {Amount}.",
            booking.BookingReference, userId, booking.TotalAmount);

        return Result<BookingResponse>.Created(MapBooking(booking, flight));
    }

    public async Task<Result<BookingResponse>> GetByIdAsync(
        int bookingId, int userId, bool isAdmin, CancellationToken ct = default)
    {
        var booking = await _bookings.GetByIdWithDetailsAsync(bookingId, ct);

        if (booking is null)
            return Result<BookingResponse>.NotFound("ההזמנה לא נמצאה.");

        // הרשאה נבדקת בשרת — לא בצד הלקוח
        if (!isAdmin && booking.UserId != userId)
            return Result<BookingResponse>.Forbidden("אין הרשאה לצפות בהזמנה זו.");

        return Result<BookingResponse>.Ok(MapDetails(booking));
    }

    public async Task<Result<PagedResult<BookingSummaryResponse>>> GetMyBookingsAsync(
        int userId, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var (items, totalCount) = await _bookings.GetByUserAsync(
            userId, pageNumber, pageSize, ct);

        var mapped = items.Select(b => new BookingSummaryResponse
        {
            Id = b.Id,
            BookingReference = b.BookingReference,
            Status = b.Status.ToString(),
            CreatedAt = b.CreatedAt,
            TotalAmount = b.TotalAmount,
            FlightsCount = b.BookingFlights.Count,
            SeatsCount = b.Seats.Count,
            FirstDepartureTime = b.BookingFlights
                .Select(bf => (DateTime?)bf.Flight.DepartureTime)
                .Min()
        }).ToList();

        return Result<PagedResult<BookingSummaryResponse>>.Ok(
            new PagedResult<BookingSummaryResponse>(mapped, pageNumber, pageSize, totalCount));
    }

    private async Task<string> GenerateReferenceAsync(CancellationToken ct)
    {
        // 6 תווים, אותיות וספרות — מספיק לשימוש, עם בדיקת ייחודיות
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var reference = new string(
                Enumerable.Range(0, 6)
                    .Select(_ => chars[Random.Shared.Next(chars.Length)])
                    .ToArray());

            if (!await _bookings.ReferenceExistsAsync(reference, ct))
                return reference;
        }

        throw new InvalidOperationException("לא ניתן ליצור קוד הזמנה ייחודי.");
    }

    private static BookingResponse MapBooking(Booking booking, Flight flight) => new()
    {
        Id = booking.Id,
        BookingReference = booking.BookingReference,
        Status = booking.Status.ToString(),
        CreatedAt = booking.CreatedAt,
        TotalAmount = booking.TotalAmount,
        PassengerFullName = booking.PassengerFullName,
        MealType = booking.MealType.ToString(),
        KashrutLevel = booking.KashrutLevel.ToString(),
        SpecialRequests = booking.SpecialRequests,
        Seats = booking.Seats
            .Select(s => new SeatResponse
            {
                Id = s.Id,
                SeatNumber = s.SeatNumber,
                Status = s.Status.ToString(),
                IsAvailable = false
            }).ToList(),
        Flights = new List<FlightSegmentResponse>
        {
            new()
            {
                FlightId = flight.Id,
                FlightNumber = flight.FlightNumber,
                OriginCode = flight.OriginAirport.Code,
                DestinationCode = flight.DestinationAirport.Code,
                DepartureTime = flight.DepartureTime,
                ArrivalTime = flight.ArrivalTime,
                CheckInOpensAt = flight.CheckInOpensAt,
                BoardingTime = flight.BoardingTime,
                Gate = flight.Gate,
                SegmentOrder = 1
            }
        }
    };

    private static BookingResponse MapDetails(Booking booking) => new()
    {
        Id = booking.Id,
        BookingReference = booking.BookingReference,
        Status = booking.Status.ToString(),
        CreatedAt = booking.CreatedAt,
        TotalAmount = booking.TotalAmount,
        PassengerFullName = booking.PassengerFullName,
        MealType = booking.MealType.ToString(),
        KashrutLevel = booking.KashrutLevel.ToString(),
        SpecialRequests = booking.SpecialRequests,
        Seats = booking.Seats
            .Select(s => new SeatResponse
            {
                Id = s.Id,
                SeatNumber = s.SeatNumber,
                Status = s.Status.ToString(),
                IsAvailable = false
            }).ToList(),
        Flights = booking.BookingFlights
            .OrderBy(bf => bf.SegmentOrder)
            .Select(bf => new FlightSegmentResponse
            {
                FlightId = bf.Flight.Id,
                FlightNumber = bf.Flight.FlightNumber,
                OriginCode = bf.Flight.OriginAirport.Code,
                DestinationCode = bf.Flight.DestinationAirport.Code,
                DepartureTime = bf.Flight.DepartureTime,
                ArrivalTime = bf.Flight.ArrivalTime,
                CheckInOpensAt = bf.Flight.CheckInOpensAt,
                BoardingTime = bf.Flight.BoardingTime,
                Gate = bf.Flight.Gate,
                SegmentOrder = bf.SegmentOrder
            }).ToList()
    };
}