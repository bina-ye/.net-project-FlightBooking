using FlightBooking.Core.Entities;

namespace FlightBooking.Core.Interfaces;

public interface ITokenService
{
    (string Token, DateTime ExpiresAt) CreateToken(AppUser user);
}