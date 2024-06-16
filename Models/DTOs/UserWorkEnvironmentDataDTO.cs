using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace divitiae_api.Models.DTOs
{
    public class UserWorkEnvironmentDataDTO
    {
        public Guid Id { get; set; }
        public string EnvironmentName { get; set; } = string.Empty;
        public bool IsAdmin { get; set; }
        public bool IsOwner { get; set; }
        public dynamic Workspaces { get; set; } = new List<dynamic>();
        //public List<WorkEnvironmentUserDataDTO> UsersData { get; set; } = new List<WorkEnvironmentUserDataDTO>();

    }
}

