using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Nodes;

namespace divitiae_api.Models
{
    public class TaskCommentDTO
    {
        public string UserName { get; set; } = string.Empty;
        public string UserLastName { get; set; } = string.Empty;
        public long CreatedOn { get; set; }
        public string Comment { get; set; } = string.Empty;

    }
}
