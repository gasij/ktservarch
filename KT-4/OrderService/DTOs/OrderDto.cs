namespace OrderService.DTOs;

/// <summary>Заказ в ответах API.</summary>
/// <param name="Id">Идентификатор заказа.</param>
/// <param name="UserId">Идентификатор покупателя.</param>
/// <param name="UserEmail">Email покупателя на момент заказа.</param>
/// <param name="Status">Статус заказа (Pending, Paid, Shipped, Delivered, Cancelled).</param>
/// <param name="TotalAmount">Итоговая сумма.</param>
/// <param name="Comment">Комментарий.</param>
/// <param name="ShippingAddress">Адрес доставки (снимок).</param>
/// <param name="CreatedAt">Дата создания (UTC).</param>
/// <param name="Items">Позиции заказа.</param>
public record OrderDto(
    Guid Id,
    Guid UserId,
    string UserEmail,
    string Status,
    decimal TotalAmount,
    string? Comment,
    ShippingAddressDto ShippingAddress,
    DateTime CreatedAt,
    List<OrderItemDto> Items
);

/// <summary>Позиция заказа в ответах API.</summary>
/// <param name="ProductId">Идентификатор товара.</param>
/// <param name="ProductName">Название на момент покупки.</param>
/// <param name="PriceAtPurchase">Цена на момент покупки.</param>
/// <param name="Quantity">Количество.</param>
public record OrderItemDto(
    Guid ProductId,
    string ProductName,
    decimal PriceAtPurchase,
    int Quantity
);
