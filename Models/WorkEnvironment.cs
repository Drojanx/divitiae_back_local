using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;

namespace divitiae_api.Models
{
    public class WorkEnvironment
    {
        [Key]
        public Guid Id { get; set; }
        public string EnvironmentName { get; set; } = string.Empty;
        public List<Workspace> Workspaces { get; set; } = new List<Workspace>();
        public List<UserToWorkEnvRole> UserToWorkEnvRole { get; set; } = new List<UserToWorkEnvRole>();
    }
}
