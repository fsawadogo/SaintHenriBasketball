using AutoMapper;
using SaintHenriBasketball.Application.DTOs;
using SaintHenriBasketball.Application.DTOs.Auth;
using SaintHenriBasketball.Domain.Entities;

namespace SaintHenriBasketball.Application.Mapping;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<Player, PlayerDto>();
        CreateMap<CreatePlayerDto, Player>();
        CreateMap<UpdatePlayerDto, Player>();

        CreateMap<TrainingSession, SessionDto>()
            .ForMember(dest => dest.RegisteredPlayersCount,
                      opt => opt.MapFrom(src => src.Registrations.Count));

        CreateMap<SessionRegistration, SessionRegistrationDto>()
            .ForMember(dest => dest.PlayerName,
                      opt => opt.MapFrom(src => src.Player.Name))
            .ForMember(dest => dest.SessionDate,
                      opt => opt.MapFrom(src => src.Session.SessionDate));

        CreateMap<ApplicationUser, UserDto>();
    }
}