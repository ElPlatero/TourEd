using Api.Options;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Api.Services;

internal sealed class SmtpRegistrationNotificationSender(
    IOptions<RegistrationNotificationOptions> options) : IRegistrationNotificationSender
{
    private readonly RegistrationNotificationOptions _options = options.Value;

    public async Task SendAsync(
        int newRequestCount,
        int totalPendingRequestCount,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Validate(out var errors))
        {
            throw new InvalidOperationException($"Invalid registration notification configuration: {string.Join("; ", errors)}");
        }

        var message = CreateMessage(_options, newRequestCount, totalPendingRequestCount);

        using var client = new SmtpClient();
        client.Timeout = 10000; // 10 seconds

        await client.ConnectAsync(_options.SmtpHost, _options.SmtpPort, SecureSocketOptions.SslOnConnect, cancellationToken);
        await client.AuthenticateAsync(_options.SmtpUsername, _options.SmtpPassword, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }

    internal static MimeMessage CreateMessage(
        RegistrationNotificationOptions options,
        int newRequestCount,
        int totalPendingRequestCount)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("TourEd", options.SenderAddress));
        message.To.Add(MailboxAddress.Parse(options.RecipientAddress));
        message.Subject = "Neue Registrierungsanträge bei TourEd";
        message.Headers.Add("Auto-Submitted", "auto-generated");

        var newRequestsSentence = newRequestCount == 1
            ? "Bei TourEd gibt es 1 neuen Registrierungsantrag."
            : $"Bei TourEd gibt es {newRequestCount} neue Registrierungsanträge.";

        var totalPendingSentence = totalPendingRequestCount == 1
            ? "Insgesamt ist derzeit 1 Registrierungsantrag offen."
            : $"Insgesamt sind derzeit {totalPendingRequestCount} Registrierungsanträge offen.";

        var bodyText = $"{newRequestsSentence}\n{totalPendingSentence}\n\nBitte öffne TourEd.Admin, um die Anträge zu prüfen.\n";
        message.Body = new TextPart("plain")
        {
            Text = bodyText
        };
        return message;
    }
}
