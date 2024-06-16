using MongoDB.Bson;

namespace divitiae_api.Models.DTOs
{
    public class UserDTORegister
    {
        public string Name { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool GenerateSampleEnvironment { get; set; }
    }
}

