namespace CatalogService.Models;

/// <summary>
/// Отметка о том, что остатки по заказу уже списаны.
/// Нужна для идемпотентности: RabbitMQ гарантирует доставку «хотя бы один раз»,
/// поэтому одно и то же событие OrderCreated может прийти повторно.
/// </summary>
public class ProcessedOrder
{
    /// <summary>Идентификатор заказа из OrderService (первичный ключ).</summary>
    public Guid OrderId { get; set; }

    /// <summary>Когда остатки были списаны (UTC).</summary>
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}
