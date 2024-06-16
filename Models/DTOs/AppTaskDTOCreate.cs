using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;

namespace divitiae_api.Models
{
    public class AppTaskDTOCreate
    {
        public string EnvironmentId { get; set; } = string.Empty;
        public string AssignedUserId { get; set; } = string.Empty;
        public string? WorkspaceId { get; set; }
        public string? AppId { get; set; }
        public string? ItemId { get; set; }
        public long DueDate { get; set; }
        public string Information { get; set; } = string.Empty;
        public bool Finished { get; set; }

    }
}
