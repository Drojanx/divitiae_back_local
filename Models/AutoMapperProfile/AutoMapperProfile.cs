using AutoMapper;
using divitiae_api.Models.DTOs;

namespace divitiae_api.Models.AutoMapperProfile
{
    public class AutoMapperProfile : Profile
    {
        public AutoMapperProfile() 
        {
            CreateMap<User, UserDTO>();
        }

    }
}
