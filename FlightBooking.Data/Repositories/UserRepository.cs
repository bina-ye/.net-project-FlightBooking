using FlightBooking.Core.Entities;
using FlightBooking.Core.Interfaces;
using FlightBooking.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace FlightBooking.Data.Repositories;

public class UserRepository : IUserRepository
{
    private readonly FlightBookingDbContext _context;

    public UserRepository(FlightBookingDbContext context)
    {
        _context = context;
    }

    public async Task<AppUser?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        return await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email, ct);
    }

    public async Task<AppUser?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, ct);
    }

    public async Task<bool> EmailExistsAsync(string email, CancellationToken ct = default)
    {
        return await _context.Users
            .AsNoTracking()
            .AnyAsync(u => u.Email == email, ct);
    }

    public async Task AddAsync(AppUser user, CancellationToken ct = default)
    {
        await _context.Users.AddAsync(user, ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}