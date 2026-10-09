using Microsoft.Extensions.Http.Resilience;
using OrderService.Services;
using Polly;

namespace OrderService.Infrastructure;

/// <summary>
/// Регистрация HTTP-клиента CatalogService с политиками устойчивости (Polly v8).
/// </summary>
public static class CatalogClientRegistration
{
    /// <summary>Имя именованного клиента для health check (без retry/circuit breaker).</summary>
    public const string HealthClientName = "catalog-health";

    /// <summary>
    /// Зарегистрировать <see cref="ICatalogServiceClient"/> с пайплайном:
    /// общий таймаут 10 с → retry (3 раза, экспоненциально, с jitter) → circuit breaker → таймаут попытки 2 с.
    /// </summary>
    /// <remarks>
    /// Порядок важен: circuit breaker стоит внутри retry, поэтому каждая неудачная попытка
    /// учитывается в статистике. Когда цепь открыта, запросы к каталогу вообще не отправляются,
    /// а OrderService сразу отвечает 503 — упавший сервис не добивается повторами.
    /// </remarks>
    /// <param name="services">Коллекция сервисов.</param>
    /// <param name="baseUrl">Базовый адрес CatalogService, например http://catalog-service:8080/.</param>
    /// <returns>Коллекция сервисов для цепочки вызовов.</returns>
    public static IServiceCollection AddCatalogServiceClient(this IServiceCollection services, Uri baseUrl)
    {
        services.AddHttpClient<ICatalogServiceClient, CatalogServiceClient>(client => client.BaseAddress = baseUrl)
            .AddResilienceHandler("catalog", (pipeline, context) =>
            {
                var logger = context.ServiceProvider
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("CatalogResilience");

                // 1. Общий таймаут на весь вызов вместе с повторами
                pipeline.AddTimeout(TimeSpan.FromSeconds(10));

                // 2. Retry: 5xx, 408, 429, сетевые ошибки и таймауты попытки. 404 не повторяется.
                pipeline.AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = 3,
                    BackoffType = DelayBackoffType.Exponential,
                    Delay = TimeSpan.FromMilliseconds(200), // ~200, 400, 800 мс
                    UseJitter = true,
                    OnRetry = args =>
                    {
                        logger.LogWarning("CatalogService retry #{Attempt} через {Delay} мс: {Reason}",
                            args.AttemptNumber + 1,
                            (int)args.RetryDelay.TotalMilliseconds,
                            args.Outcome.Exception?.Message ?? $"HTTP {(int?)args.Outcome.Result?.StatusCode}");
                        return default;
                    }
                });

                // 3. Circuit breaker: ≥50 % ошибок среди ≥5 вызовов за 30 с → размыкаем на 30 с
                pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                {
                    FailureRatio = 0.5,
                    MinimumThroughput = 5,
                    SamplingDuration = TimeSpan.FromSeconds(30),
                    BreakDuration = TimeSpan.FromSeconds(30),
                    OnOpened = args =>
                    {
                        logger.LogError("⚡ Circuit breaker ОТКРЫТ на {Seconds} с — CatalogService недоступен",
                            args.BreakDuration.TotalSeconds);
                        return default;
                    },
                    OnHalfOpened = _ =>
                    {
                        logger.LogWarning("Circuit breaker ПОЛУОТКРЫТ — пробный запрос к CatalogService");
                        return default;
                    },
                    OnClosed = _ =>
                    {
                        logger.LogInformation("✅ Circuit breaker ЗАКРЫТ — CatalogService снова доступен");
                        return default;
                    }
                });

                // 4. Таймаут одной попытки
                pipeline.AddTimeout(TimeSpan.FromSeconds(2));
            });

        // Отдельный «голый» клиент для health check: его проверки не должны влиять на circuit breaker.
        services.AddHttpClient(HealthClientName, client =>
        {
            client.BaseAddress = baseUrl;
            client.Timeout = TimeSpan.FromSeconds(2);
        });

        return services;
    }
}
