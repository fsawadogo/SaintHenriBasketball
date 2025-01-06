using AutoMapper;
using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Application.DTOs;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class SeasonSubscriptionService : ISeasonSubscriptionService
{
    private readonly ISeasonSubscriptionRepository _subscriptionRepository;
    private readonly IUserRepository _userRepository;
    private readonly IMapper _mapper;
    private readonly ILogger<SeasonSubscriptionService> _logger;

    public SeasonSubscriptionService(
        ISeasonSubscriptionRepository subscriptionRepository,
        IUserRepository userRepository,
        IMapper mapper,
        ILogger<SeasonSubscriptionService> logger)
    {
        _subscriptionRepository = subscriptionRepository;
        _userRepository = userRepository;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<SeasonSubscriptionDto> CreateSubscriptionAsync(Guid userId, CreateSeasonSubscriptionDto createDto)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            throw new NotFoundException(nameof(ApplicationUser), userId);
        }

        if (await HasActiveSubscriptionAsync(userId))
        {
            throw new ValidationException("User already has an active season subscription");
        }

        if (createDto.StartDate < DateTime.UtcNow)
        {
            throw new ValidationException("Start date cannot be in the past");
        }

        if (createDto.EndDate <= createDto.StartDate)
        {
            throw new ValidationException("End date must be after start date");
        }

        if (createDto.Price < 0)
        {
            throw new ValidationException("Price cannot be negative");
        }

        var subscription = new SeasonSubscription
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            StartDate = createDto.StartDate,
            EndDate = createDto.EndDate,
            Price = createDto.Price,
            PaymentStatus = PaymentStatus.Pending,
            CreatedOn = DateTime.UtcNow,
            IsActive = true
        };

        await _subscriptionRepository.AddAsync(subscription);

        // Update user's payment plan
        user.PaymentPlan = PaymentPlan.Season;
        await _userRepository.UpdateAsync(user);

        _logger.LogInformation("Season subscription created for user {UserId}", userId);

        return _mapper.Map<SeasonSubscriptionDto>(subscription);
    }

    public async Task<SeasonSubscriptionDto> GetSubscriptionAsync(Guid id)
    {
        var subscription = await _subscriptionRepository.GetByIdAsync(id);
        if (subscription == null)
        {
            throw new NotFoundException(nameof(SeasonSubscription), id);
        }

        return _mapper.Map<SeasonSubscriptionDto>(subscription);
    }

    public async Task<SeasonSubscriptionDto> GetActiveSubscriptionAsync(Guid userId)
    {
        var subscription = await _subscriptionRepository.GetActiveSubscriptionByUserIdAsync(userId);
        if (subscription == null)
        {
            throw new NotFoundException("No active subscription found for user");
        }

        return _mapper.Map<SeasonSubscriptionDto>(subscription);
    }

    public async Task<IReadOnlyList<SeasonSubscriptionDto>> GetAllSubscriptionsAsync()
    {
        var subscriptions = await _subscriptionRepository.GetAllAsync();
        return _mapper.Map<IReadOnlyList<SeasonSubscriptionDto>>(subscriptions);
    }

    public async Task<IReadOnlyList<SeasonSubscriptionDto>> GetActiveSubscriptionsAsync()
    {
        var subscriptions = await _subscriptionRepository.GetActiveSubscriptionsAsync();
        return _mapper.Map<IReadOnlyList<SeasonSubscriptionDto>>(subscriptions);
    }

    public async Task CancelSubscriptionAsync(Guid id)
    {
        var subscription = await _subscriptionRepository.GetByIdAsync(id);
        if (subscription == null)
        {
            throw new NotFoundException(nameof(SeasonSubscription), id);
        }

        subscription.IsActive = false;
        await _subscriptionRepository.UpdateAsync(subscription);

        // Update user's payment plan back to DropIn
        var user = await _userRepository.GetByIdAsync(subscription.UserId);
        if (user != null)
        {
            user.PaymentPlan = PaymentPlan.DropIn;
            await _userRepository.UpdateAsync(user);
        }

        _logger.LogInformation("Season subscription {SubscriptionId} cancelled", id);
    }

    public async Task UpdatePaymentStatusAsync(Guid id, PaymentStatus status)
    {
        var subscription = await _subscriptionRepository.GetByIdAsync(id);
        if (subscription == null)
        {
            throw new NotFoundException(nameof(SeasonSubscription), id);
        }

        subscription.PaymentStatus = status;
        await _subscriptionRepository.UpdateAsync(subscription);

        _logger.LogInformation("Season subscription {SubscriptionId} payment status updated to {Status}",
            id, status);
    }

    public async Task<bool> HasActiveSubscriptionAsync(Guid userId)
    {
        return await _subscriptionRepository.HasActiveSubscriptionAsync(userId);
    }
}