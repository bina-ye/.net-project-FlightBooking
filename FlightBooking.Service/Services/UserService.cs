using FlightBooking.Core.Common;
using FlightBooking.Core.DTOs.Auth;
using FlightBooking.Core.Interfaces;

namespace FlightBooking.Service.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _users;
    private readonly IAuthService _auth;

    public UserService(IUserRepository users, IAuthService auth)
    {
        _users = users;
        _auth = auth;
    }

    public async Task<Result<UserResponse>> GetByIdAsync(int userId, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct);

        if (user is null)
            return Result<UserResponse>.NotFound("המשתמש לא נמצא.");

        return Result<UserResponse>.Ok(new UserResponse
        {
            Id = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            Role = user.Role.ToString()
        });
    }
    public async Task<Result<UserResponse>> GetByIdForCallerAsync(
        int targetId, int callerId, bool isAdmin, CancellationToken ct = default)
    {
        // הרשאה נבדקת בשרת — לא בצד הלקוח
        if (!isAdmin && targetId != callerId)
            return Result<UserResponse>.Forbidden("אין הרשאה לצפות בפרטי משתמש אחר.");

        return await GetByIdAsync(targetId, ct);
    }

    public Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
        => _auth.RegisterAsync(request, ct);
}