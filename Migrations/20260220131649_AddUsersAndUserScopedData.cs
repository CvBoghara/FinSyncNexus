using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinSyncNexus.Migrations
{
    /// <inheritdoc />
    public partial class AddUsersAndUserScopedData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_Provider_ExternalId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_Provider_ExternalId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_Provider_ExternalId",
                table: "Expenses");

            migrationBuilder.DropIndex(
                name: "IX_Customers_Provider_ExternalId",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Connections_Provider",
                table: "Connections");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_Provider_ExternalId",
                table: "Accounts");

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "SyncErrors",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "Payments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "Invoices",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "Expenses",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "Customers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "Connections",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "Accounts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_UserId_Provider_ExternalId",
                table: "Payments",
                columns: new[] { "UserId", "Provider", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_UserId_Provider_ExternalId",
                table: "Invoices",
                columns: new[] { "UserId", "Provider", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_UserId_Provider_ExternalId",
                table: "Expenses",
                columns: new[] { "UserId", "Provider", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_UserId_Provider_ExternalId",
                table: "Customers",
                columns: new[] { "UserId", "Provider", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Connections_UserId_Provider",
                table: "Connections",
                columns: new[] { "UserId", "Provider" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_UserId_Provider_ExternalId",
                table: "Accounts",
                columns: new[] { "UserId", "Provider", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Payments_UserId_Provider_ExternalId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_UserId_Provider_ExternalId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_UserId_Provider_ExternalId",
                table: "Expenses");

            migrationBuilder.DropIndex(
                name: "IX_Customers_UserId_Provider_ExternalId",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Connections_UserId_Provider",
                table: "Connections");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_UserId_Provider_ExternalId",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "SyncErrors");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Connections");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Accounts");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_Provider_ExternalId",
                table: "Payments",
                columns: new[] { "Provider", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Provider_ExternalId",
                table: "Invoices",
                columns: new[] { "Provider", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_Provider_ExternalId",
                table: "Expenses",
                columns: new[] { "Provider", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Provider_ExternalId",
                table: "Customers",
                columns: new[] { "Provider", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Connections_Provider",
                table: "Connections",
                column: "Provider",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_Provider_ExternalId",
                table: "Accounts",
                columns: new[] { "Provider", "ExternalId" },
                unique: true);
        }
    }
}
