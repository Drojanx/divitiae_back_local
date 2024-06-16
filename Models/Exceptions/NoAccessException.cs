namespace divitiae_api.Models.Exceptions
{
    public class NoAccessException : Exception
    {
        public NoAccessException(string userId, string itemName, string itemId)
            : base($"The user {userId} has no access to {itemName} {itemId}.")
        {
            ItemName = itemName;
            ItemName = itemName;
            ItemId = itemId;
        }

        public string UserId { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string ItemId { get; set; } = string.Empty;  
    }
}
