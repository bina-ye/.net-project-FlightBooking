using FlightBooking.Core.Entities;

namespace FlightBooking.Core.Interfaces;

public interface IUserRepository
{
    Task<AppUser?> GetByEmailAsync(string email, CancellationToken ct = default);

    Task<AppUser?> GetByIdAsync(int id, CancellationToken ct = default);

    Task<bool> EmailExistsAsync(string email, CancellationToken ct = default);

    Task AddAsync(AppUser user, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}