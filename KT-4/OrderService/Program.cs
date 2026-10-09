using System.Reflection;
using HealthChecks.UI.Client;
using MassTransit;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OrderService.Data;
using OrderService.Endpoints;
using OrderService.HealthChecks;
using OrderService.Infrastructure;
using OrderService.Services;

var builder = WebApplication.CreateBuilder(args);

var postgres = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Не задана строка подключения ConnectionStrings:Default");
var catalogUrl = builder.Configuration["CatalogService:BaseUrl"]
    ?? throw new InvalidOperationException("Не задан адрес CatalogService:BaseUrl");

// ── Swagger/OpenAPI ───────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "OrderService API",
        Version = "v1",
        Description = "Заказы ShopHub: интеграция с CatalogService (HTTP + Polly) и RabbitMQ"
    });
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml"));
});

// ── Валидация и обработка ошибок ──────────────────────────────────────────
builder.Services.AddValidation();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<OrderExceptionHandler>();

// ── База данных ───────────────────────────────────────────────────────────
builder.Services.AddDbContext<OrderDbContext>(options =>
    options.UseNpgsql(postgres, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "orders")));

// ── HTTP-клиент CatalogService: retry + circuit breaker + таймауты ────────
builder.Services.AddCatalogServiceClient(new Uri(catalogUrl));

// ── MassTransit (RabbitMQ) + transactional outbox ─────────────────────────
builder.Services.AddMassTransit(x =>
{
    x.AddEntityFrameworkOutbox<OrderDbContext>(o =>
    {
        o.UsePostgres();
        // IPublishEndpoint пишет события в таблицу OutboxMessage, а не сразу в RabbitMQ.
        o.UseBusOutbox();
        o.QueryDelay = TimeSpan.FromSeconds(1);
    });

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"], "/", h =>
        {
            h.Username(builder.Configuration["RabbitMQ:Username"]!);
            h.Password(builder.Configuration["RabbitMQ:Password"]!);
        });
        cfg.ConfigureEndpoints(context);
    });
});

// ── Сервисы ───────────────────────────────────────────────────────────────
builder.Services.AddScoped<OrderManagementService>();

// ── Health Checks ─────────────────────────────────────────────────────────
// Проверка шины RabbitMQ (тег "ready") добавляется MassTransit автоматически.
builder.Services.AddHealthChecks()
    .AddNpgSql(postgres, name: "postgresql", tags: ["db", "ready"])
    .AddCheck<CatalogServiceHealthCheck>("catalog-service",
        failureStatus: HealthStatus.Degraded, tags: ["dependency"]);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "OrderService v1"));
    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
}

// ── Автоматические миграции при старте ───────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    await db.Database.MigrateAsync();
}

app.MapOrderEndpoints();

// /health       — все проверки (Degraded, если недоступен CatalogService)
// /health/live  — процесс жив
// /health/ready — готов принимать трафик (своя БД и брокер)
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.Run();
