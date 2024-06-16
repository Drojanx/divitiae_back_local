using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using System.Text.Json.Nodes;

namespace divitiae_api.Models
{
    [Owned]
    public class FieldStructure
    {
        public FieldStructure(string name, string type)
        {
                Name = name;
                Type = type;
                NameAsProperty = name.ToLower().Replace(" ", "_");
                Id = ObjectId.GenerateNewId().ToString();
        }

        public FieldStructure() { }

        public string Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string NameAsProperty { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;


    }
    [Owned]
    public class FieldStructureRelation : FieldStructure
    {
        public FieldStructureRelation(FieldStructure fieldStructure, string appRelationId, string appRelationName) 
            : base(fieldStructure.Name, fieldStructure.Type)
        {
            this.AppRelationId = appRelationId;
            this.AppRelationName = appRelationName;
        }

        public FieldStructureRelation(string name, string type, string appRelationId, string appRelationName, string workspaceRelationId) 
            : base(name, type) 
        {
            this.AppRelationId = appRelationId;
            this.AppRelationName = appRelationName;
            this.WorkspaceRelationId = workspaceRelationId;
        }
        
        public FieldStructureRelation() { }


        public string WorkspaceRelationId { get; set; } = string.Empty;
        public string AppRelationId { get; set; } = string.Empty;
        public string AppRelationName { get; set; } = string.Empty;

    }
}
