using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace divitiae_api.Models.DTOs
{
    public class UserDataDTO
    {
        public Guid UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string UserLastName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public List<UserWorkEnvironmentDataDTO> WorkEnvironments { get; set; } = new List<UserWorkEnvironmentDataDTO>();
        public List<AppTaskDTO> UserTasks { get; set; } = new List<AppTaskDTO> { };

    }
}

