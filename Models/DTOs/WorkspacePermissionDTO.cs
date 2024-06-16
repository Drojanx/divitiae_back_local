using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace divitiae_api.Models.DTOs
{
    public class WorkspacePermissionDTO
    {
        public string Id { get; set; } = string.Empty;
        public bool Access { get; set; } 
    }
}

