using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace divitiae_api.Models
{
    public class ItemDTO
    {
        [JsonConstructor]
        public ItemDTO() { }  
        public string Id { get; set; } = string.Empty;
        public string DescriptiveName { get; set; } = string.Empty;
        public ICollection<FieldValue> FieldsValue { get; set; } = new List<FieldValue>();
        public ICollection<FieldRelationValue> FieldsRelationValue { get; set; } = new List<FieldRelationValue>();
        public List<ItemRelation> Relations { get; set; } = new List<ItemRelation>();

    }
}
