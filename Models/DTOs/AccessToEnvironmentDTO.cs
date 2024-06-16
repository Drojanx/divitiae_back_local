namespace divitiae_api.Models.DTOs
{
    public class AccessToEnvironmentDTO
    {
        public string UserId { get; set; } 
        public List<string> WorkspaceIds { get; set; } = new List<string>();
        public bool IsAdmin { get; set; } = false;
        public bool IsOwner { get; set; } = false;
    }
}
