using CatalogService.Services;
using Contracts.Events;
using MassTransit;

namespace CatalogService.Consumers;

/// <summary>
/// Обработчик события OrderCreated на стороне каталога: списывает остатки заказанных товаров.
/// </summary>
/// <remarks>
/// Очередь <c>catalog-order-created</c> — своя у каталога. NotificationService слушает то же событие
/// через свою очередь, поэтому каждое событие получают оба сервиса (fan-out), а не один из них.
/// </remarks>
/// <param name="productService">Бизнес-логика каталога.</param>
/// <param name="logger">Логгер.</param>
public class OrderCreatedConsumer(ProductService productService, ILogger<OrderCreatedConsumer> logger)
    : IConsumer<OrderCreatedEvent>
{
    /// <summary>Списать остатки по позициям заказа. Исключения приводят к повторной доставке (retry).</summary>
    /// <param name="context">Контекст сообщения MassTransit.</param>
    public async Task Consume(ConsumeContext<OrderCreatedEvent> context)
    {
        var evt = context.Message;
        logger.LogInformation("Получено событие OrderCreated: заказ {OrderId}, позиций {Count}",
            evt.OrderId, evt.Items.Count);

        var items = evt.Items
            .GroupBy(i => i.ProductId)
            .Select(g => (ProductId: g.Key, Quantity: g.Sum(i => i.Quantity)))
            .ToList();

        await productService.DeductStockAsync(evt.OrderId, items, context.CancellationToken);
    }
}
