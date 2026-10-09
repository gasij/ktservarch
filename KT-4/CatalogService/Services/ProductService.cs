using System.Text.Json;
using CatalogService.Data;
using CatalogService.DTOs;
using CatalogService.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace CatalogService.Services;

/// <summary>
/// Сервис управления товарами каталога.
/// Содержит всю бизнес-логику: CRUD, проверку наличия, списание остатков и кэширование в Redis.
/// </summary>
/// <remarks>
/// Redis здесь — только ускоритель чтения. Любая ошибка Redis логируется и не ломает запрос:
/// источник правды — PostgreSQL.
/// </remarks>
/// <param name="db">Контекст БД каталога.</param>
/// <param name="cache">Распределённый кэш (Redis).</param>
/// <param name="logger">Логгер.</param>
public class ProductService(
    CatalogDbContext db,
    IDistributedCache cache,
    ILogger<ProductService> logger)
{
    /// <summary>Время жизни записи в кэше.</summary>
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Получить список активных товаров с фильтрацией и пагинацией.
    /// Результат не кэшируется — список меняется часто.
    /// </summary>
    /// <param name="category">Фильтр по категории (точное совпадение).</param>
    /// <param name="minPrice">Минимальная цена.</param>
    /// <param name="maxPrice">Максимальная цена.</param>
    /// <param name="page">Номер страницы, начиная с 1.</param>
    /// <param name="pageSize">Размер страницы.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Страница товаров и общее количество по фильтру.</returns>
    public async Task<PagedResult<ProductDto>> GetAllAsync(
        string? category,
        decimal? minPrice,
        decimal? maxPrice,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = db.Products.AsNoTracking().Where(p => p.IsActive);

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(p => p.Category == category);

        if (minPrice.HasValue)
            query = query.Where(p => p.Price >= minPrice.Value);

        if (maxPrice.HasValue)
            query = query.Where(p => p.Price <= maxPrice.Value);

        var totalCount = await query.CountAsync(ct);

        var products = await query
            .OrderByDescending(p => p.CreatedAt)
            .ThenBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        return new PagedResult<ProductDto>(products.Select(ToDto).ToList(), totalCount, page, pageSize, totalPages);
    }

    /// <summary>
    /// Получить товар по ID. Сначала смотрит в Redis (TTL 5 минут), при промахе — в PostgreSQL.
    /// </summary>
    /// <param name="id">Идентификатор товара.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Товар или null, если он не найден или удалён.</returns>
    public async Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var cacheKey = CacheKey(id);

        try
        {
            var cached = await cache.GetStringAsync(cacheKey, ct);
            if (cached is not null)
            {
                logger.LogDebug("Товар {ProductId} получен из кэша", id);
                return JsonSerializer.Deserialize<ProductDto>(cached);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Redis упал — не страшно, идём в БД
            logger.LogWarning(ex, "Ошибка чтения из Redis, читаем из БД");
        }

        var product = await db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && p.IsActive, ct);

        if (product is null) return null;

        var dto = ToDto(product);

        try
        {
            await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(dto),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl }, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Не удалось сохранить товар в кэш — продолжаем без кэша");
        }

        return dto;
    }

    /// <summary>Создать новый товар.</summary>
    /// <param name="dto">Данные нового товара (уже провалидированы).</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Созданный товар.</returns>
    public async Task<ProductDto> CreateAsync(CreateProductDto dto, CancellationToken ct = default)
    {
        var product = new Product
        {
            Name = dto.Name.Trim(),
            Description = dto.Description,
            Price = dto.Price,
            Stock = dto.Stock,
            Category = dto.Category.Trim(),
            ImageUrl = dto.ImageUrl
        };

        db.Products.Add(product);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Создан товар {ProductId}: {ProductName}", product.Id, product.Name);
        return ToDto(product);
    }

    /// <summary>Частично обновить товар: меняются только переданные (не null) поля.</summary>
    /// <param name="id">Идентификатор товара.</param>
    /// <param name="dto">Изменяемые поля.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Обновлённый товар или null, если товар не найден или удалён.</returns>
    public async Task<ProductDto?> UpdateAsync(Guid id, UpdateProductDto dto, CancellationToken ct = default)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id && p.IsActive, ct);
        if (product is null) return null;

        if (dto.Name is not null) product.Name = dto.Name.Trim();
        if (dto.Description is not null) product.Description = dto.Description;
        if (dto.Price.HasValue) product.Price = dto.Price.Value;
        if (dto.Stock.HasValue) product.Stock = dto.Stock.Value;
        if (dto.Category is not null) product.Category = dto.Category.Trim();
        if (dto.ImageUrl is not null) product.ImageUrl = dto.ImageUrl;
        product.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        await InvalidateAsync(id, ct);

        logger.LogInformation("Обновлён товар {ProductId}", id);
        return ToDto(product);
    }

    /// <summary>Мягкое удаление (soft delete): IsActive = false. Старые заказы продолжают ссылаться на товар.</summary>
    /// <param name="id">Идентификатор товара.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>true — товар удалён; false — не найден или уже удалён.</returns>
    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id && p.IsActive, ct);
        if (product is null) return false;

        product.IsActive = false;
        product.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await InvalidateAsync(id, ct);

        logger.LogInformation("Товар {ProductId} помечен как удалённый", id);
        return true;
    }

    /// <summary>
    /// Проверить наличие товара и его текущую цену. Используется OrderService при создании заказа,
    /// поэтому читает напрямую из БД, минуя кэш: остаток должен быть актуальным.
    /// </summary>
    /// <param name="id">Идентификатор товара.</param>
    /// <param name="quantity">Требуемое количество.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Результат проверки или null, если товар не найден.</returns>
    public async Task<AvailabilityDto?> CheckAvailabilityAsync(Guid id, int quantity, CancellationToken ct = default)
    {
        var product = await db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && p.IsActive, ct);

        return product is null
            ? null
            : new AvailabilityDto(product.Id, product.Name, quantity, product.Stock >= quantity, product.Price);
    }

    /// <summary>
    /// Списать остатки по созданному заказу. Вызывается потребителем события OrderCreated.
    /// </summary>
    /// <remarks>
    /// Операция идемпотентна: повторная доставка того же события ничего не изменит.
    /// Списание атомарно (UPDATE ... WHERE Stock &gt;= qty) — две параллельные операции не уведут остаток в минус.
    /// </remarks>
    /// <param name="orderId">Идентификатор заказа.</param>
    /// <param name="items">Пары «товар — количество».</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>false, если заказ уже обрабатывался ранее.</returns>
    public async Task<bool> DeductStockAsync(
        Guid orderId,
        IReadOnlyCollection<(Guid ProductId, int Quantity)> items,
        CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        if (await db.ProcessedOrders.AnyAsync(p => p.OrderId == orderId, ct))
        {
            logger.LogInformation("Остатки по заказу {OrderId} уже списаны — повторное событие пропущено", orderId);
            return false;
        }

        foreach (var (productId, quantity) in items)
        {
            var affected = await db.Products
                .Where(p => p.Id == productId && p.Stock >= quantity)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.Stock, p => p.Stock - quantity)
                    .SetProperty(p => p.UpdatedAt, DateTime.UtcNow), ct);

            if (affected == 0)
            {
                // Между проверкой наличия и списанием товар раскупили.
                // В полной системе здесь публикуется StockReservationFailed и заказ отменяется (сага).
                logger.LogWarning(
                    "Не удалось списать {Quantity} шт. товара {ProductId} по заказу {OrderId}: недостаточно остатка",
                    quantity, productId, orderId);
            }
        }

        db.ProcessedOrders.Add(new ProcessedOrder { OrderId = orderId });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        foreach (var (productId, _) in items)
            await InvalidateAsync(productId, ct);

        logger.LogInformation("Остатки по заказу {OrderId} списаны, позиций: {Count}", orderId, items.Count);
        return true;
    }

    /// <summary>Удалить товар из кэша. Ошибки Redis не пробрасываются.</summary>
    /// <param name="id">Идентификатор товара.</param>
    /// <param name="ct">Токен отмены.</param>
    private async Task InvalidateAsync(Guid id, CancellationToken ct)
    {
        try
        {
            await cache.RemoveAsync(CacheKey(id), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Не удалось сбросить кэш товара {ProductId}", id);
        }
    }

    /// <summary>Ключ товара в Redis.</summary>
    /// <param name="id">Идентификатор товара.</param>
    private static string CacheKey(Guid id) => $"product:{id}";

    /// <summary>Преобразовать сущность в DTO.</summary>
    /// <param name="p">Сущность товара.</param>
    private static ProductDto ToDto(Product p) => new(
        p.Id, p.Name, p.Description, p.Price,
        p.Stock, p.Category, p.ImageUrl, p.CreatedAt);
}
