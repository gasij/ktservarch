using Contracts.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using OrderService.Data;
using OrderService.DTOs;
using OrderService.Exceptions;
using OrderService.Models;

namespace OrderService.Services;

/// <summary>
/// Сервис управления заказами.
/// Оркестрирует: проверку товаров в CatalogService (HTTP), сохранение снимка заказа
/// и публикацию события OrderCreated (RabbitMQ через transactional outbox).
/// </summary>
/// <remarks>
/// Класс назван OrderManagementService, чтобы не конфликтовать с корневым namespace OrderService.
/// </remarks>
/// <param name="db">Контекст БД заказов.</param>
/// <param name="catalogClient">Клиент CatalogService.</param>
/// <param name="publishEndpoint">Публикация событий. С включённым bus outbox пишет в таблицу OutboxMessage.</param>
/// <param name="logger">Логгер.</param>
public class OrderManagementService(
    OrderDbContext db,
    ICatalogServiceClient catalogClient,
    IPublishEndpoint publishEndpoint,
    ILogger<OrderManagementService> logger)
{
    /// <summary>
    /// Создать новый заказ.
    /// Шаги:
    ///   1. Проверить наличие каждого товара в CatalogService (HTTP)
    ///   2. Создать заказ со снимком названия и цены
    ///   3. Добавить событие OrderCreated в outbox
    ///   4. Сохранить заказ и событие одной транзакцией
    /// </summary>
    /// <param name="dto">Данные заказа (уже провалидированы).</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Созданный заказ.</returns>
    /// <exception cref="ProductNotFoundException">Товара нет в каталоге (→ 422).</exception>
    /// <exception cref="OutOfStockException">Товара недостаточно (→ 409).</exception>
    /// <exception cref="CatalogUnavailableException">CatalogService недоступен (→ 503).</exception>
    public async Task<OrderDto> CreateAsync(CreateOrderDto dto, CancellationToken ct = default)
    {
        logger.LogInformation("Создание заказа для пользователя {UserId}, позиций: {ItemCount}",
            dto.UserId, dto.Items.Count);

        // Одинаковые товары в запросе объединяем: проверяем и храним суммарное количество.
        var requested = dto.Items
            .GroupBy(i => i.ProductId)
            .Select(g => (ProductId: g.Key, Quantity: g.Sum(i => i.Quantity)))
            .ToList();

        // Шаг 1–2: проверяем наличие и делаем снимок товара
        var orderItems = new List<OrderItem>(requested.Count);
        foreach (var (productId, quantity) in requested)
        {
            var availability = await catalogClient.CheckAvailabilityAsync(productId, quantity, ct)
                ?? throw new ProductNotFoundException(productId);

            if (!availability.InStock)
                throw new OutOfStockException(productId, availability.ProductName, quantity);

            orderItems.Add(new OrderItem
            {
                ProductId = productId,
                ProductName = availability.ProductName,      // snapshot!
                PriceAtPurchase = availability.CurrentPrice, // snapshot!
                Quantity = quantity
            });
        }

        var order = new Order
        {
            UserId = dto.UserId,
            UserEmail = dto.UserEmail.Trim(),
            TotalAmount = orderItems.Sum(i => i.PriceAtPurchase * i.Quantity),
            Comment = dto.Comment,
            ShippingStreet = dto.ShippingAddress.Street.Trim(),
            ShippingCity = dto.ShippingAddress.City.Trim(),
            ShippingZipCode = dto.ShippingAddress.ZipCode,
            Items = orderItems
        };

        db.Orders.Add(order);

        // Шаг 3: событие попадает в таблицу OutboxMessage того же DbContext, в RabbitMQ его
        // отправит фоновый процесс MassTransit только после успешного коммита.
        await publishEndpoint.Publish(new OrderCreatedEvent
        {
            OrderId = order.Id,
            UserId = order.UserId,
            UserEmail = order.UserEmail,
            TotalAmount = order.TotalAmount,
            Items = orderItems.Select(i => new OrderItemEvent
            {
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                Quantity = i.Quantity,
                Price = i.PriceAtPurchase
            }).ToList(),
            CreatedAt = order.CreatedAt
        }, ct);

        // Шаг 4: одна транзакция — и заказ, и событие
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Заказ {OrderId} создан, сумма {Total}. Событие OrderCreated записано в outbox",
            order.Id, order.TotalAmount);

        return ToDto(order);
    }

    /// <summary>История заказов пользователя, новые сверху.</summary>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Список заказов (может быть пустым).</returns>
    public async Task<List<OrderDto>> GetUserOrdersAsync(Guid userId, CancellationToken ct = default)
    {
        var orders = await db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(ct);

        return orders.Select(ToDto).ToList();
    }

    /// <summary>Получить заказ по идентификатору.</summary>
    /// <param name="orderId">Идентификатор заказа.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Заказ или null.</returns>
    public async Task<OrderDto?> GetByIdAsync(Guid orderId, CancellationToken ct = default)
    {
        var order = await db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);

        return order is null ? null : ToDto(order);
    }

    /// <summary>Преобразовать заказ в DTO.</summary>
    /// <param name="o">Сущность заказа с загруженными позициями.</param>
    private static OrderDto ToDto(Order o) => new(
        o.Id, o.UserId, o.UserEmail, o.Status.ToString(), o.TotalAmount, o.Comment,
        new ShippingAddressDto(o.ShippingStreet, o.ShippingCity, o.ShippingZipCode),
        o.CreatedAt,
        o.Items.Select(i => new OrderItemDto(i.ProductId, i.ProductName, i.PriceAtPurchase, i.Quantity)).ToList());
}
