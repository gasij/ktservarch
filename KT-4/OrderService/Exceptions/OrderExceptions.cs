namespace OrderService.Exceptions;

/// <summary>
/// Товар из заказа не найден в каталоге (удалён или не существовал).
/// Преобразуется в HTTP 422 Unprocessable Entity.
/// </summary>
/// <param name="productId">Идентификатор товара.</param>
public class ProductNotFoundException(Guid productId)
    : Exception($"Товар {productId} не найден в каталоге")
{
    /// <summary>Идентификатор ненайденного товара.</summary>
    public Guid ProductId { get; } = productId;
}

/// <summary>
/// Товара недостаточно на складе. Преобразуется в HTTP 409 Conflict.
/// </summary>
/// <param name="productId">Идентификатор товара.</param>
/// <param name="productName">Название товара.</param>
/// <param name="requested">Запрошенное количество.</param>
public class OutOfStockException(Guid productId, string productName, int requested)
    : Exception($"Товар «{productName}» отсутствует на складе в количестве {requested} шт.")
{
    /// <summary>Идентификатор товара.</summary>
    public Guid ProductId { get; } = productId;
}

/// <summary>
/// CatalogService недоступен: сетевые ошибки после всех повторов, таймаут или открытый circuit breaker.
/// Преобразуется в HTTP 503 Service Unavailable с заголовком Retry-After.
/// </summary>
/// <param name="reason">Краткая причина для клиента.</param>
/// <param name="inner">Исходное исключение.</param>
public class CatalogUnavailableException(string reason, Exception inner)
    : Exception($"Каталог временно недоступен: {reason}", inner);
