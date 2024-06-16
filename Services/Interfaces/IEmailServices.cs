using divitiae_api.Models.Mailing;

namespace divitiae_api.Services.Interfaces
{
    public interface IEmailServices
    {
        Task SendEmailAsync(MailRequest mailRequest);
    }
}
