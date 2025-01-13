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

    public async Task<Season?> GetCurrentSeasonAsync()
    {
        var now = DateTime.UtcNow;
        return await _context.Seasons
            .Include(s => s.Registrations)
                .ThenInclude(r => r.User)
            .FirstOrDefaultAsync(s => s.Status == SeasonStatus.Open);
                //s.Status == SeasonStatus.Open &&
                //s.StartDate <= now &&
                //s.EndDate >= now);
    }

    public async Task<bool> HasUserRegisteredAsync(Guid seasonId, Guid userId)
    {
        return await _context.SeasonRegistrations
            .AnyAsync(r => r.SeasonId == seasonId && r.UserId == userId);
    }

    public async Task<SeasonRegistration?> GetRegistrationAsync(Guid seasonId, Guid userId)
    {
        return await _context.SeasonRegistrations
            .Include(r => r.Season)
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.SeasonId == seasonId && r.UserId == userId);
    }

    public async Task<Season> AddAsync(Season season)
    {
        try
        {
            _logger.LogInformation("Adding new season with start date {StartDate}", season.StartDate);
            await _context.Seasons.AddAsync(season);
            await _context.SaveChangesAsync();
            return season;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding season with start date {StartDate}", season.StartDate);
            throw;
        }
    }

    public async Task AddRegistrationAsync(SeasonRegistration registration)
    {
        try
        {
            _logger.LogInformation("Adding registration for user {UserId} to season {SeasonId}",
                registration.UserId, registration.SeasonId);

            await _context.SeasonRegistrations.AddAsync(registration);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding registration for user {UserId} to season {SeasonId}",
                registration.UserId, registration.SeasonId);
            throw;
        }
    }
    public async Task UpdateAsync(Season season)
    {
        try
        {
            _logger.LogInformation("Updating season {SeasonId}", season.Id);
            _context.Entry(season).State = EntityState.Modified;
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating season {SeasonId}", season.Id);
            throw;
        }
    }

    public async Task DeleteAsync(Season season)
    {
        try
        {
            _logger.LogInformation("Deleting season {SeasonId}", season.Id);
            _context.Seasons.Remove(season);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting season {SeasonId}", season.Id);
            throw;
        }
    }

    public async Task DeleteRegistrationAsync(SeasonRegistration registration)
    {
        try
        {
            _logger.LogInformation("Deleting season registration for user {UserId} in season {SeasonId}",
                registration.UserId, registration.SeasonId);

            _context.SeasonRegistrations.Remove(registration);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting season registration for user {UserId} in season {SeasonId}",
                registration.UserId, registration.SeasonId);
            throw;
        }
    }
}