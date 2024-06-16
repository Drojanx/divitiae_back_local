namespace divitiae_api.Models.Exceptions
{
    public class NoAdminPermissionException : Exception
    {
        public NoAdminPermissionException(string userId, string itemName, string itemId)
            : base($"User {userId} has no admin permissions on {itemName} {itemId}.")
        {
            UserId = itemName;
            ItemName = itemName;
            ItemId = itemId;
        }

        public string UserId { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string ItemId { get; set; } = string.Empty;  
    }
}
