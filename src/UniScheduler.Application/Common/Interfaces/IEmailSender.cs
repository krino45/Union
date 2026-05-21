namespace UniScheduler.Application.Common.Interfaces;

public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string bodyHtml, CancellationToken ct = default);
}
