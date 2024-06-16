using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace divitiae_api.Models
{
    public class ItemActivityLogDTO
    {
        public string Id { get; set; } = string.Empty;
        public string ItemId { get; set; } = string.Empty;
        public string AppId { get; set; } = string.Empty;
        public string CreatorId { get; set; } = string.Empty;
        public long UnixCreatedOn { get; set; } = 0;
        public string LogText { get; set; } = string.Empty;
    }
}
