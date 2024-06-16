using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Nodes;

namespace divitiae_api.Models
{
        public class Item
        {
        [BsonId]
        public ObjectId Id { get; set; }
        public string DescriptiveName { get; set; } = string.Empty;
        public ICollection<FieldValue> FieldsValue { get; set; } = new List<FieldValue>();
        public ICollection<FieldRelationValue> FieldsRelationValue { get; set; } = new List<FieldRelationValue>();
        public List<ItemRelation> Relations { get; set; } = new List<ItemRelation>();
    }
}
