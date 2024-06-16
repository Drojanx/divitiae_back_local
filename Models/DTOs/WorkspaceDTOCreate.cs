using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace divitiae_api.Models.DTOs
{
    public class WorkspaceDTOCreate
    {
        public string WorkspaceName { get; set; } = string.Empty;
    }
}

