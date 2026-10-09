using System.Globalization;
using System.Net;
using Contracts.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using NotificationService.Data;
using NotificationService.Models;
using NotificationService.Services;

namespace NotificationService.Consumers;

/// <summary>
/// Обработчик события OrderCreated: отправляет покупателю письмо о заказе и пишет журнал.
/// MassTransit автоматически создаёт очередь notification-order-created и привязывает её
/// к exchange shop.order.created.
/// </summary>
/// <param name="emailService">Отправка email.</param>
/// <param name="db">Контекст БД уведомлений.</param>
/// <param name="logger">Логгер.</param>
public class OrderCreatedConsumer(
    IEmailService emailService,
    NotificationDbContext db,
    ILogger<OrderCreatedConsumer> logger) : IConsumer<OrderCreatedEvent>
{
    /// <summary>Тип уведомления в журнале.</summary>
    public const string NotificationType = "ORDER_CREATED";

    /// <summary>Культура для форматирования сумм в письме.</summary>
    private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

    /// <summary>
    /// Обработать событие OrderCreated:
    /// 1. Пропустить, если письмо по этому заказу уже отправлено (идемпотентность)
    /// 2. Сформировать HTML-письмо и отправить покупателю
    /// 3. Записать результат в журнал уведомлений
    /// </summary>
    /// <remarks>
    /// При ошибке SMTP статус FAILED сохраняется, а исключение пробрасывается дальше: MassTransit
    /// повторит доставку (UseMessageRetry), а после всех попыток переложит сообщение в очередь
    /// notification-order-created_error (dead letter). Так письмо не теряется молча.
    /// </remarks>
    /// <param name="context">Контекст сообщения MassTransit.</param>
    public async Task Consume(ConsumeContext<OrderCreatedEvent> context)
    {
        var evt = context.Message;
        var ct = context.CancellationToken;

        logger.LogInformation(
            "Получено событие OrderCreated: заказ {OrderId} от {UserEmail}, сумма {Total}",
            evt.OrderId, evt.UserEmail, evt.TotalAmount);

        var log = await db.NotificationLogs
            .FirstOrDefaultAsync(n => n.ReferenceId == evt.OrderId && n.Type == NotificationType, ct);

        if (log?.Status == NotificationStatus.Sent)
        {
            logger.LogInformation("Письмо по заказу {OrderId} уже отправлено — повторное событие пропущено", evt.OrderId);
            return;
        }

        if (log is null)
        {
            var shortId = evt.OrderId.ToString()[..8].ToUpperInvariant();
            log = new NotificationLog
            {
                UserId = evt.UserId,
                UserEmail = evt.UserEmail,
                ReferenceId = evt.OrderId,
                Type = NotificationType,
                Subject = $"✅ Заказ #{shortId} успешно оформлен",
                Body = BuildEmailBody(evt, shortId)
            };
            db.NotificationLogs.Add(log);
        }

        log.Attempts++;

        try
        {
            await emailService.SendAsync(evt.UserEmail, log.Subject, log.Body, ct);

            log.Status = NotificationStatus.Sent;
            log.SentAt = DateTime.UtcNow;
            log.Error = null;
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.Status = NotificationStatus.Failed;
            log.Error = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
            await db.SaveChangesAsync(ct);

            logger.LogError(ex, "Ошибка отправки email для заказа {OrderId} (попытка {Attempt})",
                evt.OrderId, log.Attempts);
            throw;
        }
    }

    /// <summary>Построить HTML-тело письма с деталями заказа. Пользовательские строки экранируются.</summary>
    /// <param name="evt">Событие заказа.</param>
    /// <param name="shortId">Короткий номер заказа для отображения.</param>
    /// <returns>HTML-строка.</returns>
    private static string BuildEmailBody(OrderCreatedEvent evt, string shortId)
    {
        var rows = string.Concat(evt.Items.Select(i =>
            $"<tr><td>{WebUtility.HtmlEncode(i.ProductName)}</td>" +
            $"<td>{i.Quantity} шт.</td>" +
            $"<td>{Money(i.Price)}</td>" +
            $"<td>{Money(i.Price * i.Quantity)}</td></tr>"));

        return $"""
            <html><body style="font-family: Arial, sans-serif">
              <h2>Спасибо за заказ!</h2>
              <p>Ваш заказ <strong>#{shortId}</strong> успешно оформлен.</p>
              <table border="1" cellpadding="8" cellspacing="0">
                <thead><tr><th>Товар</th><th>Кол-во</th><th>Цена</th><th>Сумма</th></tr></thead>
                <tbody>{rows}</tbody>
              </table>
              <p><strong>Итого: {Money(evt.TotalAmount)}</strong></p>
              <p>Мы свяжемся с вами в ближайшее время.</p>
            </body></html>
            """;
    }

    /// <summary>Форматировать сумму в рублях: «89 990,00 ₽».</summary>
    /// <param name="amount">Сумма.</param>
    private static string Money(decimal amount) => amount.ToString("N2", Ru) + " ₽";
}
