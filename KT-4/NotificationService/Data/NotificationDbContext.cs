using Microsoft.EntityFrameworkCore;
using NotificationService.Models;

namespace NotificationService.Data;

/// <summary>
/// DbContext для NotificationService. Своя БД — только журнал уведомлений.
/// </summary>
/// <param name="options">Настройки подключения.</param>
public class NotificationDbContext(DbContextOptions<NotificationDbContext> options) : DbContext(options)
{
    /// <summary>Журнал отправленных уведомлений.</summary>
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();

    /// <summary>Настройка схемы.</summary>
    /// <param name="modelBuilder">Построитель модели EF Core.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("notifications");

        modelBuilder.Entity<NotificationLog>(entity =>
        {
            entity.HasKey(n => n.Id);
            entity.Property(n => n.UserEmail).IsRequired().HasMaxLength(254);
            entity.Property(n => n.Type).IsRequired().HasMaxLength(50);
            entity.Property(n => n.Channel).IsRequired().HasMaxLength(20);
            entity.Property(n => n.Subject).IsRequired().HasMaxLength(300);
            entity.Property(n => n.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(n => n.Error).HasMaxLength(2000);

            // Идемпотентность: повторная доставка события не создаст второе письмо
            entity.HasIndex(n => new { n.ReferenceId, n.Type }).IsUnique();
            entity.HasIndex(n => n.UserEmail);
        });
    }
}
