namespace NotificationService.Models;

/// <summary>
/// Запись журнала уведомлений: что, кому и когда отправили и чем закончилась попытка.
/// </summary>
public class NotificationLog
{
    /// <summary>Идентификатор записи.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Идентификатор получателя (из IdentityService).</summary>
    public Guid UserId { get; set; }

    /// <summary>Email получателя.</summary>
    public string UserEmail { get; set; } = string.Empty;

    /// <summary>
    /// Идентификатор бизнес-объекта, к которому относится уведомление (для ORDER_CREATED — заказ).
    /// Вместе с <see cref="Type"/> образует уникальный ключ: одно письмо на одно событие.
    /// </summary>
    public Guid ReferenceId { get; set; }

    /// <summary>Тип уведомления, например ORDER_CREATED.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Канал доставки.</summary>
    public string Channel { get; set; } = "EMAIL";

    /// <summary>Тема письма.</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>HTML-тело письма.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>Статус доставки.</summary>
    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;

    /// <summary>Число попыток отправки.</summary>
    public int Attempts { get; set; }

    /// <summary>Текст последней ошибки (если была).</summary>
    public string? Error { get; set; }

    /// <summary>Когда запись создана (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Когда письмо успешно отправлено (UTC).</summary>
    public DateTime? SentAt { get; set; }
}

/// <summary>Статус доставки уведомления.</summary>
public enum NotificationStatus
{
    /// <summary>Создано, отправка ещё не выполнялась.</summary>
    Pending,

    /// <summary>Успешно отправлено.</summary>
    Sent,

    /// <summary>Последняя попытка отправки завершилась ошибкой.</summary>
    Failed
}
