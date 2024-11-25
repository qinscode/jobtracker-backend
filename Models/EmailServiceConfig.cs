namespace JobTracker.Models;

public class EmailServiceConfig
{
    public GmailConfig Gmail { get; set; } = new();
}

public class GmailConfig
{
    public string ImapServer { get; set; } = "imap.gmail.com";
    public int ImapPort { get; set; } = 993;
    public bool UseSsl { get; set; } = true;
    public int BatchSize { get; set; } = 100;
    public int MaxConcurrentScans { get; set; } = 5;
    public int RetryAttempts { get; set; } = 3;
    public int RetryDelaySeconds { get; set; } = 5;
}