using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OrdersAndFinance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "finance");

            migrationBuilder.EnsureSchema(
                name: "orders");

            migrationBuilder.CreateTable(
                name: "Orders",
                schema: "orders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<byte>(type: "tinyint", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    PartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseKind = table.Column<byte>(type: "tinyint", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                    table.CheckConstraint("CK_Orders_State", "[Kind] IN (1,2) AND [Status] BETWEEN 1 AND 6 AND [WarehouseKind] = 3");
                    table.ForeignKey(
                        name: "FK_Orders_BusinessPartners_PartnerId",
                        column: x => x.PartnerId,
                        principalSchema: "catalog",
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Orders_Lookups_WarehouseKind_WarehouseId",
                        columns: x => new { x.WarehouseKind, x.WarehouseId },
                        principalSchema: "catalog",
                        principalTable: "Lookups",
                        principalColumns: new[] { "Kind", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Direction = table.Column<byte>(type: "tinyint", nullable: false),
                    IsRefund = table.Column<bool>(type: "bit", nullable: false),
                    PaymentMethodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentMethodKind = table.Column<byte>(type: "tinyint", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PostedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.CheckConstraint("CK_Payments_Amount", "[Amount] > 0");
                    table.CheckConstraint("CK_Payments_Direction", "[Direction] IN (1,2)");
                    table.CheckConstraint("CK_Payments_Method", "[PaymentMethodKind] = 4");
                    table.ForeignKey(
                        name: "FK_Payments_BusinessPartners_PartnerId",
                        column: x => x.PartnerId,
                        principalSchema: "catalog",
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payments_Lookups_PaymentMethodKind_PaymentMethodId",
                        columns: x => new { x.PaymentMethodKind, x.PaymentMethodId },
                        principalSchema: "catalog",
                        principalTable: "Lookups",
                        principalColumns: new[] { "Kind", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Fulfillments",
                schema: "orders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StockDocumentId = table.Column<int>(type: "int", nullable: false),
                    IsReturn = table.Column<bool>(type: "bit", nullable: false),
                    ReversesId = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fulfillments", x => x.Id);
                    table.CheckConstraint("CK_Fulfillments_Amount", "[Amount] >= 0");
                    table.ForeignKey(
                        name: "FK_Fulfillments_Documents_StockDocumentId",
                        column: x => x.StockDocumentId,
                        principalSchema: "inventory",
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Fulfillments_Fulfillments_ReversesId",
                        column: x => x.ReversesId,
                        principalSchema: "orders",
                        principalTable: "Fulfillments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Fulfillments_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "orders",
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Invoices",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StockDocumentId = table.Column<int>(type: "int", nullable: false),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    OrderNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Direction = table.Column<byte>(type: "tinyint", nullable: false),
                    IsCredit = table.Column<bool>(type: "bit", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                    CostAmount = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DueAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.Id);
                    table.CheckConstraint("CK_Invoices_Amounts", "[Amount] >= 0 AND [CostAmount] >= 0");
                    table.CheckConstraint("CK_Invoices_Dates", "[DueAtUtc] >= [IssuedAtUtc]");
                    table.CheckConstraint("CK_Invoices_Direction", "[Direction] IN (1,2)");
                    table.ForeignKey(
                        name: "FK_Invoices_BusinessPartners_PartnerId",
                        column: x => x.PartnerId,
                        principalSchema: "catalog",
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Invoices_Documents_StockDocumentId",
                        column: x => x.StockDocumentId,
                        principalSchema: "inventory",
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Invoices_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "orders",
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrderLines",
                schema: "orders",
                columns: table => new
                {
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sku = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    FulfilledQuantity = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    ReturnedQuantity = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    ReservedQuantity = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderLines", x => new { x.OrderId, x.ProductId });
                    table.CheckConstraint("CK_OrderLines_Amounts", "[Quantity] > 0 AND [UnitPrice] >= 0 AND [FulfilledQuantity] BETWEEN 0 AND [Quantity] AND [ReturnedQuantity] BETWEEN 0 AND [FulfilledQuantity] AND [ReservedQuantity] >= 0 AND [ReservedQuantity] <= [Quantity] - [FulfilledQuantity]");
                    table.ForeignKey(
                        name: "FK_OrderLines_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "orders",
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderLines_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "catalog",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Allocations",
                schema: "finance",
                columns: table => new
                {
                    PaymentId = table.Column<long>(type: "bigint", nullable: false),
                    InvoiceId = table.Column<long>(type: "bigint", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Allocations", x => new { x.PaymentId, x.InvoiceId });
                    table.CheckConstraint("CK_Allocations_Amount", "[Amount] > 0");
                    table.ForeignKey(
                        name: "FK_Allocations_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalSchema: "finance",
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Allocations_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalSchema: "finance",
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Allocations_InvoiceId",
                schema: "finance",
                table: "Allocations",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Fulfillments_OrderId",
                schema: "orders",
                table: "Fulfillments",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Fulfillments_RequestId",
                schema: "orders",
                table: "Fulfillments",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Fulfillments_ReversesId",
                schema: "orders",
                table: "Fulfillments",
                column: "ReversesId",
                unique: true,
                filter: "[ReversesId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Fulfillments_StockDocumentId",
                schema: "orders",
                table: "Fulfillments",
                column: "StockDocumentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Direction_PartnerId_DueAtUtc",
                schema: "finance",
                table: "Invoices",
                columns: new[] { "Direction", "PartnerId", "DueAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_OrderId",
                schema: "finance",
                table: "Invoices",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_PartnerId",
                schema: "finance",
                table: "Invoices",
                column: "PartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_StockDocumentId",
                schema: "finance",
                table: "Invoices",
                column: "StockDocumentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderLines_ProductId",
                schema: "orders",
                table: "OrderLines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Kind_PartnerId_Id",
                schema: "orders",
                table: "Orders",
                columns: new[] { "Kind", "PartnerId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_PartnerId",
                schema: "orders",
                table: "Orders",
                column: "PartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_RequestId",
                schema: "orders",
                table: "Orders",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_WarehouseKind_WarehouseId",
                schema: "orders",
                table: "Orders",
                columns: new[] { "WarehouseKind", "WarehouseId" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_PartnerId_PostedAtUtc",
                schema: "finance",
                table: "Payments",
                columns: new[] { "PartnerId", "PostedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_PaymentMethodKind_PaymentMethodId",
                schema: "finance",
                table: "Payments",
                columns: new[] { "PaymentMethodKind", "PaymentMethodId" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_RequestId",
                schema: "finance",
                table: "Payments",
                column: "RequestId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Allocations",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "Fulfillments",
                schema: "orders");

            migrationBuilder.DropTable(
                name: "OrderLines",
                schema: "orders");

            migrationBuilder.DropTable(
                name: "Invoices",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "Payments",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "Orders",
                schema: "orders");
        }
    }
}
