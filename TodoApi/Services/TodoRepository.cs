using TodoApi.Models;

namespace TodoApi.Services;

/// <summary>
/// Хранилище задач в памяти процесса (обычный List).
/// Данные теряются при перезапуске приложения.
/// Регистрируется как Singleton, поэтому доступ к списку защищён lock —
/// запросы могут обрабатываться параллельно.
/// </summary>
public class TodoRepository
{
    private readonly List<TodoItem> _items = new();
    private readonly object _lock = new();
    private int _nextId = 1;

    public TodoRepository()
    {
        // Несколько стартовых задач, чтобы GET сразу возвращал данные.
        Add(new TodoItemRequest("Изучить Minimal API", "Прочитать документацию ASP.NET Core", false));
        Add(new TodoItemRequest("Написать README", "Добавить скриншоты из Swagger", false));
        Add(new TodoItemRequest("Создать репозиторий", null, true));
    }

    /// <summary>Возвращает копию списка всех задач.</summary>
    public List<TodoItem> GetAll()
    {
        lock (_lock)
        {
            return _items.ToList();
        }
    }

    /// <summary>Ищет задачу по id; null, если не найдена.</summary>
    public TodoItem? GetById(int id)
    {
        lock (_lock)
        {
            return _items.FirstOrDefault(t => t.Id == id);
        }
    }

    /// <summary>Создаёт новую задачу и присваивает ей следующий свободный id.</summary>
    public TodoItem Add(TodoItemRequest request)
    {
        lock (_lock)
        {
            var item = new TodoItem
            {
                Id = _nextId++,
                Title = request.Title.Trim(),
                Description = request.Description,
                IsCompleted = request.IsCompleted,
                CreatedAt = DateTime.UtcNow
            };
            _items.Add(item);
            return item;
        }
    }

    /// <summary>Обновляет поля задачи; null, если задачи с таким id нет.</summary>
    public TodoItem? Update(int id, TodoItemRequest request)
    {
        lock (_lock)
        {
            var item = _items.FirstOrDefault(t => t.Id == id);
            if (item is null)
            {
                return null;
            }

            item.Title = request.Title.Trim();
            item.Description = request.Description;
            item.IsCompleted = request.IsCompleted;
            return item;
        }
    }

    /// <summary>Удаляет задачу; false, если задачи с таким id нет.</summary>
    public bool Delete(int id)
    {
        lock (_lock)
        {
            return _items.RemoveAll(t => t.Id == id) > 0;
        }
    }
}
