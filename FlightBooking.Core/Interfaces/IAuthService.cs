using FlightBooking.Core.Common;
using FlightBooking.Core.DTOs.Auth;

namespace FlightBooking.Core.Interfaces;

public interface IAuthService
{
    Task<Result<AuthResponse>> RegisterAsync(
        RegisterRequest request,
        CancellationToken ct = default);

    Task<Result<AuthResponse>> LoginAsync(
        LoginRequest request,
        CancellationToken ct = default);
}