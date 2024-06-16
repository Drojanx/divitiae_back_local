using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace divitiae_api.Models
{
    public class ItemActivityLogDTO
    {
        public string CreatorId { get; set; } = string.Empty;
        public long UnixCreatedOn { get; set; } = 0;
        public string LogText { get; set; } = string.Empty;
    }
}
