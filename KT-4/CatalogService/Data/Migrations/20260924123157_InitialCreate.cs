using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CatalogService.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "catalog");

            migrationBuilder.CreateTable(
                name: "ProcessedOrders",
                schema: "catalog",
                columns: table => new
                {
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessedOrders", x => x.OrderId);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Stock = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                    table.CheckConstraint("CK_Products_Stock_NonNegative", "\"Stock\" >= 0");
                });

            migrationBuilder.InsertData(
                schema: "catalog",
                table: "Products",
                columns: new[] { "Id", "Category", "CreatedAt", "Description", "ImageUrl", "IsActive", "Name", "Price", "Stock", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "Смартфоны", new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Смартфон Apple с чипом A17 Pro", null, true, "iPhone 15 Pro", 89990.00m, 15, new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("4ba96f75-6828-4673-a4bd-3d074e77beb7"), "Ноутбуки", new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Ноутбук Apple с чипом M3", null, true, "MacBook Air M3", 129990.00m, 8, new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("5cb07a86-7939-4784-b5ce-4e185f88cfc8"), "Аудио", new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Беспроводные наушники с шумоподавлением. Нет в наличии — для проверки ошибки 409", null, true, "AirPods Pro 2", 24990.00m, 0, new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Products_Category",
                schema: "catalog",
                table: "Products",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_Products_IsActive",
                schema: "catalog",
                table: "Products",
                column: "IsActive",
                filter: "\"IsActive\" = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcessedOrders",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "Products",
                schema: "catalog");
        }
    }
}
