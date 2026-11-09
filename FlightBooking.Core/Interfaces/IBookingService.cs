using FlightBooking.Core.Common;
using FlightBooking.Core.DTOs.Bookings;

namespace FlightBooking.Core.Interfaces;

public interface IBookingService
{
    Task<Result<BookingResponse>> CreateAsync(
        int userId,
        BookingCreateRequest request,
        CancellationToken ct = default);

    Task<Result<BookingResponse>> GetByIdAsync(
        int bookingId,
        int userId,
        bool isAdmin,
        CancellationToken ct = default);

    Task<Result<PagedResult<BookingSummaryResponse>>> GetMyBookingsAsync(
        int userId,
        int pageNumber,
        int pageSize,
        CancellationToken ct = default);
}