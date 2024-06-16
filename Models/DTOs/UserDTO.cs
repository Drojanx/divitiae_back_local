using MongoDB.Bson;

namespace divitiae_api.Models.DTOs
{
    public class UserDTO
    {
        //public string GoogleUID { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
}

