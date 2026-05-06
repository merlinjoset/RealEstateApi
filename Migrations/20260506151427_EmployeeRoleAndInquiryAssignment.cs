using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstateApi.Migrations
{
    /// <inheritdoc />
    public partial class EmployeeRoleAndInquiryAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Data migration: rename existing "Buyer" role to "Employee" ──
            migrationBuilder.Sql("UPDATE \"Users\" SET \"Role\" = 'Employee' WHERE \"Role\" = 'Buyer';");

            migrationBuilder.AddColumn<DateTime>(
                name: "AssignedAt",
                table: "Inquiries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AssignedByUserId",
                table: "Inquiries",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AssignedToUserId",
                table: "Inquiries",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastUpdatedAt",
                table: "Inquiries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LastUpdatedByUserId",
                table: "Inquiries",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Inquiries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Inquiries",
                type: "text",
                nullable: false,
                defaultValue: "");

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

            migrationBuilder.CreateIndex(
                name: "IX_Inquiries_AssignedToUserId",
                table: "Inquiries",
                column: "AssignedToUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Inquiries_Users_AssignedToUserId",
                table: "Inquiries",
                column: "AssignedToUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Inquiries_Users_AssignedToUserId",
                table: "Inquiries");

            migrationBuilder.DropIndex(
                name: "IX_Inquiries_AssignedToUserId",
                table: "Inquiries");

            migrationBuilder.DropColumn(
                name: "AssignedAt",
                table: "Inquiries");

            migrationBuilder.DropColumn(
                name: "AssignedByUserId",
                table: "Inquiries");

            migrationBuilder.DropColumn(
                name: "AssignedToUserId",
                table: "Inquiries");

            migrationBuilder.DropColumn(
                name: "LastUpdatedAt",
                table: "Inquiries");

            migrationBuilder.DropColumn(
                name: "LastUpdatedByUserId",
                table: "Inquiries");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Inquiries");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Inquiries");

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
                value: "$2a$11$OU.pu52vrSTcK023aupeluP8Hd6UdGGlqCuP2OJlZo7iKerxA9elK");
        }
    }
}
