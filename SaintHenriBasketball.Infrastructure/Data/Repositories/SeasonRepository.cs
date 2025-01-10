using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using SaintHenriBasketball.Infrastructure.Data.Context;

namespace SaintHenriBasketball.Infrastructure.Data.Repositories;

public class SeasonRepository : ISeasonRepository
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SeasonRepository> _logger;

    public SeasonRepository(
        ApplicationDbContext context,
        ILogger<SeasonRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Season?> GetByIdAsync(Guid id)
    {
        return await _context.Seasons
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<Season?> GetByIdWithRegistrationsAsync(Guid id)
    {
        return await _context.Seasons
            .Include(s => s.Registrations)
                .ThenInclude(r => r.User)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<List<Season>> GetAllAsync()
    {
        return await _context.Seasons
            .OrderByDescending(s => s.StartDate)
            .ToListAsync();
    }

    public async Task<List<Season>> GetAllWithRegistrationsAsync()
    {
        return await _context.Seasons
            .Include(s => s.Registrations)
                .ThenInclude(r => r.User)
            .OrderByDescending(s => s.StartDate)
            .ToListAsync();
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _context.Seasons
            .AnyAsync(s => s.Id == id);
    }

    public async Task<Season> AddAsync(Season season)
    {
        await _context.Seasons.AddAsync(season);
        await _context.SaveChangesAsync();
        return season;
    }

    public async Task UpdateAsync(Season season)
    {
        _context.Entry(season).State = EntityState.Modified;
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Season season)
    {
        _context.Seasons.Remove(season);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> HasUserRegisteredAsync(Guid seasonId, Guid userId)
    {
        return await _context.SeasonRegistrations
            .AnyAsync(r => r.SeasonId == seasonId && r.UserId == userId);
    }

    public async Task<SeasonRegistration?> GetRegistrationAsync(Guid seasonId, Guid userId)
    {
        return await _context.SeasonRegistrations
            .FirstOrDefaultAsync(r => r.SeasonId == seasonId && r.UserId == userId);
    }

    public async Task DeleteRegistrationAsync(SeasonRegistration registration)
    {
        try
        {
            _context.SeasonRegistrations.Remove(registration);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting registration for Season {SeasonId} and User {UserId}",
                registration.SeasonId, registration.UserId);
            throw;
        }
    }

    public async Task<List<Season>> GetActiveSeasonsByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await _context.Seasons
            .Where(s => s.Status == SeasonStatus.Open &&
                       s.StartDate <= endDate &&
                       s.EndDate >= startDate)
            .OrderBy(s => s.StartDate)
            .ToListAsync();
    }
}