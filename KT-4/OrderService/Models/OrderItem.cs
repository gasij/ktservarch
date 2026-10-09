namespace OrderService.Models;

/// <summary>
/// Позиция заказа — СНИМОК товара на момент покупки.
/// Хранит productId как ссылку, но name и price — копии.
/// Это гарантирует: даже если товар изменится, заказ остаётся верным.
/// </summary>
public class OrderItem
{
    /// <summary>Идентификатор позиции.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Идентификатор заказа (FK внутри БД заказов).</summary>
    public Guid OrderId { get; set; }

    /// <summary>ID товара в CatalogService (не FK — другая БД).</summary>
    public Guid ProductId { get; set; }

    /// <summary>Название на момент заказа.</summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>Цена на момент заказа. Не изменится, если CatalogService обновит цену.</summary>
    public decimal PriceAtPurchase { get; set; }

    /// <summary>Количество единиц.</summary>
    public int Quantity { get; set; }

    /// <summary>Навигационное свойство к заказу.</summary>
    public Order Order { get; set; } = null!;
}
