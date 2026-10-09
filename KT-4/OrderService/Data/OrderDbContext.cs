using MassTransit;
using Microsoft.EntityFrameworkCore;
using OrderService.Models;

namespace OrderService.Data;

/// <summary>
/// DbContext для OrderService. Своя БД: заказы, позиции и таблицы transactional outbox.
/// </summary>
/// <remarks>
/// Outbox (таблицы MassTransit) решает проблему «заказ сохранён, а событие потерялось»:
/// событие записывается в ту же транзакцию, что и заказ, а фоновый процесс MassTransit
/// доставляет его в RabbitMQ. Если брокер недоступен — событие дождётся его в БД.
/// </remarks>
/// <param name="options">Настройки подключения.</param>
public class OrderDbContext(DbContextOptions<OrderDbContext> options) : DbContext(options)
{
    /// <summary>Заказы.</summary>
    public DbSet<Order> Orders => Set<Order>();

    /// <summary>Позиции заказов.</summary>
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    /// <summary>Настройка схемы заказов и таблиц outbox.</summary>
    /// <param name="modelBuilder">Построитель модели EF Core.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("orders");

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(o => o.Id);
            entity.Property(o => o.UserEmail).IsRequired().HasMaxLength(254);
            entity.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(o => o.TotalAmount).HasPrecision(18, 2);
            entity.Property(o => o.Comment).HasMaxLength(1000);
            entity.Property(o => o.ShippingStreet).IsRequired().HasMaxLength(200);
            entity.Property(o => o.ShippingCity).IsRequired().HasMaxLength(100);
            entity.Property(o => o.ShippingZipCode).IsRequired().HasMaxLength(6);

            // История заказов пользователя — самый частый запрос
            entity.HasIndex(o => new { o.UserId, o.CreatedAt });

            entity.HasMany(o => o.Items)
                  .WithOne(i => i.Order)
                  .HasForeignKey(i => i.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(i => i.Id);
            entity.Property(i => i.ProductName).IsRequired().HasMaxLength(200);
            entity.Property(i => i.PriceAtPurchase).HasPrecision(18, 2);
        });

        // Таблицы MassTransit: InboxState, OutboxMessage, OutboxState
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
