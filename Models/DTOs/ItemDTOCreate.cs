using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace divitiae_api.Models
{
    public class ItemDTOCreate
    {
        [JsonConstructor]
        public ItemDTOCreate() { }  
        public string DescriptiveName { get; set; } = string.Empty;
        public ICollection<FieldValue> FieldsValue { get; set; } = new List<FieldValue>();
        public ICollection<FieldRelationValue> FieldsRelationValue { get; set; } = new List<FieldRelationValue>();

    }
}
