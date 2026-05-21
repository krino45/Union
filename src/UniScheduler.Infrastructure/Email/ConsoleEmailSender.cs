using Microsoft.Extensions.Logging;
using UniScheduler.Application.Common.Interfaces;

namespace UniScheduler.Infrastructure.Email;

// Dev stub.
// Replace with a real SMTP/Mailgun/SendGrid implementation later.
public class ConsoleEmailSender : IEmailSender
{
    private readonly ILogger<ConsoleEmailSender> _logger;
    public ConsoleEmailSender(ILogger<ConsoleEmailSender> logger) => _logger = logger;

    public Task SendAsync(string toEmail, string subject, string bodyHtml, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "============ EMAIL (stub) ============\nTo: {To}\nSubject: {Subject}\n\n{Body}\n========================",
            toEmail, subject, bodyHtml);
        return Task.CompletedTask;
    }
}
