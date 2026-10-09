namespace CatalogService.DTOs;

/// <summary>
/// DTO для отдачи данных о товаре клиентам.
/// Не содержит внутренних полей (IsActive, служебные даты).
/// </summary>
/// <param name="Id">Идентификатор товара.</param>
/// <param name="Name">Название.</param>
/// <param name="Description">Описание.</param>
/// <param name="Price">Текущая цена в рублях.</param>
/// <param name="Stock">Остаток на складе.</param>
/// <param name="Category">Категория.</param>
/// <param name="ImageUrl">URL изображения.</param>
/// <param name="CreatedAt">Дата создания (UTC).</param>
public record ProductDto(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    int Stock,
    string Category,
    string? ImageUrl,
    DateTime CreatedAt
);

/// <summary>
/// Ответ на проверку наличия товара. Этот эндпоинт вызывает OrderService при оформлении заказа.
/// </summary>
/// <param name="ProductId">Идентификатор товара.</param>
/// <param name="ProductName">Текущее название — OrderService сохранит его в снимок заказа.</param>
/// <param name="RequestedQuantity">Запрошенное количество.</param>
/// <param name="InStock">Хватает ли остатка на запрошенное количество.</param>
/// <param name="CurrentPrice">Текущая цена за единицу.</param>
public record AvailabilityDto(
    Guid ProductId,
    string ProductName,
    int RequestedQuantity,
    bool InStock,
    decimal CurrentPrice
);

/// <summary>Страница списка с метаданными пагинации.</summary>
/// <typeparam name="T">Тип элемента списка.</typeparam>
/// <param name="Items">Элементы текущей страницы.</param>
/// <param name="TotalCount">Всего элементов по фильтру.</param>
/// <param name="Page">Номер страницы (с 1).</param>
/// <param name="PageSize">Размер страницы.</param>
/// <param name="TotalPages">Всего страниц.</param>
public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);
