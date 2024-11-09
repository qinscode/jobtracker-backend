using JobTracker.Models;

namespace JobTracker.Services.Interfaces;

public interface IEmailService
{
    Task<IEnumerable<EmailMessage>> FetchNewEmailsAsync(UserEmailConfig config);
    Task ConnectAndAuthenticateAsync(string email, string password);
} 