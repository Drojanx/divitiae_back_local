using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;

namespace divitiae_api.Models
{
    public class Workspace
    {
        [Key]
        public Guid Id { get; set; }
        public string WorkspaceName { get; set; } = string.Empty;
        public Guid WorkenvironmentId { get; set; }
        public WorkEnvironment Workenvironment { get; set; }
        public List<WsAppsRelation> WsAppsRelations { get; set; } = new List<WsAppsRelation>();
        public List<User> Users { get; set; } = new List<User>();

    }
}
