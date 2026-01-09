using System.Net;
using System.Net.Mail;

public interface IEmailService
{
    Task SendEmailAsync(string toEmail, string subject, string message);
}

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;

    public EmailService(IConfiguration config)
    {
        _config = config;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string message)
    {
        var emailSettings = _config.GetSection("EmailSettings");

        var mailMessage = new MailMessage
        {
            From = new MailAddress(emailSettings["SenderEmail"], emailSettings["SenderName"]),
            Subject = subject,
            Body = message,
            IsBodyHtml = true, 
        };
        mailMessage.To.Add(toEmail);

        using var smtpClient = new SmtpClient(emailSettings["MailServer"], int.Parse(emailSettings["MailPort"]))
        {
            Credentials = new NetworkCredential(emailSettings["SenderEmail"], emailSettings["Password"]),
            EnableSsl = true, // Gmail bắt buộc SSL
        };

        await smtpClient.SendMailAsync(mailMessage);
    }
}