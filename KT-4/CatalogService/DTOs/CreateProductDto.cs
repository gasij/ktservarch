using System.ComponentModel.DataAnnotations;

namespace CatalogService.DTOs;

/// <summary>
/// DTO для создания нового товара.
/// Клиент не может задать Id, IsActive, CreatedAt — это устанавливает сервер.
/// Атрибуты валидации проверяются автоматически (builder.Services.AddValidation),
/// при ошибке клиент получает 400 Bad Request с описанием полей.
/// </summary>
/// <param name="Name">Название, 3–200 символов.</param>
/// <param name="Description">Описание (необязательно).</param>
/// <param name="Price">Цена в рублях, больше 0.</param>
/// <param name="Stock">Остаток, не меньше 0.</param>
/// <param name="Category">Категория, до 100 символов.</param>
/// <param name="ImageUrl">URL изображения (необязательно).</param>
public record CreateProductDto(
    [Required(ErrorMessage = "Название обязательно")]
    [StringLength(200, MinimumLength = 3, ErrorMessage = "Название должно быть от 3 до 200 символов")]
    string Name,

    [MaxLength(2000)]
    string? Description,

    [Range(typeof(decimal), "0.01", "100000000",
        ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true,
        ErrorMessage = "Цена должна быть больше 0")]
    decimal Price,

    [Range(0, int.MaxValue, ErrorMessage = "Остаток не может быть отрицательным")]
    int Stock,

    [Required(ErrorMessage = "Категория обязательна")]
    [StringLength(100, MinimumLength = 1)]
    string Category,

    [Url(ErrorMessage = "ImageUrl должен быть корректным URL")]
    string? ImageUrl
);
