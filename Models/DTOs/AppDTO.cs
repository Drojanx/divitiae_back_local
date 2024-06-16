using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Nodes;

namespace divitiae_api.Models
{
        public class AppDTO
        {
            public string Id { get; set; } = string.Empty;
            public string AppName { get; set; } = string.Empty;
            public string AppIconId { get; set; } = string.Empty;
            public ICollection<FieldStructure> Fields { get; set; }
            public ICollection<FieldStructureRelation> RelationFields { get; set; }

    }
}
