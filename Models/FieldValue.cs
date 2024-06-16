using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace divitiae_api.Models
{
    public class FieldValue : FieldStructure
    {
        [JsonConstructor]
        public FieldValue() { }
        public FieldValue(FieldStructure fieldStructure, dynamic value)
            :base(fieldStructure.Name, fieldStructure.Type)
        {
            this.Value = value;
            this.Id = fieldStructure.Id;
        }        
        public dynamic Value { get; set; }

    }

    public class FieldRelationValue
    {
        [JsonConstructor]
        public FieldRelationValue() { }
        public FieldRelationValue(string id, string name, string nameAsProperty, string type, ItemRelation value)
        {
            this.Name = name;
            this.NameAsProperty = nameAsProperty;
            this.Type = type;
            this.Value = value;
            this.Id = id;
        }

        public string Id { get; set; }
        public ItemRelation Value { get; set; }
        public string Name { get; set; } = string.Empty;
        public string NameAsProperty { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;

    }
}
