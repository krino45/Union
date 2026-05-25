namespace UniScheduler.Infrastructure.Email;

public class EmailSettings
{
    public const string SectionName = "EmailSettings";

    // "Console" (dev stub, logs to console), "Smtp", or "Resend" (real delivery).
    public string Provider { get; set; } = "Console";
    public string FromAddress { get; set; } = "noreply@uniran.online";
    public string FromName { get; set; } = "Юниран";
    public SmtpSettings Smtp { get; set; } = new();
    public ResendSettings Resend { get; set; } = new();
}

public class ResendSettings
{
    public string ApiKey { get; set; } = string.Empty;
}

public class SmtpSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    // STARTTLS on port 587 is the common case. Set false for implicit SSL (port 465).
    public bool UseStartTls { get; set; } = true;
}
