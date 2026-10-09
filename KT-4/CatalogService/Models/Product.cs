namespace CatalogService.Models;

/// <summary>
/// Сущность товара в каталоге магазина.
/// Содержит полную информацию для отображения покупателю.
/// </summary>
public class Product
{
    /// <summary>Уникальный идентификатор товара.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Название товара. Обязательное поле, 3–200 символов.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Подробное описание. Необязательное.</summary>
    public string? Description { get; set; }

    /// <summary>Цена в рублях. Должна быть положительной.</summary>
    public decimal Price { get; set; }

    /// <summary>Количество на складе. Не может быть отрицательным.</summary>
    public int Stock { get; set; }

    /// <summary>Название категории товара.</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// URL изображения. Хранится полный URL,
    /// например https://cdn.shophub.ru/products/item.jpg
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>Флаг активности. Удалённые товары не удаляются физически (soft delete).</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Дата создания (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Дата последнего изменения (UTC).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
