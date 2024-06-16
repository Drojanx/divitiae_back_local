using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;

namespace divitiae_api.Models
{
    public class App
    {
        [BsonId]
        public ObjectId Id { get; set; }
        public string AppName { get; set; } = string.Empty;
        public string AppIconId { get; set; } = string.Empty;
        public string WorkspaceId { get; set; }
        public List<FieldStructure> Fields { get; set; } = new List<FieldStructure>();
        public ICollection<FieldStructureRelation> RelationFields { get; set; } = new List<FieldStructureRelation>(); 
        public List<ObjectId> Items { get; set; } = new List<ObjectId>();
    }

}
