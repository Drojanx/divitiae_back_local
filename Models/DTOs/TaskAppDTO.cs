using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Nodes;

namespace divitiae_api.Models
{
        public class TaskAppDTO
        {
            public string? Id { get; set; }
            public string? AppName { get; set; }

    }
}
