using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace divitiae_api.Models.DTOs
{
    public class WorkspaceDTO
    {
        public string Id { get; set; } = string.Empty;
        public string WorkspaceName { get; set; } = string.Empty;
        public List<AppDTO> Apps { get; set; } = new List<AppDTO>();
    }
}

