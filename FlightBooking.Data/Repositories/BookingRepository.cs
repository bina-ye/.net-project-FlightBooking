using FlightBooking.Core.Entities;
using FlightBooking.Core.Interfaces;
using FlightBooking.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace FlightBooking.Data.Repositories;

public class BookingRepository : IBookingRepository
{
    private readonly FlightBookingDbContext _context;

    public BookingRepository(FlightBookingDbContext context)
    {
        _context = context;
    }

    public async Task<Booking?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await _context.Bookings
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == id, ct);
    }

    public async Task<Booking?> GetByIdWithDetailsAsync(int id, CancellationToken ct = default)
    {
        return await _context.Bookings
            .AsNoTracking()
            .Include(b => b.Seats)
            .Include(b => b.BookingFlights)
                .ThenInclude(bf => bf.Flight)
                    .ThenInclude(f => f.OriginAirport)
            .Include(b => b.BookingFlights)
                .ThenInclude(bf => bf.Flight)
                    .ThenInclude(f => f.DestinationAirport)
            .FirstOrDefaultAsync(b => b.Id == id, ct);
    }

    public async Task<(IReadOnlyList<Booking> Items, int TotalCount)> GetByUserAsync(
        int userId,
        int pageNumber,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _context.Bookings
            .AsNoTracking()
            .Where(b => b.UserId == userId);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(b => b.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Include(b => b.Seats)
            .Include(b => b.BookingFlights)
                .ThenInclude(bf => bf.Flight)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<bool> ReferenceExistsAsync(string bookingReference, CancellationToken ct = default)
    {
        return await _context.Bookings
            .AsNoTracking()
            .AnyAsync(b => b.BookingReference == bookingReference, ct);
    }

    public async Task AddAsync(Booking booking, CancellationToken ct = default)
    {
        await _context.Bookings.AddAsync(booking, ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}