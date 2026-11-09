using FlightBooking.Core.Common;
using FlightBooking.Core.DTOs.Flights;
using FlightBooking.Core.DTOs.Seats;
using FlightBooking.Core.Entities;
using FlightBooking.Core.Enums;
using FlightBooking.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace FlightBooking.Service.Services;

public class FlightService : IFlightService
{
    private const int MaxPageSize = 50;

    private readonly IFlightRepository _flights;
    private readonly ILogger<FlightService> _logger;

    public FlightService(IFlightRepository flights, ILogger<FlightService> logger)
    {
        _flights = flights;
        _logger = logger;
    }

    public async Task<Result<PagedResult<FlightResponse>>> SearchAsync(
        FlightSearchRequest request, CancellationToken ct = default)
    {
        // ולידציה עסקית — לא תלוית נתונים אבל לא שייכת ל-Controller
        if (request.ReturnDate.HasValue && request.ReturnDate.Value < request.DepartureDate)
        {
            return Result<PagedResult<FlightResponse>>.BadRequest(
                "תאריך החזור לא יכול להיות לפני תאריך היציאה.");
        }

        var origin = request.OriginCode.Trim().ToUpperInvariant();
        var destination = request.DestinationCode.Trim().ToUpperInvariant();

        if (origin == destination)
        {
            return Result<PagedResult<FlightResponse>>.BadRequest(
                "נמל המוצא ונמל היעד חייבים להיות שונים.");
        }

        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);

        var (items, totalCount) = await _flights.SearchAsync(
            origin,
            destination,
    DateTime.SpecifyKind(request.DepartureDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc),
            pageNumber,
            pageSize,
            ct);

        var mapped = items.Select(MapFlight).ToList();

        return Result<PagedResult<FlightResponse>>.Ok(new PagedResult<FlightResponse>(
            mapped, pageNumber, pageSize, totalCount));
    }

    public async Task<Result<FlightResponse>> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var flight = await _flights.GetByIdAsync(id, ct);

        if (flight is null)
        {
            return Result<FlightResponse>.NotFound("הטיסה לא נמצאה.");
        }

        return Result<FlightResponse>.Ok(MapFlight(flight));
    }

    public async Task<Result<IReadOnlyList<SeatResponse>>> GetSeatsAsync(
        int flightId, CancellationToken ct = default)
    {
        var flight = await _flights.GetByIdAsync(flightId, ct);

        if (flight is null)
        {
            return Result<IReadOnlyList<SeatResponse>>.NotFound("הטיסה לא נמצאה.");
        }

        var seats = await _flights.GetSeatsAsync(flightId, ct);
        var flightDeparted = flight.DepartureTime <= DateTime.UtcNow;

        var response = seats
            .Select(s => new SeatResponse
            {
                Id = s.Id,
                SeatNumber = s.SeatNumber,
                Status = s.Status.ToString(),
                // מושב פנוי אבל טיסה שהמריאה — אינו זמין להזמנה
                IsAvailable = s.Status == SeatStatus.Available && !flightDeparted
            })
            .ToList();

        return Result<IReadOnlyList<SeatResponse>>.Ok(response);
    }

    private static FlightResponse MapFlight(Flight flight) => new()
    {
        Id = flight.Id,
        FlightNumber = flight.FlightNumber,
        OriginCode = flight.OriginAirport.Code,
        OriginCity = flight.OriginAirport.City,
        DestinationCode = flight.DestinationAirport.Code,
        DestinationCity = flight.DestinationAirport.City,
        DepartureTime = flight.DepartureTime,
        ArrivalTime = flight.ArrivalTime,
        CheckInOpensAt = flight.CheckInOpensAt,
        BoardingTime = flight.BoardingTime,
        Gate = flight.Gate,
        Price = flight.Price,
        AircraftType = flight.AircraftType,
        AvailableSeatsCount = flight.Seats.Count(s => s.Status == SeatStatus.Available)
    };
}