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

    public SeasonService(
        ISeasonRepository seasonRepository,
        IUserRepository userRepository,
        IMapper mapper,
        ILogger<SeasonService> logger)
    {
        _seasonRepository = seasonRepository;
        _userRepository = userRepository;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<SeasonDto> CreateSeasonAsync(CreateSeasonDto createSeasonDto)
    {
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
        return seasons.Select(season => _mapper.Map<SeasonDto>(season));
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

        if (updateSeasonDto.Status.HasValue)
            season.Status = updateSeasonDto.Status.Value;

        if (updateSeasonDto.Notes != null)
            season.Notes = updateSeasonDto.Notes;

        await _seasonRepository.UpdateAsync(season);
    }

    public async Task DeleteSeasonAsync(Guid id)
    {
        var season = await _seasonRepository.GetByIdAsync(id);
        if (season == null)
        {
            throw new NotFoundException($"Season with ID {id} not found");
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
        season.Registrations.Add(registration);
        await _seasonRepository.UpdateAsync(season);

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

    private async Task<SeasonDto?> GetSeasonDtoAsync(Guid seasonId)
    {
        var season = await _seasonRepository.GetByIdWithRegistrationsAsync(seasonId);
        if (season == null)
        {
            return null;
        }

        var seasonDto = _mapper.Map<SeasonDto>(season);
        seasonDto.RegisteredUsersCount = season.Registrations.Count;
        seasonDto.RegisteredUsers = season.Registrations
            .Select(r => new SeasonUserDto
            {
                UserId = r.UserId,
                FirstName = r.User.FirstName,
                LastName = r.User.LastName,
                RegisteredOn = r.RegisteredOn
            })
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .ToList();

        return seasonDto;
    }
}