using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Nodes;

namespace divitiae_api.Models
{
        public class RelatedItemDTO
        {
        public RelatedItemDTO(string relatedAppId, string relatedItemName, string relatedItemId) 
        {
            RelatedAppId = relatedAppId;
            RelatedItemName = relatedItemName;
            RelatedItemId = relatedItemId;
        }
        public string RelatedAppId { get; set; } = string.Empty;
        public string RelatedItemName { get; set; } = string.Empty;
        public string RelatedItemId { get; set; } = string.Empty;

    }
}
