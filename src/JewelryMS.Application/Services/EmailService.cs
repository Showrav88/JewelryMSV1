using Microsoft.Extensions.Logging;
using JewelryMS.Domain.Interfaces.Services;

namespace JewelryMS.Application.Services;

public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;

    public EmailService(ILogger<EmailService> logger)
    {
        _logger = logger;
    }

    public Task SendPasswordResetEmailAsync(string toEmail, string resetToken)
    {
        // This is your "Dummy" email. It just prints to the terminal.
        _logger.LogInformation("==========================================");
        _logger.LogInformation("EMAIL SIMULATION");
        _logger.LogInformation("TO: {Email}", toEmail);
        _logger.LogInformation("SUBJECT: Password Reset");
        _logger.LogInformation("TOKEN: {Token}", resetToken);
        _logger.LogInformation("LINK: http://localhost:5000/reset-password?token={Token}", resetToken);
        _logger.LogInformation("==========================================");

        return Task.CompletedTask;
    }
}