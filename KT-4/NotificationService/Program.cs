using System.Reflection;
using HealthChecks.UI.Client;
using MassTransit;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using NotificationService.Consumers;
using NotificationService.Data;
using NotificationService.Endpoints;
using NotificationService.Services;

var builder = WebApplication.CreateBuilder(args);

var postgres = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Не задана строка подключения ConnectionStrings:Default");

// ── Swagger/OpenAPI ───────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "NotificationService API",
        Version = "v1",
        Description = "Уведомления ShopHub: потребитель событий RabbitMQ и журнал отправок"
    });
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml"));
});
builder.Services.AddProblemDetails();

// ── БД ────────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<NotificationDbContext>(options =>
    options.UseNpgsql(postgres, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "notifications")));

// ── Email: Smtp (MailKit → Mailpit/реальный SMTP) или Fake (только лог) ───
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.SectionName));
if (string.Equals(builder.Configuration["Email:Provider"], "Fake", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddScoped<IEmailService, FakeEmailService>();
else
    builder.Services.AddScoped<IEmailService, EmailService>();

// ── MassTransit (RabbitMQ): подписка на OrderCreated ──────────────────────
builder.Services.AddMassTransit(x =>
{
    // Префикс "notification" → очередь notification-order-created (отдельная от каталога).
    x.SetEndpointNameFormatter(new KebabCaseEndpointNameFormatter("notification", false));
    x.AddConsumer<OrderCreatedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"], "/", h =>
        {
            h.Username(builder.Configuration["RabbitMQ:Username"]!);
            h.Password(builder.Configuration["RabbitMQ:Password"]!);
        });

        // SMTP/БД временно недоступны → 3 повтора (1 с, 5 с, 15 с), затем сообщение
        // уходит в notification-order-created_error для ручного разбора.
        cfg.UseMessageRetry(r => r.Intervals(1000, 5000, 15000));
        cfg.ConfigureEndpoints(context);
    });
});

// ── Health Checks ─────────────────────────────────────────────────────────
// Проверка шины RabbitMQ (тег "ready") добавляется MassTransit автоматически.
builder.Services.AddHealthChecks()
    .AddNpgSql(postgres, name: "postgresql", tags: ["db", "ready"]);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "NotificationService v1"));
    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
}

// ── Автоматические миграции при старте ───────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
    await db.Database.MigrateAsync();
}

app.MapNotificationEndpoints();

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
