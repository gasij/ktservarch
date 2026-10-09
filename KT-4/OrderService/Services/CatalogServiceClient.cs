using System.Net;
using OrderService.Exceptions;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace OrderService.Services;

/// <summary>Данные о доступности товара, которые возвращает CatalogService.</summary>
/// <param name="ProductId">Идентификатор товара.</param>
/// <param name="ProductName">Текущее название товара.</param>
/// <param name="RequestedQuantity">Запрошенное количество.</param>
/// <param name="InStock">Хватает ли остатка.</param>
/// <param name="CurrentPrice">Текущая цена за единицу.</param>
public record ProductAvailability(
    Guid ProductId,
    string ProductName,
    int RequestedQuantity,
    bool InStock,
    decimal CurrentPrice);

/// <summary>
/// Клиент CatalogService. OrderService зависит от интерфейса, а не от HTTP-реализации
/// (Dependency Inversion) — в тестах её легко подменить.
/// </summary>
public interface ICatalogServiceClient
{
    /// <summary>Проверить наличие товара и получить его текущие цену и название.</summary>
    /// <param name="productId">Идентификатор товара.</param>
    /// <param name="quantity">Требуемое количество.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Данные о доступности или null, если товара нет в каталоге (HTTP 404).</returns>
    /// <exception cref="CatalogUnavailableException">CatalogService недоступен.</exception>
    Task<ProductAvailability?> CheckAvailabilityAsync(Guid productId, int quantity, CancellationToken ct = default);
}

/// <summary>
/// HTTP-реализация клиента CatalogService.
/// Retry, circuit breaker и таймауты настраиваются на HttpClient в Program.cs
/// (Microsoft.Extensions.Http.Resilience, Polly v8). Этот класс только переводит
/// технические ошибки в понятное доменное исключение <see cref="CatalogUnavailableException"/>.
/// </summary>
/// <param name="httpClient">HttpClient с настроенным BaseAddress и resilience-пайплайном.</param>
/// <param name="logger">Логгер.</param>
public class CatalogServiceClient(HttpClient httpClient, ILogger<CatalogServiceClient> logger) : ICatalogServiceClient
{
    /// <inheritdoc />
    public async Task<ProductAvailability?> CheckAvailabilityAsync(
        Guid productId, int quantity, CancellationToken ct = default)
    {
        logger.LogInformation("→ CatalogService: проверка наличия {ProductId} × {Quantity}", productId, quantity);

        try
        {
            using var response = await httpClient.GetAsync(
                $"api/products/{productId}/availability?quantity={quantity}", ct);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ProductAvailability>(ct);
            logger.LogInformation("← CatalogService: {ProductName}, цена {Price}, в наличии: {InStock}",
                result?.ProductName, result?.CurrentPrice, result?.InStock);
            return result;
        }
        catch (BrokenCircuitException ex)
        {
            // Цепь разомкнута: не нагружаем упавший сервис, сразу отвечаем клиенту.
            logger.LogWarning("CatalogService: circuit breaker открыт, запрос не отправлялся");
            throw new CatalogUnavailableException("circuit breaker открыт, повторите позже", ex);
        }
        catch (TimeoutRejectedException ex)
        {
            logger.LogWarning("CatalogService: превышено время ожидания ответа");
            throw new CatalogUnavailableException("превышено время ожидания ответа", ex);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning("CatalogService: ошибка после всех повторов — {Error}", ex.Message);
            throw new CatalogUnavailableException("сервис не отвечает", ex);
        }
    }
}
