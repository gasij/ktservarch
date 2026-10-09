using System.ComponentModel.DataAnnotations;

namespace CatalogService.DTOs;

/// <summary>
/// DTO для обновления существующего товара. Все поля опциональны:
/// null означает «не менять», переданные значения проверяются теми же правилами, что при создании.
/// </summary>
/// <param name="Name">Новое название.</param>
/// <param name="Description">Новое описание.</param>
/// <param name="Price">Новая цена.</param>
/// <param name="Stock">Новый остаток.</param>
/// <param name="Category">Новая категория.</param>
/// <param name="ImageUrl">Новый URL изображения.</param>
public record UpdateProductDto(
    [StringLength(200, MinimumLength = 3, ErrorMessage = "Название должно быть от 3 до 200 символов")]
    string? Name,

    [MaxLength(2000)]
    string? Description,

    [Range(typeof(decimal), "0.01", "100000000",
        ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true,
        ErrorMessage = "Цена должна быть больше 0")]
    decimal? Price,

    [Range(0, int.MaxValue, ErrorMessage = "Остаток не может быть отрицательным")]
    int? Stock,

    [StringLength(100, MinimumLength = 1)]
    string? Category,

    [Url(ErrorMessage = "ImageUrl должен быть корректным URL")]
    string? ImageUrl
);
