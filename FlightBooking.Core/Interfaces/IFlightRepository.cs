using FlightBooking.Core.Entities;

namespace FlightBooking.Core.Interfaces;

public interface IFlightRepository
{
    Task<(IReadOnlyList<Flight> Items, int TotalCount)> SearchAsync(
        string originCode,
        string destinationCode,
        DateTime departureDate,
        int pageNumber,
        int pageSize,
        CancellationToken ct = default);

    Task<Flight?> GetByIdAsync(int id, CancellationToken ct = default);

    Task<List<Seat>> GetSeatsAsync(int flightId, CancellationToken ct = default);

    Task<Seat?> GetSeatWithFlightAsync(int seatId, CancellationToken ct = default);
}