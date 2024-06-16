namespace divitiae_api.Models.ErrorHandling
{
    public class ErrorMessage
    {
        public int httpStatus { get; set; }
        public string Header { get; set; } = "Error";
        public string Message { get; set; } = string.Empty;
    }
}
