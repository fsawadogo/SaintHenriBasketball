using AutoMapper;
using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Application.DTOs.Season;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class SeasonService : ISeasonService
{
    private readonly ISeasonRepository _seasonRepository;
    private readonly IUserRepository _userRepository;
    private readonly IMapper _mapper;
    private readonly ILogger<SeasonService> _logger;
    private readonly IEmailService _emailService;
    private readonly ICacheService _cacheService;

    // Cache keys
    private const string CurrentSeasonCacheKey = "CurrentSeason";
    private const string AllSeasonsCacheKey = "AllSeasons";
    private const string SeasonKeyPrefix = "Season_";


    public SeasonService(
        ISeasonRepository seasonRepository,
        IUserRepository userRepository,
        IMapper mapper,
        IEmailService emailService,
        ILogger<SeasonService> logger,
        ICacheService cacheService)
    {
        _seasonRepository = seasonRepository;
        _userRepository = userRepository;
        _mapper = mapper;
        _emailService = emailService;
        _logger = logger;
        _cacheService = cacheService;
    }

    public async Task<SeasonDto> CreateSeasonAsync(CreateSeasonDto createSeasonDto)
    {
        var currentSeason = await _seasonRepository.GetCurrentSeasonAsync();
        if (currentSeason != null)
        {
            throw new ValidationException("Cannot create a new season while another season is active");
        }

        var season = new Season(
            createSeasonDto.StartDate,
            createSeasonDto.EndDate,
            createSeasonDto.Price,
            createSeasonDto.Notes
        );

        await _seasonRepository.AddAsync(season);
        
        // Invalidate affected caches
        await _cacheService.RemoveAsync(AllSeasonsCacheKey);
        await _cacheService.RemoveAsync(CurrentSeasonCacheKey);
        
        return (await GetSeasonDtoAsync(season.Id))!;
    }

    public async Task<SeasonDto> GetSeasonAsync(Guid id)
    {
        string cacheKey = $"{SeasonKeyPrefix}{id}";

        // Try to get from cache first
        var cachedSeason = await _cacheService.GetAsync<SeasonDto>(cacheKey);

        if (cachedSeason != null)
        {
            _logger.LogInformation("Season {SeasonId} retrieved from cache", id);
            return cachedSeason;
        }

        // If not in cache, get from database
        var seasonDto = await GetSeasonDtoAsync(id);
        if (seasonDto == null)
        {
            throw new NotFoundException($"Season with ID {id} not found");
        }

        // Store in cache with a 1-hour expiration
        await _cacheService.SetAsync(cacheKey, seasonDto, TimeSpan.FromHours(1));

        return seasonDto;
    }

    public async Task<IEnumerable<SeasonDto>> GetAllSeasonsAsync()
    {
        // Try to get from cache first
        var cachedSeasons = await _cacheService.GetAsync<IEnumerable<SeasonDto>>(AllSeasonsCacheKey);

        if (cachedSeasons != null)
        {
            _logger.LogInformation("All seasons retrieved from cache");
            return cachedSeasons;
        }

        // If not in cache, get from database
        var seasons = await _seasonRepository.GetAllWithRegistrationsAsync();
        var currentSeason = seasons.FirstOrDefault(s =>
            s.Status == SeasonStatus.Open &&
            s.StartDate <= DateTime.UtcNow &&
            s.EndDate >= DateTime.UtcNow);

        var seasonsDto = seasons.Select(season => {
            var dto = _mapper.Map<SeasonDto>(season);
            dto.IsCurrentSeason = season.Id == currentSeason?.Id;
            return dto;
        }).ToList();

        // Store in cache with a 1-hour expiration
        await _cacheService.SetAsync(AllSeasonsCacheKey, seasonsDto, TimeSpan.FromHours(1));

        return seasonsDto;
    }

    public async Task<SeasonDto> GetCurrentSeasonAsync()
    {
        // Try to get from cache first
        var cachedSeason = await _cacheService.GetAsync<SeasonDto>(CurrentSeasonCacheKey);

        if (cachedSeason != null)
        {
            _logger.LogInformation("Current season retrieved from cache");
            return cachedSeason;
        }

        // If not in cache, get from database
        var season = await _seasonRepository.GetCurrentSeasonAsync();
        if (season == null)
        {
            throw new NotFoundException("No active season found");
        }

        var seasonDto = _mapper.Map<SeasonDto>(season);
        seasonDto.IsCurrentSeason = true;

        // Store in cache with a 30-minute expiration
        await _cacheService.SetAsync(CurrentSeasonCacheKey, seasonDto, TimeSpan.FromMinutes(30));

        return seasonDto;
    }

    public async Task UpdateSeasonAsync(Guid id, UpdateSeasonDto updateSeasonDto)
    {
        var season = await _seasonRepository.GetByIdAsync(id);
        if (season == null)
        {
            throw new NotFoundException($"Season with ID {id} not found");
        }

        if (updateSeasonDto.StartDate.HasValue)
            season.StartDate = updateSeasonDto.StartDate.Value;

        if (updateSeasonDto.EndDate.HasValue)
            season.EndDate = updateSeasonDto.EndDate.Value;

        if (updateSeasonDto.Price.HasValue)
            season.Price = updateSeasonDto.Price.Value;

        if (updateSeasonDto.Notes != null)
            season.Notes = updateSeasonDto.Notes;

        await _seasonRepository.UpdateAsync(season);

        // Invalidate affected caches
        await _cacheService.RemoveAsync(CurrentSeasonCacheKey);
        await _cacheService.RemoveAsync(AllSeasonsCacheKey);
        await _cacheService.RemoveAsync($"{SeasonKeyPrefix}{id}");
    }

    public async Task UpdateSeasonStatusAsync(Guid id, SeasonStatus status)
    {
        var season = await _seasonRepository.GetByIdAsync(id);
        if (season == null)
        {
            throw new NotFoundException($"Season with ID {id} not found");
        }

        if (status == SeasonStatus.Open)
        {
            var currentSeason = await _seasonRepository.GetCurrentSeasonAsync();
            if (currentSeason != null && currentSeason.Id != id)
            {
                throw new ValidationException("Cannot open a new season while another season is active");
            }
        }

        season.Status = status;
        await _seasonRepository.UpdateAsync(season);
        
        // Invalidate affected caches
        await _cacheService.RemoveAsync(CurrentSeasonCacheKey);
        await _cacheService.RemoveAsync(AllSeasonsCacheKey);
        await _cacheService.RemoveAsync($"{SeasonKeyPrefix}{id}");
    }

    public async Task DeleteSeasonAsync(Guid id)
    {
        var season = await _seasonRepository.GetByIdWithRegistrationsAsync(id);
        if (season == null)
        {
            throw new NotFoundException($"Season with ID {id} not found");
        }

        if (season.Registrations?.Any() == true)
        {
            throw new ValidationException("Cannot delete a season that has registered users");
        }

        await _seasonRepository.DeleteAsync(season);
        
        // Invalidate affected caches
        await _cacheService.RemoveAsync(AllSeasonsCacheKey);
        await _cacheService.RemoveAsync(CurrentSeasonCacheKey);
        await _cacheService.RemoveAsync($"{SeasonKeyPrefix}{id}");
    }

    public async Task<SeasonDto> RegisterUserForSeasonAsync(Guid seasonId, Guid userId)
    {
        var season = await _seasonRepository.GetByIdWithRegistrationsAsync(seasonId);
        if (season == null)
        {
            throw new NotFoundException($"Season with ID {seasonId} not found");
        }

        if (season.Status != SeasonStatus.Open)
        {
            throw new ValidationException("Cannot register for a closed season");
        }

        var isRegistered = await _seasonRepository.HasUserRegisteredAsync(seasonId, userId);
        if (isRegistered)
        {
            throw new ValidationException("User is already registered for this season");
        }

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            throw new NotFoundException($"User with ID {userId} not found");
        }

        var registration = new SeasonRegistration(seasonId, userId);
        await _seasonRepository.AddRegistrationAsync(registration);

        try
        {
            // Send confirmation email
            await _emailService.SendSeasonRegistrationConfirmationEmailAsync(registration);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send season registration confirmation email");
        }

        return (await GetSeasonDtoAsync(seasonId))!;
    }

    public async Task UnregisterUserFromSeasonAsync(Guid seasonId, Guid userId)
    {
        var registration = await _seasonRepository.GetRegistrationAsync(seasonId, userId);
        if (registration == null)
        {
            throw new NotFoundException("Registration not found");
        }

        await _seasonRepository.DeleteRegistrationAsync(registration);
    }

    public async Task<IEnumerable<SeasonUserDto>> GetRegisteredUsersAsync(Guid seasonId)
    {
        var season = await _seasonRepository.GetByIdWithRegistrationsAsync(seasonId);
        if (season == null)
        {
            throw new NotFoundException($"Season with ID {seasonId} not found");
        }

        return season.Registrations
            .Select(r => new SeasonUserDto
            {
                UserId = r.UserId,
                FirstName = r.User.FirstName,
                LastName = r.User.LastName,
                RegisteredOn = r.RegisteredOn,
                PaymentPlan = r.User.PaymentPlan,
                Email = r.User.Email
            })
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .ToList();
    }

    private async Task<SeasonDto?> GetSeasonDtoAsync(Guid seasonId)
    {
        var season = await _seasonRepository.GetByIdWithRegistrationsAsync(seasonId);
        if (season == null)
        {
            return null;
        }

        var currentSeason = await _seasonRepository.GetCurrentSeasonAsync();
        var seasonDto = _mapper.Map<SeasonDto>(season);
        seasonDto.IsCurrentSeason = season.Id == currentSeason?.Id;
        seasonDto.RegisteredUsersCount = season.Registrations.Count;
        seasonDto.RegisteredUsers = season.Registrations
            .Select(r => new SeasonUserDto
            {
                UserId = r.UserId,
                FirstName = r.User.FirstName,
                LastName = r.User.LastName,
                RegisteredOn = r.RegisteredOn,
                PaymentPlan = r.User.PaymentPlan,
                Email = r.User.Email
            })
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .ToList();

        return seasonDto;
    }
}