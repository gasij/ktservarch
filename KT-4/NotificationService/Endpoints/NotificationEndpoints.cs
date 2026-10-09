using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotificationService.Data;

namespace NotificationService.Endpoints;

/// <summary>Краткая запись журнала уведомлений для API (без HTML-тела).</summary>
/// <param name="Id">Идентификатор записи.</param>
/// <param name="ReferenceId">Идентификатор заказа.</param>
/// <param name="Type">Тип уведомления.</param>
/// <param name="UserEmail">Получатель.</param>
/// <param name="Subject">Тема письма.</param>
/// <param name="Status">Статус доставки.</param>
/// <param name="Attempts">Число попыток.</param>
/// <param name="Error">Последняя ошибка.</param>
/// <param name="CreatedAt">Когда создано (UTC).</param>
/// <param name="SentAt">Когда отправлено (UTC).</param>
public record NotificationLogDto(
    Guid Id,
    Guid ReferenceId,
    string Type,
    string UserEmail,
    string Subject,
    string Status,
    int Attempts,
    string? Error,
    DateTime CreatedAt,
    DateTime? SentAt);

/// <summary>
/// HTTP-эндпоинты NotificationService. Основная работа сервиса — асинхронная (RabbitMQ),
/// API нужен только для просмотра журнала отправок.
/// </summary>
public static class NotificationEndpoints
{
    /// <summary>Подключить эндпоинты группы <c>/api/notifications</c>.</summary>
    /// <param name="app">Приложение.</param>
    /// <returns>То же приложение для цепочки вызовов.</returns>
    public static WebApplication MapNotificationEndpoints(this WebApplication app)
    {
        app.MapGet("/api/notifications", GetNotifications)
            .WithTags("Notifications")
            .WithName("GetNotifications")
            .WithSummary("Журнал уведомлений (фильтры: email, orderId; take ≤ 100)")
            .Produces<List<NotificationLogDto>>();

        return app;
    }

    /// <summary>GET /api/notifications — последние записи журнала.</summary>
    /// <param name="db">Контекст БД уведомлений.</param>
    /// <param name="email">Фильтр по получателю.</param>
    /// <param name="orderId">Фильтр по заказу.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <param name="take">Сколько записей вернуть (1–100).</param>
    private static async Task<IResult> GetNotifications(
        NotificationDbContext db,
        [FromQuery] string? email,
        [FromQuery] Guid? orderId,
        CancellationToken ct,
        [FromQuery] int take = 20)
    {
        var query = db.NotificationLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(email))
            query = query.Where(n => n.UserEmail == email);
        if (orderId.HasValue)
            query = query.Where(n => n.ReferenceId == orderId.Value);

        var logs = await query
            .OrderByDescending(n => n.CreatedAt)
            .Take(Math.Clamp(take, 1, 100))
            .ToListAsync(ct);

        return Results.Ok(logs.Select(n => new NotificationLogDto(
            n.Id, n.ReferenceId, n.Type, n.UserEmail, n.Subject, n.Status.ToString(),
            n.Attempts, n.Error, n.CreatedAt, n.SentAt)).ToList());
    }
}
