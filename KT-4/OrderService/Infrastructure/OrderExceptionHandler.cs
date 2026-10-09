using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using OrderService.Exceptions;

namespace OrderService.Infrastructure;

/// <summary>
/// Глобальный обработчик исключений: переводит доменные ошибки в осмысленные HTTP-коды
/// в формате ProblemDetails (RFC 9457). Непредвиденные исключения обрабатывает стандартный
/// механизм (500 без деталей реализации).
/// </summary>
/// <param name="problemDetails">Сервис записи ProblemDetails в ответ.</param>
/// <param name="logger">Логгер.</param>
public class OrderExceptionHandler(IProblemDetailsService problemDetails, ILogger<OrderExceptionHandler> logger)
    : IExceptionHandler
{
    /// <summary>Сколько секунд клиенту подождать перед повтором при 503 (совпадает с BreakDuration).</summary>
    private const int RetryAfterSeconds = 30;

    /// <summary>Попробовать обработать исключение.</summary>
    /// <param name="httpContext">Текущий HTTP-контекст.</param>
    /// <param name="exception">Исключение.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>true — ответ сформирован; false — передать стандартному обработчику.</returns>
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            ProductNotFoundException => (StatusCodes.Status422UnprocessableEntity, "Товар не найден"),
            OutOfStockException => (StatusCodes.Status409Conflict, "Недостаточно товара на складе"),
            CatalogUnavailableException => (StatusCodes.Status503ServiceUnavailable, "Каталог временно недоступен"),
            _ => (0, string.Empty)
        };

        if (status == 0)
            return false;

        logger.LogWarning("Запрос {Method} {Path} завершён с кодом {Status}: {Message}",
            httpContext.Request.Method, httpContext.Request.Path, status, exception.Message);

        if (status == StatusCodes.Status503ServiceUnavailable)
            httpContext.Response.Headers.RetryAfter = RetryAfterSeconds.ToString();

        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = exception.Message
            }
        });
    }
}
