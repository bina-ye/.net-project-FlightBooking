using FlightBooking.Core.Common;
using FlightBooking.Core.DTOs.Auth;

namespace FlightBooking.Core.Interfaces;

public interface IUserService
{
    Task<Result<UserResponse>> GetByIdAsync(int userId, CancellationToken ct = default);

    Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<Result<UserResponse>> GetByIdForCallerAsync(
        int targetId, int callerId, bool isAdmin, CancellationToken ct = default);
}