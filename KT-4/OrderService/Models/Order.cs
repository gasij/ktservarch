namespace OrderService.Models;

/// <summary>
/// Заказ покупателя.
/// Содержит снимок (snapshot) данных на момент оформления.
/// ID пользователя — ссылка на IdentityService, не внешний ключ.
/// </summary>
public class Order
{
    /// <summary>Идентификатор заказа.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>ID пользователя из IdentityService (не FK — своя БД).</summary>
    public Guid UserId { get; set; }

    /// <summary>Snapshot email на момент заказа — не зависим от IdentityService.</summary>
    public string UserEmail { get; set; } = string.Empty;

    /// <summary>Текущий статус заказа.</summary>
    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    /// <summary>Итоговая сумма: сумма (цена × количество) по всем позициям.</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>Комментарий покупателя.</summary>
    public string? Comment { get; set; }

    /// <summary>Snapshot адреса доставки: улица.</summary>
    public string ShippingStreet { get; set; } = string.Empty;

    /// <summary>Snapshot адреса доставки: город.</summary>
    public string ShippingCity { get; set; } = string.Empty;

    /// <summary>Snapshot адреса доставки: индекс.</summary>
    public string ShippingZipCode { get; set; } = string.Empty;

    /// <summary>Дата создания (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Дата последнего изменения (UTC).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Позиции заказа.</summary>
    public List<OrderItem> Items { get; set; } = [];
}

/// <summary>Статусы жизненного цикла заказа.</summary>
public enum OrderStatus
{
    /// <summary>Создан, ожидает оплаты.</summary>
    Pending,

    /// <summary>Оплачен.</summary>
    Paid,

    /// <summary>Отправлен.</summary>
    Shipped,

    /// <summary>Доставлен.</summary>
    Delivered,

    /// <summary>Отменён.</summary>
    Cancelled
}
