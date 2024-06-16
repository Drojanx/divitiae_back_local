using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace divitiae_api.Models.DTOs
{
    public class WorkEnvironmentUserDataDTO
    {
        public Guid UserId { get; set; }
        public string UserEmail { get; set; } = string.Empty;
        public string UserDisplayName { get; set; } = string.Empty;
        public bool IsAdmin { get; set; }
        public bool IsOwner { get; set; }
    }
}

