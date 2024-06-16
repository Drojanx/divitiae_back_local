using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace divitiae_api.Models.DTOs
{
    public class WorkEnvironmentDTO
    {
        public Guid Id { get; set; }
        public string EnvironmentName { get; set; } = string.Empty;
        public List<WorkspaceDTO> Workspaces { get; set; } = new List<WorkspaceDTO>();
        public List<WorkEnvironmentUserDataDTO> UsersData { get; set; } = new List<WorkEnvironmentUserDataDTO>();
    }
}

