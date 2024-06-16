using divitiae_api.Models.Mailing;
using divitiae_api.Services.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using System.Net.Mail;

namespace divitiae_api.Services
{
    public class EmailServices : IEmailServices
    {

        private readonly EmailSettings emailSettings;

        public EmailServices(IOptions<EmailSettings> options)
        {
            this.emailSettings = options.Value;
        }

        /// <summary>
        /// Se configuran los parámetros necesarios para el envío de mail: remitente, asunto y cuerpo. Luego lo envía.
        /// </summary>
        /// <param name="mailRequest"></param>
        public async Task SendEmailAsync(MailRequest mailRequest)
        {
            var email = new MimeMessage();
            var from = new MailboxAddress("Divitiae", emailSettings.Email);
            email.From.Add(from);
            email.To.Add(MailboxAddress.Parse(mailRequest.ToEmail));
            email.Subject = mailRequest.Subject;
            var builder = new BodyBuilder();
            builder.HtmlBody = mailRequest.Body;
            email.Body = builder.ToMessageBody();

            using var smtp = new MailKit.Net.Smtp.SmtpClient();
            smtp.Connect(emailSettings.Host, emailSettings.Port, SecureSocketOptions.StartTls);
            smtp.Authenticate(emailSettings.Email, emailSettings.Password);
            await smtp.SendAsync(email);
            smtp.Disconnect(true);
        }
    }
}
