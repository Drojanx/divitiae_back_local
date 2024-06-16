namespace divitiae_api.Models.Exceptions
{
    public class CustomBadRequestException : Exception
    {
        public CustomBadRequestException(string message)
            : base($"{message}")
        {
            Message = message;
        }

        public string Message { get; set; } = string.Empty;

    }
}
