using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace NotificationService.Services;

/// <summary>Отправка email. Реализация выбирается конфигурацией Email:Provider (Smtp или Fake).</summary>
public interface IEmailService
{
    /// <summary>Отправить HTML-письмо.</summary>
    /// <param name="to">Адрес получателя.</param>
    /// <param name="subject">Тема.</param>
    /// <param name="htmlBody">HTML-тело.</param>
    /// <param name="ct">Токен отмены.</param>
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
}

/// <summary>Настройки SMTP (секция Smtp в конфигурации).</summary>
public class SmtpOptions
{
    /// <summary>Имя секции конфигурации.</summary>
    public const string SectionName = "Smtp";

    /// <summary>Хост SMTP-сервера. В docker-compose — Mailpit.</summary>
    public string Host { get; set; } = "localhost";

    /// <summary>Порт SMTP-сервера.</summary>
    public int Port { get; set; } = 1025;

    /// <summary>Адрес отправителя.</summary>
    public string From { get; set; } = "noreply@shophub.local";

    /// <summary>Имя пользователя. Пусто — без аутентификации (Mailpit).</summary>
    public string? Username { get; set; }

    /// <summary>Пароль.</summary>
    public string? Password { get; set; }

    /// <summary>Использовать STARTTLS (true для реальных почтовых серверов).</summary>
    public bool UseStartTls { get; set; }
}

/// <summary>
/// Сервис отправки email через SMTP (MailKit).
/// В docker-compose письма принимает Mailpit — их видно в веб-интерфейсе http://localhost:8025.
/// </summary>
/// <param name="options">Настройки SMTP.</param>
/// <param name="logger">Логгер.</param>
public class EmailService(IOptions<SmtpOptions> options, ILogger<EmailService> logger) : IEmailService
{
    /// <inheritdoc />
    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        var smtp = options.Value;

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("ShopHub", smtp.From));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(
            smtp.Host,
            smtp.Port,
            smtp.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None,
            ct);

        if (!string.IsNullOrEmpty(smtp.Username))
            await client.AuthenticateAsync(smtp.Username, smtp.Password ?? string.Empty, ct);

        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);

        logger.LogInformation("📧 Email отправлен на {To}: «{Subject}»", to, subject);
    }
}

/// <summary>
/// Заглушка для разработки/тестирования.
/// Не отправляет письмо — только пишет в лог.
/// Включается настройкой Email:Provider = Fake.
/// </summary>
/// <param name="logger">Логгер.</param>
public class FakeEmailService(ILogger<FakeEmailService> logger) : IEmailService
{
    /// <inheritdoc />
    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        logger.LogInformation("📧 FAKE EMAIL (не отправлен реально) To: {To} | Subject: {Subject} | Body: {BodyPreview}...",
            to, subject, htmlBody[..Math.Min(100, htmlBody.Length)]);

        return Task.CompletedTask;
    }
}
