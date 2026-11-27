using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DocumentWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_State",
                schema: "orders",
                table: "Orders");

            migrationBuilder.AddColumn<string>(
                name: "CreatePayloadHash",
                schema: "orders",
                table: "Orders",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ExpectedDate",
                schema: "orders",
                table: "Orders",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatePayloadHash",
                schema: "inventory",
                table: "Documents",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsEdited",
                schema: "inventory",
                table: "Documents",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "InputVersion",
                schema: "audit",
                table: "AuditLogs",
                type: "varchar(12)",
                unicode: false,
                maxLength: 12,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                schema: "audit",
                table: "AuditLogs",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_State",
                schema: "orders",
                table: "Orders",
                sql: "[Kind] IN (1,2) AND [Status] BETWEEN 1 AND 8 AND [WarehouseKind] = 3");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Entity_EntityId_Id",
                schema: "audit",
                table: "AuditLogs",
                columns: new[] { "Entity", "EntityId", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_State",
                schema: "orders",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_Entity_EntityId_Id",
                schema: "audit",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "CreatePayloadHash",
                schema: "orders",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ExpectedDate",
                schema: "orders",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CreatePayloadHash",
                schema: "inventory",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "IsEdited",
                schema: "inventory",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "InputVersion",
                schema: "audit",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "Reason",
                schema: "audit",
                table: "AuditLogs");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_State",
                schema: "orders",
                table: "Orders",
                sql: "[Kind] IN (1,2) AND [Status] BETWEEN 1 AND 6 AND [WarehouseKind] = 3");
        }
    }
}
