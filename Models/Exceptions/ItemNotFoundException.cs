namespace divitiae_api.Models.Exceptions
{
    public class ItemNotFoundException : Exception
    {
        public ItemNotFoundException(string itemName, string fieldName,string fieldValue)
            : base($"The {itemName} with {fieldName} {fieldValue} was not found.")
        {
            ItemName = itemName;
            FieldName = fieldName;
            FieldValue = fieldValue;
        }

        public string ItemName { get; set; } = string.Empty;
        public string FieldName { get; set; } = string.Empty;
        public string FieldValue { get; set; } = string.Empty;  
    }
}
