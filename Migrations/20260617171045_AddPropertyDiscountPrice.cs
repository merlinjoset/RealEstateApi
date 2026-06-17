using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstateApi.Migrations
{
    /// <inheritdoc />
    public partial class AddPropertyDiscountPrice : Migration
    {
        // Purely additive: adds the nullable Properties.DiscountPrice column.
        // The seed-data UpdateData calls EF auto-scaffolds (a BCrypt random-salt
        // artifact + List re-detection) were removed so applying this on
        // production can't rewrite seed rows or reset the admin password.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DiscountPrice",
                table: "Properties",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscountPrice",
                table: "Properties");
        }
    }
}
