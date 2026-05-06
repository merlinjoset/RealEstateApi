using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstateApi.Migrations
{
    /// <inheritdoc />
    public partial class PropertySubmitterContact : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SubmitterEmail",
                table: "Properties",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubmitterName",
                table: "Properties",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubmitterPhone",
                table: "Properties",
                type: "text",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Properties",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Features", "Images", "NearbyLandmarks", "SubmitterEmail", "SubmitterName", "SubmitterPhone" },
                values: new object[] { new List<string> { "Road Access", "Clear Title", "Near Market" }, new List<string>(), new List<string> { "Nagercoil Railway Station (2 km)", "NH 44 (200 m)" }, null, null, null });

            migrationBuilder.UpdateData(
                table: "Properties",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Features", "Images", "NearbyLandmarks", "SubmitterEmail", "SubmitterName", "SubmitterPhone" },
                values: new object[] { new List<string> { "Road Access", "Electricity", "Water Source" }, new List<string>(), new List<string> { "Marthandam Bus Stand (500 m)" }, null, null, null });

            migrationBuilder.UpdateData(
                table: "Properties",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "Features", "Images", "NearbyLandmarks", "SubmitterEmail", "SubmitterName", "SubmitterPhone" },
                values: new object[] { new List<string> { "Water Source", "Fertile Soil" }, new List<string>(), new List<string>(), null, null, null });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "$2a$11$LhaWkUZ/DGoDR3oFhUpT4eK2V9UZabzHNuwuvBQmUlLyVWC3juTI6");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubmitterEmail",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "SubmitterName",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "SubmitterPhone",
                table: "Properties");

            migrationBuilder.UpdateData(
                table: "Properties",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Features", "Images", "NearbyLandmarks" },
                values: new object[] { new List<string> { "Road Access", "Clear Title", "Near Market" }, new List<string>(), new List<string> { "Nagercoil Railway Station (2 km)", "NH 44 (200 m)" } });

            migrationBuilder.UpdateData(
                table: "Properties",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Features", "Images", "NearbyLandmarks" },
                values: new object[] { new List<string> { "Road Access", "Electricity", "Water Source" }, new List<string>(), new List<string> { "Marthandam Bus Stand (500 m)" } });

            migrationBuilder.UpdateData(
                table: "Properties",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "Features", "Images", "NearbyLandmarks" },
                values: new object[] { new List<string> { "Water Source", "Fertile Soil" }, new List<string>(), new List<string>() });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "$2a$11$OjRAgVzKMw2rq89nnplIJOok42e0vNJ5m.yu8mKEGxTIdAONdkTTi");
        }
    }
}
