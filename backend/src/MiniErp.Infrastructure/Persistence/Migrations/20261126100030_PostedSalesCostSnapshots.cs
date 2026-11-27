using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PostedSalesCostSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PostedSalesCost",
                schema: "inventory",
                table: "DocumentLines",
                type: "decimal(19,2)",
                precision: 19,
                scale: 2,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_StockLines_PostedSalesCost",
                schema: "inventory",
                table: "DocumentLines",
                sql: "[PostedSalesCost] IS NULL OR [PostedSalesCost] >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_StockLines_PostedSalesCost",
                schema: "inventory",
                table: "DocumentLines");

            migrationBuilder.DropColumn(
                name: "PostedSalesCost",
                schema: "inventory",
                table: "DocumentLines");
        }
    }
}
