using FlightBooking.Core.Entities;

namespace FlightBooking.Core.Interfaces;

public interface IBookingRepository
{
    Task<Booking?> GetByIdAsync(int id, CancellationToken ct = default);

    Task<Booking?> GetByIdWithDetailsAsync(int id, CancellationToken ct = default);

    Task<(IReadOnlyList<Booking> Items, int TotalCount)> GetByUserAsync(
        int userId,
        int pageNumber,
        int pageSize,
        CancellationToken ct = default);

    Task<bool> ReferenceExistsAsync(string bookingReference, CancellationToken ct = default);

    Task AddAsync(Booking booking, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}