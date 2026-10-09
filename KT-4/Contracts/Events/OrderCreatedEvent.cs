using MassTransit;

namespace Contracts.Events;

/// <summary>
/// Интеграционное событие «Заказ создан».
/// Публикует OrderService, потребляют NotificationService (письмо покупателю)
/// и CatalogService (списание остатков).
/// </summary>
/// <remarks>
/// Контракт события — это договор между сервисами. Поля нельзя удалять или переименовывать
/// без версионирования: можно только добавлять новые необязательные поля.
/// Имя exchange в RabbitMQ фиксировано атрибутом, чтобы не зависеть от namespace класса.
/// </remarks>
[EntityName("shop.order.created")]
public record OrderCreatedEvent
{
    /// <summary>Идентификатор созданного заказа.</summary>
    public Guid OrderId { get; init; }

    /// <summary>Идентификатор покупателя (из IdentityService).</summary>
    public Guid UserId { get; init; }

    /// <summary>Email покупателя на момент заказа — адрес для уведомления.</summary>
    public string UserEmail { get; init; } = string.Empty;

    /// <summary>Итоговая сумма заказа в рублях.</summary>
    public decimal TotalAmount { get; init; }

    /// <summary>Позиции заказа (снимок товаров на момент покупки).</summary>
    public List<OrderItemEvent> Items { get; init; } = [];

    /// <summary>Время создания заказа (UTC).</summary>
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// Позиция заказа внутри события <see cref="OrderCreatedEvent"/>.
/// </summary>
public record OrderItemEvent
{
    /// <summary>Идентификатор товара в CatalogService.</summary>
    public Guid ProductId { get; init; }

    /// <summary>Название товара на момент покупки.</summary>
    public string ProductName { get; init; } = string.Empty;

    /// <summary>Количество единиц.</summary>
    public int Quantity { get; init; }

    /// <summary>Цена за единицу на момент покупки.</summary>
    public decimal Price { get; init; }
}
