using System.Net.Mail;

namespace Api.Options;

public sealed class RegistrationNotificationOptions
{
    public const string SectionName = "RegistrationNotifications";

    public bool Enabled { get; set; }
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 465;
    public string SmtpUsername { get; set; } = string.Empty;
    public string SmtpPassword { get; set; } = string.Empty;
    public string SenderAddress { get; set; } = string.Empty;
    public string RecipientAddress { get; set; } = string.Empty;

    public bool Validate(out List<string> errors)
    {
        errors = [];
        if (!Enabled)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(SmtpHost))
        {
            errors.Add("SmtpHost is required when registration notifications are enabled.");
        }

        if (SmtpPort is < 1 or > 65535)
        {
            errors.Add("SmtpPort must be between 1 and 65535.");
        }

        if (string.IsNullOrWhiteSpace(SmtpUsername))
        {
            errors.Add("SmtpUsername is required when registration notifications are enabled.");
        }

        if (string.IsNullOrWhiteSpace(SmtpPassword))
        {
            errors.Add("SmtpPassword is required when registration notifications are enabled.");
        }

        if (!IsValidEmail(SenderAddress))
        {
            errors.Add("SenderAddress must be a valid email address.");
        }

        if (!IsValidEmail(RecipientAddress))
        {
            errors.Add("RecipientAddress must be a valid email address.");
        }

        return errors.Count == 0;
    }

    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        try
        {
            var address = new MailAddress(email.Trim());
            return address.Address == email.Trim();
        }
        catch
        {
            return false;
        }
    }
}
