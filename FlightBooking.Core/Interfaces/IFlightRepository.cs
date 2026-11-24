using FlightBooking.Core.Entities;

namespace FlightBooking.Core.Interfaces;

/// <summary>פרמטרי שליפת הקטלוג — עימוד וסינון אופציונלי.</summary>
public class FlightCatalogQuery
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public bool IncludePast { get; init; }
    public string? DestinationCode { get; init; }
    public string? OriginCode { get; init; }
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
}

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

    Task<(IReadOnlyList<Flight> Items, int TotalCount)> GetCatalogAsync(
        FlightCatalogQuery query,
        CancellationToken ct = default);

    Task<(IReadOnlyList<Flight> Items, int TotalCount)> GetCatalogAsync(
        int pageNumber,
        int pageSize,
        bool includePast,
        CancellationToken ct = default);

    Task<Seat?> GetSeatWithFlightAsync(int seatId, CancellationToken ct = default);
    Task<Airport?> GetAirportByCodeAsync(string code, CancellationToken ct = default);
    Task<bool> ExistsByNumberAsync(string flightNumber, CancellationToken ct = default);
    Task AddAsync(Flight flight, CancellationToken ct = default);
}