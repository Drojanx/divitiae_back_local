using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Nodes;

namespace divitiae_api.Models
{
    public class ItemRelation
    {
        public ItemRelation(string relatedAppId, string relatedAppName, List<RelatedItem> relatedItems)
        {
            RelatedAppId = relatedAppId;
            RelatedAppName = relatedAppName;
            RelatedItems = relatedItems;
        }

        public string RelatedAppName { get; set; } = string.Empty;
        public string RelatedAppId { get; set; } = string.Empty;
        public List<RelatedItem> RelatedItems { get; set; }

    }

    public class RelatedItem
    {
        public RelatedItem (string relatedItemName, string relatedItemId)
        {
            RelatedItemName = relatedItemName;
            RelatedItemId = relatedItemId;
        }
        public string RelatedItemName { get; set; } = string.Empty;
        public string RelatedItemId { get; set; } = string.Empty;
    }
}
