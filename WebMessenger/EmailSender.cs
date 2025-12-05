using System.Net.Mail;

namespace WebMessenger
{
    public interface IEmailSender
    {
        Task SendEmailAsync(string email, string subject, string htmlMessage);
    }

    public class LocalSmtpEmailSender(ILogger<LocalSmtpEmailSender> logger) : IEmailSender
    {
        private readonly ILogger<LocalSmtpEmailSender> _logger = logger;

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            using MailMessage mm = new("server@webmessenger.ru", email);
            mm.Subject = subject;
            mm.Body = htmlMessage;
            mm.IsBodyHtml = true;

            using SmtpClient sc = new("localhost", 25);
            sc.EnableSsl = false;
            sc.UseDefaultCredentials = false;
            sc.DeliveryMethod = SmtpDeliveryMethod.Network;
            await sc.SendMailAsync(mm);
            _logger.LogInformation($"Email sent via open relay to {email}");
        }
    }
}
