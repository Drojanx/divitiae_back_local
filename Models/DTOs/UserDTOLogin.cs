using MongoDB.Bson;

namespace divitiae_api.Models.DTOs
{
    public class UserDTOLogin
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string GoogleUID { get; set; } = string.Empty;

    }
}

