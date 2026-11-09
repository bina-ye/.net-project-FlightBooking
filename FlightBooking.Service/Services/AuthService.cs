using FlightBooking.Core.Common;
using FlightBooking.Core.DTOs.Auth;
using FlightBooking.Core.Entities;
using FlightBooking.Core.Enums;
using FlightBooking.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace FlightBooking.Service.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly ITokenService _tokens;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository users,
        ITokenService tokens,
        ILogger<AuthService> logger)
    {
        _users = users;
        _tokens = tokens;
        _logger = logger;
    }

    public async Task<Result<AuthResponse>> RegisterAsync(
        RegisterRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        // ולידציה עסקית — תלוית מצב הנתונים
        if (await _users.EmailExistsAsync(email, ct))
        {
            return Result<AuthResponse>.Conflict("כתובת האימייל כבר רשומה במערכת.");
        }

        var user = new AppUser
        {
            Email = email,
            FullName = request.FullName.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = UserRole.Customer,   // קבוע בשרת — לא מהבקשה
            CreatedAt = DateTime.UtcNow
        };

        await _users.AddAsync(user, ct);
        await _users.SaveChangesAsync(ct);

        _logger.LogInformation("נרשם משתמש חדש עם מזהה {UserId}.", user.Id);

        var (token, expiresAt) = _tokens.CreateToken(user);

        return Result<AuthResponse>.Created(new AuthResponse
        {
            Token = token,
            ExpiresAt = expiresAt,
            User = MapUser(user)
        });
    }

    public async Task<Result<AuthResponse>> LoginAsync(
        LoginRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var user = await _users.GetByEmailAsync(email, ct);

        // אותה הודעה גם אם המייל לא קיים וגם אם הסיסמה שגויה
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            _logger.LogWarning("ניסיון התחברות שנכשל.");
            return Result<AuthResponse>.Unauthorized("אימייל או סיסמה שגויים.");
        }

        var (token, expiresAt) = _tokens.CreateToken(user);

        _logger.LogInformation("התחברות מוצלחת של משתמש {UserId}.", user.Id);

        return Result<AuthResponse>.Ok(new AuthResponse
        {
            Token = token,
            ExpiresAt = expiresAt,
            User = MapUser(user)
        });
    }

    private static UserResponse MapUser(AppUser user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        FullName = user.FullName,
        Role = user.Role.ToString()
    };
}