using Microsoft.AspNetCore.Mvc;
using OrderService.DTOs;
using OrderService.Services;

namespace OrderService.Endpoints;

/// <summary>
/// HTTP-эндпоинты OrderService (Minimal API).
/// Ошибки бизнес-логики (422, 409, 503) формирует <see cref="Infrastructure.OrderExceptionHandler"/>.
/// </summary>
public static class OrderEndpoints
{
    /// <summary>Подключить эндпоинты группы <c>/api/orders</c>.</summary>
    /// <param name="app">Приложение.</param>
    /// <returns>То же приложение для цепочки вызовов.</returns>
    public static WebApplication MapOrderEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orders").WithTags("Orders");

        group.MapPost("/", CreateOrder)
            .WithName("CreateOrder")
            .WithSummary("Создать заказ")
            .WithDescription("Проверяет каждый товар в CatalogService (HTTP), сохраняет снимок цен и публикует OrderCreated в RabbitMQ.")
            .Produces<OrderDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
            .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)
            .Produces<ProblemDetails>(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("/{id:guid}", GetOrder)
            .WithName("GetOrderById")
            .WithSummary("Получить заказ по ID")
            .Produces<OrderDto>()
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapGet("/", GetUserOrders)
            .WithName("GetUserOrders")
            .WithSummary("История заказов пользователя (?userId=...)")
            .Produces<List<OrderDto>>()
            .ProducesValidationProblem();

        return app;
    }

    /// <summary>POST /api/orders — создание заказа. 201 + Location.</summary>
    /// <param name="dto">Данные заказа.</param>
    /// <param name="orderService">Сервис заказов.</param>
    /// <param name="ct">Токен отмены.</param>
    private static async Task<IResult> CreateOrder(
        CreateOrderDto dto, OrderManagementService orderService, CancellationToken ct)
    {
        // Проверки, которые не выражаются атрибутами DataAnnotations
        var errors = new Dictionary<string, string[]>();
        if (dto.UserId == Guid.Empty)
            errors["userId"] = ["UserId обязателен"];
        if (dto.Items.Any(i => i.ProductId == Guid.Empty))
            errors["items"] = ["ProductId обязателен для каждой позиции"];
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var order = await orderService.CreateAsync(dto, ct);
        return Results.Created($"/api/orders/{order.Id}", order);
    }

    /// <summary>GET /api/orders/{id} — заказ по идентификатору.</summary>
    /// <param name="id">Идентификатор заказа.</param>
    /// <param name="orderService">Сервис заказов.</param>
    /// <param name="ct">Токен отмены.</param>
    private static async Task<IResult> GetOrder(Guid id, OrderManagementService orderService, CancellationToken ct)
    {
        var order = await orderService.GetByIdAsync(id, ct);
        return order is null
            ? Results.Problem(title: "Заказ не найден", detail: $"Заказ {id} не найден",
                statusCode: StatusCodes.Status404NotFound)
            : Results.Ok(order);
    }

    /// <summary>GET /api/orders?userId=... — история заказов пользователя.</summary>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <param name="orderService">Сервис заказов.</param>
    /// <param name="ct">Токен отмены.</param>
    private static async Task<IResult> GetUserOrders(
        [FromQuery] Guid? userId, OrderManagementService orderService, CancellationToken ct)
    {
        if (userId is null || userId == Guid.Empty)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["userId"] = ["Параметр userId обязателен"]
            });

        return Results.Ok(await orderService.GetUserOrdersAsync(userId.Value, ct));
    }
}
