using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace divitiae_api.Models
{
    public class TaskItemDTO
    {
        public string Id { get; set; } = string.Empty;
        public string DescriptiveName { get; set; } = string.Empty;

    }
}
