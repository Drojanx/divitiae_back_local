using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using System.ComponentModel.DataAnnotations;
using divitiae_api.Services;

namespace divitiae_api.Models
{
    public class User
    {
        [Key]
        public Guid Id { get; set; }
        public string GoogleUID { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public byte[] PasswordHash { get; set; }
        public byte[] PasswordSalt { get; set; }
        public List<UserToWorkEnvRole> UserToWorkEnvRole { get; set; } = new List<UserToWorkEnvRole>();
        public List<Workspace> Workspaces { get; set; } = new List<Workspace>();


    }
}
