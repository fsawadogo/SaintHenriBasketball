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

    public SeasonService(
        ISeasonRepository seasonRepository,
        IUserRepository userRepository,
        IMapper mapper,
        IEmailService emailService,
        ILogger<SeasonService> logger)
    {
        _seasonRepository = seasonRepository;
        _userRepository = userRepository;
        _mapper = mapper;
        _emailService = emailService;
        _logger = logger;
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
        return await GetSeasonDtoAsync(season.Id);
    }

    public async Task<SeasonDto> GetSeasonAsync(Guid id)
    {
        var seasonDto = await GetSeasonDtoAsync(id);
        if (seasonDto == null)
        {
            throw new NotFoundException($"Season with ID {id} not found");
        }

        return seasonDto;
    }

    public async Task<IEnumerable<SeasonDto>> GetAllSeasonsAsync()
    {
        var seasons = await _seasonRepository.GetAllWithRegistrationsAsync();
        var currentSeason = seasons.FirstOrDefault(s =>
            s.Status == SeasonStatus.Open &&
            s.StartDate <= DateTime.UtcNow &&
            s.EndDate >= DateTime.UtcNow);

        return seasons.Select(season => {
            var dto = _mapper.Map<SeasonDto>(season);
            dto.IsCurrentSeason = season.Id == currentSeason?.Id;
            return dto;
        });
    }

    public async Task<SeasonDto> GetCurrentSeasonAsync()
    {
        var season = await _seasonRepository.GetCurrentSeasonAsync();
        if (season == null)
        {
            throw new NotFoundException("No active season found");
        }

        var seasonDto = _mapper.Map<SeasonDto>(season);
        seasonDto.IsCurrentSeason = true;
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

        return await GetSeasonDtoAsync(seasonId);
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