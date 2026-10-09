using CatalogService.DTOs;
using CatalogService.Services;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Endpoints;

/// <summary>
/// Регистрация всех HTTP-эндпоинтов CatalogService.
/// Используем Minimal API: меньше бойлерплейта, лучше производительность.
/// </summary>
public static class ProductEndpoints
{
    /// <summary>Максимальный размер страницы списка товаров.</summary>
    private const int MaxPageSize = 100;

    /// <summary>Подключить эндпоинты группы <c>/api/products</c>.</summary>
    /// <param name="app">Приложение.</param>
    /// <returns>То же приложение для цепочки вызовов.</returns>
    public static WebApplication MapProductEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/products").WithTags("Products");

        group.MapGet("/", GetProducts)
            .WithName("GetProducts")
            .WithSummary("Получить список товаров")
            .WithDescription("Фильтрация по категории и цене, пагинация. Пример: ?category=Смартфоны&page=1&pageSize=20")
            .Produces<PagedResult<ProductDto>>()
            .ProducesValidationProblem();

        group.MapGet("/{id:guid}", GetProduct)
            .WithName("GetProductById")
            .WithSummary("Получить товар по ID (с кэшем Redis)")
            .Produces<ProductDto>()
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/availability", CheckAvailability)
            .WithName("CheckAvailability")
            .WithSummary("Проверить наличие товара (вызывается OrderService)")
            .Produces<AvailabilityDto>()
            .ProducesValidationProblem()
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapPost("/", CreateProduct)
            .WithName("CreateProduct")
            .WithSummary("Создать новый товар")
            .Produces<ProductDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        group.MapPut("/{id:guid}", UpdateProduct)
            .WithName("UpdateProduct")
            .WithSummary("Обновить товар (меняются только переданные поля)")
            .Produces<ProductDto>()
            .ProducesValidationProblem()
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", DeleteProduct)
            .WithName("DeleteProduct")
            .WithSummary("Удалить товар (soft delete)")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        return app;
    }

    /// <summary>GET /api/products — список активных товаров.</summary>
    /// <param name="productService">Сервис каталога.</param>
    /// <param name="category">Фильтр по категории.</param>
    /// <param name="minPrice">Минимальная цена.</param>
    /// <param name="maxPrice">Максимальная цена.</param>
    /// <param name="page">Номер страницы (с 1).</param>
    /// <param name="pageSize">Размер страницы (1–100).</param>
    /// <param name="ct">Токен отмены.</param>
    private static async Task<IResult> GetProducts(
        ProductService productService,
        [FromQuery] string? category,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice,
        CancellationToken ct,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var errors = new Dictionary<string, string[]>();
        if (page < 1) errors["page"] = ["Номер страницы должен быть не меньше 1"];
        if (pageSize < 1) errors["pageSize"] = ["Размер страницы должен быть не меньше 1"];
        if (minPrice > maxPrice) errors["minPrice"] = ["minPrice не может быть больше maxPrice"];
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var result = await productService.GetAllAsync(
            category, minPrice, maxPrice, page, Math.Min(pageSize, MaxPageSize), ct);
        return Results.Ok(result);
    }

    /// <summary>GET /api/products/{id} — товар по идентификатору.</summary>
    /// <param name="id">Идентификатор товара.</param>
    /// <param name="productService">Сервис каталога.</param>
    /// <param name="ct">Токен отмены.</param>
    private static async Task<IResult> GetProduct(Guid id, ProductService productService, CancellationToken ct)
    {
        var product = await productService.GetByIdAsync(id, ct);
        return product is null ? NotFound(id) : Results.Ok(product);
    }

    /// <summary>GET /api/products/{id}/availability?quantity=2 — хватает ли остатка.</summary>
    /// <param name="id">Идентификатор товара.</param>
    /// <param name="quantity">Требуемое количество (≥ 1).</param>
    /// <param name="productService">Сервис каталога.</param>
    /// <param name="ct">Токен отмены.</param>
    private static async Task<IResult> CheckAvailability(
        Guid id,
        [FromQuery] int quantity,
        ProductService productService,
        CancellationToken ct)
    {
        if (quantity < 1)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["quantity"] = ["Количество должно быть не меньше 1"]
            });

        var result = await productService.CheckAvailabilityAsync(id, quantity, ct);
        return result is null ? NotFound(id) : Results.Ok(result);
    }

    /// <summary>POST /api/products — создание товара. Возвращает 201 и заголовок Location.</summary>
    /// <param name="dto">Данные товара.</param>
    /// <param name="productService">Сервис каталога.</param>
    /// <param name="ct">Токен отмены.</param>
    private static async Task<IResult> CreateProduct(CreateProductDto dto, ProductService productService, CancellationToken ct)
    {
        var created = await productService.CreateAsync(dto, ct);
        return Results.Created($"/api/products/{created.Id}", created);
    }

    /// <summary>PUT /api/products/{id} — обновление товара.</summary>
    /// <param name="id">Идентификатор товара.</param>
    /// <param name="dto">Изменяемые поля.</param>
    /// <param name="productService">Сервис каталога.</param>
    /// <param name="ct">Токен отмены.</param>
    private static async Task<IResult> UpdateProduct(
        Guid id, UpdateProductDto dto, ProductService productService, CancellationToken ct)
    {
        var updated = await productService.UpdateAsync(id, dto, ct);
        return updated is null ? NotFound(id) : Results.Ok(updated);
    }

    /// <summary>DELETE /api/products/{id} — мягкое удаление.</summary>
    /// <param name="id">Идентификатор товара.</param>
    /// <param name="productService">Сервис каталога.</param>
    /// <param name="ct">Токен отмены.</param>
    private static async Task<IResult> DeleteProduct(Guid id, ProductService productService, CancellationToken ct)
    {
        var deleted = await productService.DeleteAsync(id, ct);
        return deleted ? Results.NoContent() : NotFound(id);
    }

    /// <summary>Единый ответ 404 в формате ProblemDetails (RFC 9457).</summary>
    /// <param name="id">Идентификатор, который не нашли.</param>
    private static IResult NotFound(Guid id) =>
        Results.Problem(
            title: "Товар не найден",
            detail: $"Товар {id} не найден",
            statusCode: StatusCodes.Status404NotFound);
}
