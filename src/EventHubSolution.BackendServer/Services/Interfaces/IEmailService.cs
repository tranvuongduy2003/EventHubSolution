using EventHubSolution.ViewModels.General;

namespace EventHubSolution.BackendServer.Services
{
    public interface IEmailService
    {
        Task SendMail(MailContent mailContent);

        Task SendEmailAsync(string email, string subject, string htmlMessage);
    }
}
