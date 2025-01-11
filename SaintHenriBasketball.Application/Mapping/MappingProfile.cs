using AutoMapper;
using SaintHenriBasketball.Application.DTOs;
using SaintHenriBasketball.Application.DTOs.Attendance;
using SaintHenriBasketball.Application.DTOs.Season;
using SaintHenriBasketball.Application.DTOs.Session;
using SaintHenriBasketball.Application.DTOs.Users;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.Mapping;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // User mappings
        CreateMap<ApplicationUser, UserDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email))
            .ForMember(dest => dest.Username, opt => opt.MapFrom(src => src.Username))
            .ForMember(dest => dest.FirstName, opt => opt.MapFrom(src => src.FirstName))
            .ForMember(dest => dest.LastName, opt => opt.MapFrom(src => src.LastName))
            .ForMember(dest => dest.IsAdmin, opt => opt.MapFrom(src => src.IsAdmin))
            .ForMember(dest => dest.PaymentPlan, opt => opt.MapFrom(src => src.PaymentPlan))
            .ForMember(dest => dest.CreatedOn, opt => opt.MapFrom(src => src.CreatedOn));

        CreateMap<RegisterUserDto, ApplicationUser>()
            .ForMember(dest => dest.PasswordHash, opt => opt.Ignore())
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedOn, opt => opt.Ignore())
            .ForMember(dest => dest.SessionRegistrations, opt => opt.Ignore())
            .ForMember(dest => dest.EmailConfirmed, opt => opt.Ignore())
            .ForMember(dest => dest.EmailConfirmationToken, opt => opt.Ignore())
            .ForMember(dest => dest.PasswordResetToken, opt => opt.Ignore())
            .ForMember(dest => dest.PasswordResetTokenExpiry, opt => opt.Ignore());

        CreateMap<ApplicationUser, UserResponseDto>()
            .ForMember(dest => dest.Token, opt => opt.Ignore())
            .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email))
            .ForMember(dest => dest.Username, opt => opt.MapFrom(src => src.Username))
            .ForMember(dest => dest.FirstName, opt => opt.MapFrom(src => src.FirstName))
            .ForMember(dest => dest.LastName, opt => opt.MapFrom(src => src.LastName))
            .ForMember(dest => dest.IsAdmin, opt => opt.MapFrom(src => src.IsAdmin))
            .ForMember(dest => dest.PaymentPlan, opt => opt.MapFrom(src => src.PaymentPlan));

        // Session mappings
        CreateMap<Session, SessionDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.SessionDate, opt => opt.MapFrom(src => src.SessionDate))
            .ForMember(dest => dest.MaxCapacity, opt => opt.MapFrom(src => src.MaxCapacity))
            .ForMember(dest => dest.DropInPrice, opt => opt.MapFrom(src => src.DropInPrice))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status))
            .ForMember(dest => dest.RegisteredPlayersCount, opt => opt.MapFrom(src => src.RegisteredPlayersCount));

        CreateMap<CreateSessionDto, Session>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => SessionStatus.Open))
            .ForMember(dest => dest.RegisteredPlayersCount, opt => opt.MapFrom(src => 0))
            .ForMember(dest => dest.Registrations, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedOn, opt => opt.Ignore());

        // Session Registration mappings
        CreateMap<SessionRegistration, SessionRegistrationResponseDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.SessionId, opt => opt.MapFrom(src => src.SessionId))
            .ForMember(dest => dest.UserId, opt => opt.MapFrom(src => src.UserId))
            .ForMember(dest => dest.PaymentPlan, opt => opt.MapFrom(src => src.PaymentPlan))
            .ForMember(dest => dest.RegistrationDate, opt => opt.MapFrom(src => src.RegistrationDate));

        // Session detail mapping with registered players
        CreateMap<Session, SessionDetailDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.SessionDate, opt => opt.MapFrom(src => src.SessionDate))
            .ForMember(dest => dest.MaxCapacity, opt => opt.MapFrom(src => src.MaxCapacity))
            .ForMember(dest => dest.DropInPrice, opt => opt.MapFrom(src => src.DropInPrice))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status))
            .ForMember(dest => dest.RegisteredPlayersCount, opt => opt.MapFrom(src => src.RegisteredPlayersCount))
            .ForMember(dest => dest.RegisteredPlayers, opt => opt.MapFrom(src => src.Registrations.Select(r => r.User)));

        // Update session mapping
        CreateMap<UpdateSessionDto, Session>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.Registrations, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedOn, opt => opt.Ignore())
            .ForMember(dest => dest.RegisteredPlayersCount, opt => opt.Ignore());

        // Season mappings
        CreateMap<Season, SeasonDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.StartDate, opt => opt.MapFrom(src => src.StartDate))
            .ForMember(dest => dest.EndDate, opt => opt.MapFrom(src => src.EndDate))
            .ForMember(dest => dest.Price, opt => opt.MapFrom(src => src.Price))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status))
            .ForMember(dest => dest.Notes, opt => opt.MapFrom(src => src.Notes))
            .ForMember(dest => dest.CreatedOn, opt => opt.MapFrom(src => src.CreatedOn))
            .ForMember(dest => dest.RegisteredUsersCount, opt => opt.MapFrom(src => src.Registrations.Count))
            .ForMember(dest => dest.RegisteredUsers, opt => opt.MapFrom(src => src.Registrations));

        CreateMap<CreateSeasonDto, Season>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedOn, opt => opt.Ignore())
            .ForMember(dest => dest.Registrations, opt => opt.Ignore());

        CreateMap<UpdateSeasonDto, Season>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedOn, opt => opt.Ignore())
            .ForMember(dest => dest.Registrations, opt => opt.Ignore());

        CreateMap<SeasonRegistration, SeasonUserDto>()
            .ForMember(dest => dest.UserId, opt => opt.MapFrom(src => src.UserId))
            .ForMember(dest => dest.FirstName, opt => opt.MapFrom(src => src.User.FirstName))
            .ForMember(dest => dest.LastName, opt => opt.MapFrom(src => src.User.LastName))
            .ForMember(dest => dest.RegisteredOn, opt => opt.MapFrom(src => src.RegisteredOn));

        // Season Subscription mappings
        CreateMap<SeasonSubscription, SeasonSubscriptionDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.UserId, opt => opt.MapFrom(src => src.UserId))
            .ForMember(dest => dest.UserEmail, opt => opt.MapFrom(src => src.User.Email))
            .ForMember(dest => dest.UserName, opt => opt.MapFrom(src =>
                $"{src.User.FirstName} {src.User.LastName}"))
            .ForMember(dest => dest.StartDate, opt => opt.MapFrom(src => src.StartDate))
            .ForMember(dest => dest.EndDate, opt => opt.MapFrom(src => src.EndDate))
            .ForMember(dest => dest.Price, opt => opt.MapFrom(src => src.Price))
            .ForMember(dest => dest.PaymentStatus, opt => opt.MapFrom(src => src.PaymentStatus))
            .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => src.IsActive));

        CreateMap<CreateSeasonSubscriptionDto, SeasonSubscription>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.UserId, opt => opt.Ignore())
            .ForMember(dest => dest.User, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedOn, opt => opt.MapFrom(src => DateTime.UtcNow))
            .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => true))
            .ForMember(dest => dest.PaymentStatus, opt => opt.MapFrom(src => PaymentStatus.Pending));

        // Attendance mappings
        CreateMap<SessionAttendance, AttendanceResponseDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.SessionId, opt => opt.MapFrom(src => src.SessionId))
            .ForMember(dest => dest.UserId, opt => opt.MapFrom(src => src.UserId))
            .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => 
                $"{src.User.FirstName} {src.User.LastName}"))
            .ForMember(dest => dest.IsPresent, opt => opt.MapFrom(src => src.IsPresent))
            .ForMember(dest => dest.CheckInTime, opt => opt.MapFrom(src => src.CheckInTime))
            .ForMember(dest => dest.Notes, opt => opt.MapFrom(src => src.Notes))
            .ForMember(dest => dest.CreatedOn, opt => opt.MapFrom(src => src.CreatedOn))
            .ForMember(dest => dest.LastUpdated, opt => opt.MapFrom(src => src.LastUpdated))
            .ForMember(dest => dest.UpdateReason, opt => opt.MapFrom(src => src.UpdateReason));

        CreateMap<Session, SessionStatDto>()
            .ForMember(dest => dest.SessionId, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.Date, opt => opt.MapFrom(src => src.SessionDate))
            .ForMember(dest => dest.TotalPresent, opt => opt.Ignore())
            .ForMember(dest => dest.AttendanceRate, opt => opt.Ignore());
    }
}