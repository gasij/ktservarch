using Microsoft.Extensions.Diagnostics.HealthChecks;
using OrderService.Infrastructure;

namespace OrderService.HealthChecks;

/// <summary>
/// Проверка доступности CatalogService (его /health/live).
/// </summary>
/// <remarks>
/// Регистрируется со статусом отказа Degraded, а не Unhealthy: без каталога нельзя создать
/// новый заказ, но история заказов продолжает работать. Поэтому OrderService в этом случае
/// «деградирован», но не «мёртв», и оркестратор не должен его перезапускать.
/// </remarks>
/// <param name="httpClientFactory">Фабрика HttpClient.</param>
public class CatalogServiceHealthCheck(IHttpClientFactory httpClientFactory) : IHealthCheck
{
    /// <summary>Выполнить проверку.</summary>
    /// <param name="context">Контекст проверки (содержит статус отказа из регистрации).</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Healthy, если CatalogService ответил 2xx, иначе статус отказа из регистрации.</returns>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient(CatalogClientRegistration.HealthClientName);
            using var response = await client.GetAsync("health/live", cancellationToken);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("CatalogService доступен")
                : new HealthCheckResult(context.Registration.FailureStatus,
                    $"CatalogService ответил {(int)response.StatusCode}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return new HealthCheckResult(context.Registration.FailureStatus,
                "CatalogService недоступен", ex);
        }
    }
}
