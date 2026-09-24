namespace TodoApi.Models;

/// <summary>
/// Задача из списка дел.
/// </summary>
public class TodoItem
{
    /// <summary>Уникальный идентификатор (назначается сервером).</summary>
    public int Id { get; set; }

    /// <summary>Название задачи.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Подробное описание (необязательно).</summary>
    public string? Description { get; set; }

    /// <summary>Признак выполнения.</summary>
    public bool IsCompleted { get; set; }

    /// <summary>Дата и время создания (UTC).</summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Тело запроса для создания (POST) и обновления (PUT) задачи.
/// Id и CreatedAt клиент не передаёт — их задаёт сервер.
/// </summary>
public record TodoItemRequest(string Title, string? Description, bool IsCompleted);
