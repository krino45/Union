using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using UniScheduler.Application.Common.Interfaces;

namespace UniScheduler.Infrastructure.Email;

public class ResendEmailSender : IEmailSender
{
    private readonly HttpClient _http;
    private readonly EmailSettings _settings;
    private readonly ILogger<ResendEmailSender> _logger;

    public ResendEmailSender(HttpClient http, EmailSettings settings, ILogger<ResendEmailSender> logger)
    {
        _http = http;
        _settings = settings;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string subject, string bodyHtml, CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails")
        {
            Content = JsonContent.Create(new
            {
                from = $"{_settings.FromName} <{_settings.FromAddress}>",
                to = new[] { toEmail },
                subject,
                html = bodyHtml
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.Resend.ApiKey);

        var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Resend send failed ({Status}) to {To}: {Error}", response.StatusCode, toEmail, error);
            throw new InvalidOperationException($"Resend returned {(int)response.StatusCode}: {error}");
        }

        _logger.LogInformation("Email sent via Resend to {To} (subject: {Subject})", toEmail, subject);
    }
}
