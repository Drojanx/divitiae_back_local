using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;

namespace divitiae_api.Models
{
    public class FilterObject
    {
        public string FieldName { get; set; } = string.Empty;
        public string FieldValue { get; set; } = string.Empty;

    }

}
