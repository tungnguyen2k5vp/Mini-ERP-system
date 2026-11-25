using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StockLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "inventory");

            migrationBuilder.CreateTable(
                name: "Balances",
                schema: "inventory",
                columns: table => new
                {
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseKind = table.Column<byte>(type: "tinyint", nullable: false),
                    OnHand = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Reserved = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    AverageCost = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Balances", x => new { x.WarehouseId, x.ProductId });
                    table.CheckConstraint("CK_Balances_Amounts", "[OnHand] >= [Reserved] AND [Reserved] >= 0 AND [AverageCost] >= 0");
                    table.CheckConstraint("CK_Balances_Warehouse", "[WarehouseKind] = 3");
                    table.ForeignKey(
                        name: "FK_Balances_Lookups_WarehouseKind_WarehouseId",
                        columns: x => new { x.WarehouseKind, x.WarehouseId },
                        principalSchema: "catalog",
                        principalTable: "Lookups",
                        principalColumns: new[] { "Kind", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Balances_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "catalog",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Documents",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<byte>(type: "tinyint", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    WarehouseKind = table.Column<byte>(type: "tinyint", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DestinationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ReversesId = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PostedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                    table.CheckConstraint("CK_Documents_Destination", "([Kind] = 3 AND [DestinationId] IS NOT NULL AND [DestinationId] <> [WarehouseId]) OR [Kind] = 6 OR ([Kind] NOT IN (3,6) AND [DestinationId] IS NULL)");
                    table.CheckConstraint("CK_Documents_Kind", "[Kind] BETWEEN 1 AND 6 AND [WarehouseKind] = 3");
                    table.CheckConstraint("CK_Documents_Reversal", "([Kind] = 6 AND [ReversesId] IS NOT NULL) OR ([Kind] <> 6 AND [ReversesId] IS NULL)");
                    table.CheckConstraint("CK_Documents_Status", "([Status] IN (1,3) AND [PostedAtUtc] IS NULL) OR ([Status] IN (2,4) AND [PostedAtUtc] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_Documents_Documents_ReversesId",
                        column: x => x.ReversesId,
                        principalSchema: "inventory",
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Documents_Lookups_WarehouseKind_DestinationId",
                        columns: x => new { x.WarehouseKind, x.DestinationId },
                        principalSchema: "catalog",
                        principalTable: "Lookups",
                        principalColumns: new[] { "Kind", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Documents_Lookups_WarehouseKind_WarehouseId",
                        columns: x => new { x.WarehouseKind, x.WarehouseId },
                        principalSchema: "catalog",
                        principalTable: "Lookups",
                        principalColumns: new[] { "Kind", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentLines",
                schema: "inventory",
                columns: table => new
                {
                    DocumentId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sku = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    CountVersion = table.Column<byte[]>(type: "varbinary(8)", maxLength: 8, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentLines", x => new { x.DocumentId, x.ProductId });
                    table.CheckConstraint("CK_StockLines_Cost", "[UnitCost] IS NULL OR [UnitCost] >= 0");
                    table.ForeignKey(
                        name: "FK_DocumentLines_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "inventory",
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentLines_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "catalog",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Movements",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentId = table.Column<int>(type: "int", nullable: false),
                    WarehouseKind = table.Column<byte>(type: "tinyint", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuantityDelta = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    PreviousAverageCost = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    PostedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Movements", x => x.Id);
                    table.CheckConstraint("CK_Movements_Amounts", "[QuantityDelta] <> 0 AND [BalanceAfter] >= 0 AND [UnitCost] >= 0 AND [PreviousAverageCost] >= 0");
                    table.CheckConstraint("CK_Movements_Warehouse", "[WarehouseKind] = 3");
                    table.ForeignKey(
                        name: "FK_Movements_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "inventory",
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Movements_Lookups_WarehouseKind_WarehouseId",
                        columns: x => new { x.WarehouseKind, x.WarehouseId },
                        principalSchema: "catalog",
                        principalTable: "Lookups",
                        principalColumns: new[] { "Kind", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Movements_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "catalog",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Balances_ProductId",
                schema: "inventory",
                table: "Balances",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Balances_WarehouseKind_WarehouseId",
                schema: "inventory",
                table: "Balances",
                columns: new[] { "WarehouseKind", "WarehouseId" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentLines_ProductId",
                schema: "inventory",
                table: "DocumentLines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_Kind_Id",
                schema: "inventory",
                table: "Documents",
                columns: new[] { "Kind", "Id" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_RequestId",
                schema: "inventory",
                table: "Documents",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ReversesId",
                schema: "inventory",
                table: "Documents",
                column: "ReversesId",
                unique: true,
                filter: "[ReversesId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_WarehouseKind_DestinationId",
                schema: "inventory",
                table: "Documents",
                columns: new[] { "WarehouseKind", "DestinationId" });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_WarehouseKind_WarehouseId",
                schema: "inventory",
                table: "Documents",
                columns: new[] { "WarehouseKind", "WarehouseId" });

            migrationBuilder.CreateIndex(
                name: "IX_Movements_DocumentId_WarehouseId_ProductId",
                schema: "inventory",
                table: "Movements",
                columns: new[] { "DocumentId", "WarehouseId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Movements_ProductId",
                schema: "inventory",
                table: "Movements",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Movements_WarehouseId_ProductId_Id",
                schema: "inventory",
                table: "Movements",
                columns: new[] { "WarehouseId", "ProductId", "Id" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Movements_WarehouseKind_WarehouseId",
                schema: "inventory",
                table: "Movements",
                columns: new[] { "WarehouseKind", "WarehouseId" });

            migrationBuilder.EnsureSchema("reporting");
            migrationBuilder.Sql("""
                EXEC(N'CREATE VIEW reporting.vw_InventorySummary AS
                    SELECT b.WarehouseId, w.Name AS WarehouseName, b.ProductId, p.Sku, p.Name AS ProductName,
                           b.OnHand, b.Reserved, CONVERT(decimal(19,4), b.OnHand - b.Reserved) AS Available,
                           b.AverageCost, p.MinimumStock,
                           CONVERT(bit, CASE WHEN p.IsActive = 1 AND b.OnHand - b.Reserved < p.MinimumStock
                                             THEN 1 ELSE 0 END) AS IsLowStock
                    FROM inventory.Balances AS b
                    INNER JOIN catalog.Lookups AS w ON w.Kind = b.WarehouseKind AND w.Id = b.WarehouseId
                    INNER JOIN catalog.Products AS p ON p.Id = b.ProductId');
                """);
            migrationBuilder.Sql("""
                EXEC(N'CREATE PROCEDURE reporting.usp_GetInventoryReport
                    @WarehouseId uniqueidentifier = NULL,
                    @ProductId uniqueidentifier = NULL,
                    @Search nvarchar(120) = NULL,
                    @LowOnly bit = 0,
                    @Offset int = 0,
                    @PageSize int = 25
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SELECT COUNT(*)
                    FROM reporting.vw_InventorySummary
                    WHERE (@WarehouseId IS NULL OR WarehouseId = @WarehouseId)
                      AND (@ProductId IS NULL OR ProductId = @ProductId)
                      AND (@LowOnly = 0 OR IsLowStock = 1)
                      AND (@Search IS NULL OR CHARINDEX(@Search, Sku) > 0 OR CHARINDEX(@Search, ProductName) > 0);

                    SELECT WarehouseId, WarehouseName, ProductId, Sku, ProductName, OnHand, Reserved,
                           Available, AverageCost, MinimumStock, IsLowStock
                    FROM reporting.vw_InventorySummary
                    WHERE (@WarehouseId IS NULL OR WarehouseId = @WarehouseId)
                      AND (@ProductId IS NULL OR ProductId = @ProductId)
                      AND (@LowOnly = 0 OR IsLowStock = 1)
                      AND (@Search IS NULL OR CHARINDEX(@Search, Sku) > 0 OR CHARINDEX(@Search, ProductName) > 0)
                    ORDER BY Sku, WarehouseId, ProductId
                    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
                END');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE reporting.usp_GetInventoryReport; DROP VIEW reporting.vw_InventorySummary;");
            migrationBuilder.DropTable(
                name: "Balances",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "DocumentLines",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "Movements",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "Documents",
                schema: "inventory");
        }
    }
}
