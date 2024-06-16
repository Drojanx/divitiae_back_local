using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;

namespace divitiae_api.Models
{
    public class WsAppsRelation
    {
        [Key]
        public Guid Id { get; set; }
        public string AppId { get; set; }
        public Guid WorkspaceId { get; set; }
        public Workspace Workspace { get; set; }

    }

}
