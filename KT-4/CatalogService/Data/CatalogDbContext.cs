using CatalogService.Models;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Data;

/// <summary>
/// DbContext для CatalogService.
/// Каждый микросервис владеет только своей БД — здесь только таблицы каталога.
/// </summary>
/// <param name="options">Настройки подключения (строка подключения задаётся в Program.cs).</param>
public class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    /// <summary>Фиксированная дата для начальных данных: значения HasData должны быть детерминированными,
    /// иначе EF Core на каждой сборке считает модель изменённой.</summary>
    private static readonly DateTime SeedDate = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>Товары каталога.</summary>
    public DbSet<Product> Products => Set<Product>();

    /// <summary>Заказы, по которым уже списаны остатки (идемпотентность потребителя событий).</summary>
    public DbSet<ProcessedOrder> ProcessedOrders => Set<ProcessedOrder>();

    /// <summary>Настройка схемы: ограничения, индексы и начальные данные.</summary>
    /// <param name="modelBuilder">Построитель модели EF Core.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("catalog");

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Name).IsRequired().HasMaxLength(200);
            entity.Property(p => p.Description).HasMaxLength(2000);
            entity.Property(p => p.Price).HasPrecision(18, 2);
            entity.Property(p => p.Category).IsRequired().HasMaxLength(100);
            entity.Property(p => p.ImageUrl).HasMaxLength(500);

            // Остаток не может уйти в минус даже при гонке двух списаний.
            entity.ToTable(t => t.HasCheckConstraint("CK_Products_Stock_NonNegative", "\"Stock\" >= 0"));

            // Индекс для фильтрации по категории
            entity.HasIndex(p => p.Category);
            // Частичный индекс — только активные товары
            entity.HasIndex(p => p.IsActive).HasFilter("\"IsActive\" = true");

            // Начальные данные для тестирования
            entity.HasData(
                new Product
                {
                    Id = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
                    Name = "iPhone 15 Pro",
                    Description = "Смартфон Apple с чипом A17 Pro",
                    Price = 89990.00m,
                    Stock = 15,
                    Category = "Смартфоны",
                    CreatedAt = SeedDate,
                    UpdatedAt = SeedDate
                },
                new Product
                {
                    Id = Guid.Parse("4ba96f75-6828-4673-a4bd-3d074e77beb7"),
                    Name = "MacBook Air M3",
                    Description = "Ноутбук Apple с чипом M3",
                    Price = 129990.00m,
                    Stock = 8,
                    Category = "Ноутбуки",
                    CreatedAt = SeedDate,
                    UpdatedAt = SeedDate
                },
                new Product
                {
                    Id = Guid.Parse("5cb07a86-7939-4784-b5ce-4e185f88cfc8"),
                    Name = "AirPods Pro 2",
                    Description = "Беспроводные наушники с шумоподавлением. Нет в наличии — для проверки ошибки 409",
                    Price = 24990.00m,
                    Stock = 0,
                    Category = "Аудио",
                    CreatedAt = SeedDate,
                    UpdatedAt = SeedDate
                }
            );
        });

        modelBuilder.Entity<ProcessedOrder>(entity =>
        {
            entity.HasKey(p => p.OrderId);
        });
    }
}
