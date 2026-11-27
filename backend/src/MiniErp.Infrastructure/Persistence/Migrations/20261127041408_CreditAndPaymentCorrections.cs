using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreditAndPaymentCorrections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaymentReversals",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PaymentId = table.Column<long>(type: "bigint", nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PostedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentReversals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentReversals_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalSchema: "finance",
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentReversals_PaymentId",
                schema: "finance",
                table: "PaymentReversals",
                column: "PaymentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentReversals_RequestId",
                schema: "finance",
                table: "PaymentReversals",
                column: "RequestId",
                unique: true);

            migrationBuilder.Sql("""
                CREATE VIEW finance.PaymentEvents AS
                SELECT p.Id PaymentId,p.Id EventId,p.PartnerId,p.Direction,p.IsRefund,
                       CAST(0 AS bit) IsReversal,p.Amount,p.PostedAtUtc
                FROM finance.Payments p
                UNION ALL
                SELECT p.Id,r.Id,p.PartnerId,p.Direction,p.IsRefund,
                       CAST(1 AS bit),-p.Amount,r.PostedAtUtc
                FROM finance.PaymentReversals r JOIN finance.Payments p ON p.Id=r.PaymentId
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW finance.PaymentEvents");
            migrationBuilder.DropTable(
                name: "PaymentReversals",
                schema: "finance");
        }
    }
}
