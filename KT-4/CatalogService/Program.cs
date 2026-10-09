using System.Reflection;
using CatalogService.Consumers;
using CatalogService.Data;
using CatalogService.Endpoints;
using CatalogService.Services;
using HealthChecks.UI.Client;
using MassTransit;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var postgres = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Не задана строка подключения ConnectionStrings:Default");
var redis = builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException("Не задана строка подключения ConnectionStrings:Redis");

// ── Swagger/OpenAPI ───────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "CatalogService API",
        Version = "v1",
        Description = "Управление каталогом товаров ShopHub"
    });
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml"));
});

// ── Валидация DTO по DataAnnotations (400 + ProblemDetails) ───────────────
builder.Services.AddValidation();
builder.Services.AddProblemDetails();

// ── База данных (PostgreSQL через EF Core) ────────────────────────────────
builder.Services.AddDbContext<CatalogDbContext>(options =>
    options.UseNpgsql(postgres, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "catalog")));

// ── Redis-кэш ─────────────────────────────────────────────────────────────
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redis;
    options.InstanceName = "catalog:";
});

// ── Сервисы (DI) ──────────────────────────────────────────────────────────
builder.Services.AddScoped<ProductService>();

// ── MassTransit (RabbitMQ): потребитель OrderCreated → списание остатков ───
builder.Services.AddMassTransit(x =>
{
    // Префикс "catalog" даёт очередь catalog-order-created: у каждого сервиса своя очередь.
    x.SetEndpointNameFormatter(new KebabCaseEndpointNameFormatter("catalog", false));
    x.AddConsumer<OrderCreatedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"], "/", h =>
        {
            h.Username(builder.Configuration["RabbitMQ:Username"]!);
            h.Password(builder.Configuration["RabbitMQ:Password"]!);
        });

        // Временные сбои (например, БД недоступна) — 3 повтора с растущей паузой,
        // затем сообщение уходит в очередь catalog-order-created_error.
        cfg.UseMessageRetry(r => r.Intervals(500, 2000, 5000));
        cfg.ConfigureEndpoints(context);
    });
});

// ── Health Checks ─────────────────────────────────────────────────────────
// Проверка шины RabbitMQ (тег "ready") добавляется MassTransit автоматически.
builder.Services.AddHealthChecks()
    .AddNpgSql(postgres, name: "postgresql", tags: ["db", "ready"])
    .AddRedis(redis, name: "redis", tags: ["cache", "ready"]);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

// ── Swagger UI (только в dev) ─────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "CatalogService v1"));
    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
}

// ── Автоматические миграции при старте ───────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    await db.Database.MigrateAsync();
}

// ── Эндпоинты ─────────────────────────────────────────────────────────────
app.MapProductEndpoints();

// /health       — все проверки, подробный JSON (статус каждой зависимости)
// /health/live  — процесс жив (без проверок зависимостей) — для liveness-проб
// /health/ready — готов принимать трафик (БД, кэш, брокер)
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
}).WithTags("Health");
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.Run();
