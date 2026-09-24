using TodoApi.Models;
using TodoApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Хранилище задач в памяти — один экземпляр на всё приложение.
builder.Services.AddSingleton<TodoRepository>();

// Swagger: генерация описания API и UI для ручного тестирования.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

// Корень сайта перенаправляем на Swagger UI.
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

// Все эндпоинты задач живут под общим префиксом /api/todos.
var todos = app.MapGroup("/api/todos").WithTags("Todos");

// GET /api/todos — список всех задач (200 OK).
todos.MapGet("/", (TodoRepository repo) => Results.Ok(repo.GetAll()))
    .WithName("GetTodos")
    .Produces<List<TodoItem>>(StatusCodes.Status200OK);

// GET /api/todos/{id} — одна задача (200 OK) или 404 Not Found.
todos.MapGet("/{id:int}", (int id, TodoRepository repo) =>
        repo.GetById(id) is { } item
            ? Results.Ok(item)
            : Results.NotFound(new { message = $"Задача с id={id} не найдена" }))
    .WithName("GetTodoById")
    .Produces<TodoItem>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status404NotFound);

// POST /api/todos — создание задачи (201 Created + заголовок Location).
todos.MapPost("/", (TodoItemRequest request, TodoRepository repo) =>
    {
        if (Validate(request) is { } error)
        {
            return error;
        }

        var created = repo.Add(request);
        return Results.Created($"/api/todos/{created.Id}", created);
    })
    .WithName("CreateTodo")
    .Produces<TodoItem>(StatusCodes.Status201Created)
    .ProducesValidationProblem();

// PUT /api/todos/{id} — полное обновление задачи (200 OK) или 404 Not Found.
todos.MapPut("/{id:int}", (int id, TodoItemRequest request, TodoRepository repo) =>
    {
        if (Validate(request) is { } error)
        {
            return error;
        }

        return repo.Update(id, request) is { } updated
            ? Results.Ok(updated)
            : Results.NotFound(new { message = $"Задача с id={id} не найдена" });
    })
    .WithName("UpdateTodo")
    .Produces<TodoItem>(StatusCodes.Status200OK)
    .ProducesValidationProblem()
    .Produces(StatusCodes.Status404NotFound);

// DELETE /api/todos/{id} — удаление задачи (204 No Content) или 404 Not Found.
todos.MapDelete("/{id:int}", (int id, TodoRepository repo) =>
        repo.Delete(id)
            ? Results.NoContent()
            : Results.NotFound(new { message = $"Задача с id={id} не найдена" }))
    .WithName("DeleteTodo")
    .Produces(StatusCodes.Status204NoContent)
    .Produces(StatusCodes.Status404NotFound);

app.Run();

// Простая валидация входных данных: название обязательно.
// Возвращает 400 Bad Request с описанием ошибки или null, если всё в порядке.
static IResult? Validate(TodoItemRequest request)
{
    if (string.IsNullOrWhiteSpace(request.Title))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["title"] = ["Поле Title обязательно для заполнения"]
        });
    }

    return null;
}
