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

        // ===== 1. טעינת המושבים — כאן מתחילה התחרות =====
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

        // ===== 2. יצירת ההזמנה ותפיסת המושבים — לפני התשלום =====
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

        // ===== 3. השמירה — כאן מכריע ה-xmin מי המנצח =====
        // מי שמגיע לכאן שני — לא ממשיך הלאה, ולכן לא מחויב.
        try
        {
            await _bookings.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex,
                "התנגשות על מושבים בטיסה {FlightId}. משתמש {UserId} לא קיבל את המושבים.",
                flight.Id, userId);

            return Result<BookingResponse>.Conflict(
                "המושב נתפס על ידי משתמש אחר. נסי שוב.");
        }

        // ===== 4. תשלום — רק למי שתפס את המושבים בפועל =====
        var payment = await _payments.ChargeAsync(
            totalAmount,
            request.Payment.CardNumber,
            request.Payment.Expiry,
            request.Payment.Cvv,
            ct);

        if (!payment.IsSuccess)
        {
            // התשלום נכשל אחרי שהמושב נתפס — משחררים אותו חזרה לבריכה
            foreach (var seat in seats)
            {
                seat.Status = SeatStatus.Available;
                seat.BookedAt = null;
                seat.BookingId = null;
            }

            booking.Status = BookingStatus.Cancelled;
            booking.CancelledAt = DateTime.UtcNow;

            try
            {
                await _bookings.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // לא קריטי: המושב יישאר תפוס, אבל הכסף לא נלקח
                _logger.LogError(ex,
                    "כשל בשחרור מושבים לאחר תשלום כושל. הזמנה {BookingReference}.",
                    booking.BookingReference);
            }

            _logger.LogWarning(
                "תשלום נכשל עבור משתמש {UserId}. הזמנה {BookingReference} בוטלה והמושבים שוחררו.",
                userId, booking.BookingReference);

            return Result<BookingResponse>.BadRequest(
                payment.ErrorMessage ?? "התשלום נכשל.");
        }

        _logger.LogInformation(
            "הזמנה {BookingReference} נוצרה עבור משתמש {UserId} בסכום {Amount}. עסקה {TransactionId}.",
            booking.BookingReference, userId, booking.TotalAmount, payment.TransactionId);

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

    public async Task<Result<BookingResponse>> CancelAsync(
        int bookingId, int userId, bool isAdmin, CancellationToken ct = default)
    {
        var booking = await _bookings.GetByIdForUpdateAsync(bookingId, ct);

        if (booking is null)
            return Result<BookingResponse>.NotFound("ההזמנה לא נמצאה.");

        if (!isAdmin && booking.UserId != userId)
            return Result<BookingResponse>.Forbidden("אין הרשאה לבטל הזמנה זו.");

        if (booking.Status == BookingStatus.Cancelled)
            return Result<BookingResponse>.Conflict("ההזמנה כבר בוטלה.");

        // חלון הביטול: עד 24 שעות לפני ההמראה של הטיסה הראשונה
        var firstDeparture = booking.BookingFlights
            .Select(bf => (DateTime?)bf.Flight.DepartureTime)
            .Min();

        if (firstDeparture is null)
            return Result<BookingResponse>.BadRequest("לא נמצאה טיסה עבור ההזמנה.");

        var hoursLeft = (firstDeparture.Value - DateTime.UtcNow).TotalHours;

        if (hoursLeft < 24)
            return Result<BookingResponse>.Conflict(
                "לא ניתן לבטל הזמנה בפחות מ-24 שעות לפני ההמראה.");

        // מחיקה לוגית + שחרור המושבים
        booking.Status = BookingStatus.Cancelled;
        booking.CancelledAt = DateTime.UtcNow;

        foreach (var seat in booking.Seats)
        {
            seat.Status = SeatStatus.Available;
            seat.BookingId = null;
            seat.BookedAt = null;
        }

        try
        {
            await _bookings.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex,
                "התנגשות בשחרור מושבים בהזמנה {BookingReference}.",
                booking.BookingReference);

            return Result<BookingResponse>.Conflict(
                "ההזמנה השתנתה בינתיים. נסי שוב.");
        }

        _logger.LogInformation(
            "הזמנה {BookingReference} בוטלה על ידי משתמש {UserId}. שוחררו {Count} מושבים.",
            booking.BookingReference, userId, booking.Seats.Count);

        return Result<BookingResponse>.Ok(new BookingResponse
        {
            Id = booking.Id,
            BookingReference = booking.BookingReference,
            Status = booking.Status.ToString(),
            CreatedAt = booking.CreatedAt,
            TotalAmount = booking.TotalAmount
        });
    }

    public async Task<Result<BookingResponse>> UpdateAsync(
        int bookingId,
        int userId,
        bool isAdmin,
        BookingUpdateRequest request,
        CancellationToken ct = default)
    {
        if (request.SeatIds.Count == 0)
            return Result<BookingResponse>.BadRequest("יש לבחור לפחות מושב אחד.");

        if (request.SeatIds.Distinct().Count() != request.SeatIds.Count)
            return Result<BookingResponse>.BadRequest("נבחר אותו מושב יותר מפעם אחת.");


        // 1. טעינה עם tracking — אנחנו עומדים לשנות
        var booking = await _bookings.GetByIdForUpdateAsync(bookingId, ct);

        if (booking is null)
            return Result<BookingResponse>.NotFound("ההזמנה לא נמצאה.");

        if (!isAdmin && booking.UserId != userId)
            return Result<BookingResponse>.Forbidden("אין הרשאה לעדכן הזמנה זו.");

        if (booking.Status == BookingStatus.Cancelled)
            return Result<BookingResponse>.Conflict("לא ניתן לעדכן הזמנה שבוטלה.");
        // מספר המושבים הסופי חייב להיות זהה למספר המקורי
        if (request.SeatIds.Count != booking.Seats.Count)
            return Result<BookingResponse>.BadRequest(
                $"יש לבחור בדיוק {booking.Seats.Count} מושבים — כמספר המושבים בהזמנה.");

        // 2. חלון העדכון — כמו בביטול
        var firstDeparture = booking.BookingFlights
            .Select(bf => (DateTime?)bf.Flight.DepartureTime)
            .Min();

        if (firstDeparture is null)
            return Result<BookingResponse>.BadRequest("לא נמצאה טיסה עבור ההזמנה.");

        if ((firstDeparture.Value - DateTime.UtcNow).TotalHours < 24)
            return Result<BookingResponse>.Conflict(
                "לא ניתן לעדכן הזמנה בפחות מ-24 שעות לפני ההמראה.");

        var flightId = booking.BookingFlights
            .OrderBy(bf => bf.SegmentOrder)
            .First().FlightId;

        // 3. טעינת כל מושבי הטיסה — לבדיקת זמינות
        var flightSeats = await _bookings.GetSeatsByFlightAsync(flightId, ct);
        var seatsById = flightSeats.ToDictionary(s => s.Id);

        var oldSeats = booking.Seats.ToList();
        var oldSeatIds = oldSeats.Select(s => s.Id).ToList();
        var newSeats = new List<Seat>();

        foreach (var seatId in request.SeatIds)
        {
            if (!seatsById.TryGetValue(seatId, out var seat))
                return Result<BookingResponse>.BadRequest(
                    $"המושב {seatId} אינו שייך לטיסה זו.");

            // מושב שכבר שייך להזמנה הזו — נשאר, לא נחשב תפוס
            if (oldSeatIds.Contains(seatId))
            {
                newSeats.Add(seat);
                continue;
            }

            if (seat.Status != SeatStatus.Available)
                return Result<BookingResponse>.Conflict(
                    $"המושב {seat.SeatNumber} אינו פנוי.");

            newSeats.Add(seat);
        }

        // 4. שחרור המושבים שהוסרו
        // מעדכנים את ה-FK (Seat.BookingId) בלבד — EF יוציא UPDATE.
        // אין צורך לגעת בקולקשן booking.Seats.
        foreach (var seat in oldSeats.Where(s => !request.SeatIds.Contains(s.Id)))
        {
            seat.Status = SeatStatus.Available;
            seat.BookingId = null;
            seat.BookedAt = null;
        }

        // 5. תפיסת המושבים החדשים
        var now = DateTime.UtcNow;
        foreach (var seat in newSeats.Where(s => !oldSeatIds.Contains(s.Id)))
        {
            seat.Status = SeatStatus.Booked;
            seat.BookingId = booking.Id;
            seat.BookedAt = now;
        }

        // 6. עדכון פרטים
        booking.PassengerFullName = request.PassengerFullName.Trim();
        booking.PassportNumber = request.PassportNumber.Trim();
        booking.MealType = (MealType)request.MealType;
        booking.KashrutLevel = (KashrutLevel)request.KashrutLevel;
        booking.SpecialRequests = request.SpecialRequests;

        // 7. שמירה — כאן מתפוצצת תחרות על המושב
        try
        {
            await _bookings.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex,
                "התנגשות בעדכון הזמנה {BookingReference} — מושב נתפס בינתיים.",
                booking.BookingReference);

            return Result<BookingResponse>.Conflict(
                "אחד המושבים נתפס על ידי משתמש אחר. נסי שוב.");
        }

        _logger.LogInformation(
            "הזמנה {BookingReference} עודכנה על ידי משתמש {UserId}. מושבים: {SeatCount}.",
            booking.BookingReference, userId, request.SeatIds.Count);

        // 8. טעינה מחדש — כדי שהתשובה תשקף את מצב המסד ולא את
        // קולקשן booking.Seats בזיכרון, שלא עודכן ידנית.
        var updated = await _bookings.GetByIdWithDetailsAsync(booking.Id, ct);

        if (updated is null)
            return Result<BookingResponse>.NotFound("ההזמנה לא נמצאה.");

        return Result<BookingResponse>.Ok(MapDetails(updated));
    }
}