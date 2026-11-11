using FlightBooking.Core.Entities;
using FlightBooking.Core.Interfaces;
using FlightBooking.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace FlightBooking.Data.Repositories;

public class FlightRepository : IFlightRepository
{
    private readonly FlightBookingDbContext _context;

    public FlightRepository(FlightBookingDbContext context)
    {
        _context = context;
    }

    public async Task<(IReadOnlyList<Flight> Items, int TotalCount)> SearchAsync(
        string originCode,
        string destinationCode,
        DateTime departureDate,
        int pageNumber,
        int pageSize,
        CancellationToken ct = default)
    {
        var dayStart = departureDate.Date;
        var dayEnd = dayStart.AddDays(1);

        var query = _context.Flights
            .AsNoTracking()
            .Where(f => f.OriginAirport.Code == originCode
                     && f.DestinationAirport.Code == destinationCode
                     && f.DepartureTime >= dayStart
                     && f.DepartureTime < dayEnd);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(f => f.DepartureTime)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Include(f => f.OriginAirport)
            .Include(f => f.DestinationAirport)
            .Include(f => f.Seats)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<Flight?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await _context.Flights
            .AsNoTracking()
            .Include(f => f.OriginAirport)
            .Include(f => f.DestinationAirport)
            .Include(f => f.Seats)
            .FirstOrDefaultAsync(f => f.Id == id, ct);
    }

    public async Task<List<Seat>> GetSeatsAsync(int flightId, CancellationToken ct = default)
    {
        return await _context.Seats
            .AsNoTracking()
            .Where(s => s.FlightId == flightId)
            .OrderBy(s => s.SeatNumber)
            .ToListAsync(ct);
    }

    public async Task<Seat?> GetSeatWithFlightAsync(int seatId, CancellationToken ct = default)
    {
        // כאן בכוונה בלי AsNoTracking — המושב נתפס ונשמר, צריך מעקב שינויים
        return await _context.Seats
            .Include(s => s.Flight)
                .ThenInclude(f => f.OriginAirport)
            .Include(s => s.Flight)
                .ThenInclude(f => f.DestinationAirport)
            .FirstOrDefaultAsync(s => s.Id == seatId, ct);
    }
    public Task<Airport?> GetAirportByCodeAsync(string code, CancellationToken ct = default)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return _context.Airports.FirstOrDefaultAsync(a => a.Code == normalized, ct);
    }

    public Task<bool> ExistsByNumberAsync(string flightNumber, CancellationToken ct = default)
    {
        var normalized = flightNumber.Trim().ToUpperInvariant();
        return _context.Flights.AnyAsync(f => f.FlightNumber == normalized, ct);
    }

    public async Task AddAsync(Flight flight, CancellationToken ct = default)
    {
        _context.Flights.Add(flight);
        await _context.SaveChangesAsync(ct);
    }

}
