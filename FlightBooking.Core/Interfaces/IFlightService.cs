using FlightBooking.Core.Common;
using FlightBooking.Core.DTOs.Flights;
using FlightBooking.Core.DTOs.Seats;

namespace FlightBooking.Core.Interfaces;

public interface IFlightService
{
    Task<Result<PagedResult<FlightResponse>>> SearchAsync(
        FlightSearchRequest request,
        CancellationToken ct = default);

    Task<Result<FlightResponse>> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Result<FlightResponse>> CreateAsync(FlightCreateRequest request, CancellationToken ct = default);
    Task<Result<IReadOnlyList<SeatResponse>>> GetSeatsAsync(
        int flightId,
        CancellationToken ct = default);
}