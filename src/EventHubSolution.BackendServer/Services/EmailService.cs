using EventHubSolution.BackendServer.Services;
using EventHubSolution.ViewModels.Systems;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace TicketManagement.Api.Services;

public class EmailService : IEmailService
{
    private readonly EmailSettings emailSettings;

    private readonly ILogger<EmailService> logger;

    public EmailService(IOptions<EmailSettings> _mailSettings, ILogger<EmailService> _logger)
    {
        emailSettings = _mailSettings.Value;
        logger = _logger;
        logger.LogInformation("Create EmailService");
    }

    // Gửi email, theo nội dung trong mailContent
    public async Task SendMail(MailContent mailContent)
    {
        var email = new MimeMessage();
        email.Sender = new MailboxAddress(emailSettings.DisplayName, emailSettings.Email);
        email.From.Add(new MailboxAddress(emailSettings.DisplayName, emailSettings.Email));
        email.To.Add(MailboxAddress.Parse(mailContent.To));
        email.Subject = mailContent.Subject;


        var builder = new BodyBuilder();
        builder.HtmlBody = mailContent.Body;
        email.Body = builder.ToMessageBody();

        using var smtp = new SmtpClient();

        try
        {
            smtp.Connect(emailSettings.Host, emailSettings.Port, SecureSocketOptions.StartTls);
            smtp.Authenticate(emailSettings.Email, emailSettings.Password);
            await smtp.SendAsync(email);
        }
        catch (Exception ex)
        {
            System.IO.Directory.CreateDirectory("mailssave");
            var emailsavefile = string.Format(@"mailssave/{0}.eml", Guid.NewGuid());
            await email.WriteToAsync(emailsavefile);

            logger.LogInformation("Email sending email, save at - " + emailsavefile);
            logger.LogError(ex.Message);
        }

        smtp.Disconnect(true);

        logger.LogInformation("send mail to " + mailContent.To);

    }

    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        await SendMail(new MailContent()
        {
            To = email,
            Subject = subject,
            Body = htmlMessage
        });
    }
}