using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;

namespace divitiae_api.Models
{
    public class UserToWorkEnvRole
    {
        public Guid UserId { get; set; }
        public User User { get; set; }
        public Guid WorkEnvironmentId { get; set; }
        public WorkEnvironment WorkEnvironment { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsOwner { get; set; }
    }
}
