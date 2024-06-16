using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Nodes;

namespace divitiae_api.Models
{
    public class AppDTOCreate
    {
        public string AppName { get; set; } = string.Empty;
        public string AppIconId { get; set; } = string.Empty;
        public List<FieldStructure> Fields { get; set; } = new List<FieldStructure>();
        public List<FieldStructureRelation> FieldsRelation { get; set; } = new List<FieldStructureRelation>();
    }
}
