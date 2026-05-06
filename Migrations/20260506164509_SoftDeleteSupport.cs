using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstateApi.Migrations
{
    /// <inheritdoc />
    public partial class SoftDeleteSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Testimonials",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Testimonials",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "SmsTemplates",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "SmsTemplates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Properties",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Properties",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Inquiries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Inquiries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "Properties",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "DeletedAt", "Features", "Images", "IsDeleted", "NearbyLandmarks" },
                values: new object[] { null, new List<string> { "Road Access", "Clear Title", "Near Market" }, new List<string>(), false, new List<string> { "Nagercoil Railway Station (2 km)", "NH 44 (200 m)" } });

            migrationBuilder.UpdateData(
                table: "Properties",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "DeletedAt", "Features", "Images", "IsDeleted", "NearbyLandmarks" },
                values: new object[] { null, new List<string> { "Road Access", "Electricity", "Water Source" }, new List<string>(), false, new List<string> { "Marthandam Bus Stand (500 m)" } });

            migrationBuilder.UpdateData(
                table: "Properties",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "DeletedAt", "Features", "Images", "IsDeleted", "NearbyLandmarks" },
                values: new object[] { null, new List<string> { "Water Source", "Fertile Soil" }, new List<string>(), false, new List<string>() });

            migrationBuilder.UpdateData(
                table: "SmsTemplates",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "DeletedAt", "IsDeleted" },
                values: new object[] { null, false });

            migrationBuilder.UpdateData(
                table: "SmsTemplates",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "DeletedAt", "IsDeleted" },
                values: new object[] { null, false });

            migrationBuilder.UpdateData(
                table: "SmsTemplates",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "DeletedAt", "IsDeleted" },
                values: new object[] { null, false });

            migrationBuilder.UpdateData(
                table: "SmsTemplates",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "DeletedAt", "IsDeleted" },
                values: new object[] { null, false });

            migrationBuilder.UpdateData(
                table: "SmsTemplates",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "DeletedAt", "IsDeleted" },
                values: new object[] { null, false });

            migrationBuilder.UpdateData(
                table: "SmsTemplates",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "DeletedAt", "IsDeleted" },
                values: new object[] { null, false });

            migrationBuilder.UpdateData(
                table: "SmsTemplates",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "DeletedAt", "IsDeleted" },
                values: new object[] { null, false });

            migrationBuilder.UpdateData(
                table: "SmsTemplates",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "DeletedAt", "IsDeleted" },
                values: new object[] { null, false });

            migrationBuilder.UpdateData(
                table: "Testimonials",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "DeletedAt", "IsDeleted" },
                values: new object[] { null, false });

            migrationBuilder.UpdateData(
                table: "Testimonials",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "DeletedAt", "IsDeleted" },
                values: new object[] { null, false });

            migrationBuilder.UpdateData(
                table: "Testimonials",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "DeletedAt", "IsDeleted" },
                values: new object[] { null, false });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "DeletedAt", "IsDeleted", "PasswordHash" },
                values: new object[] { null, false, "$2a$11$pG.k4HEuYOSgbg2BlMJPUuCX.ChmZVVzSXYi2OnRRnB9lPCMDxKOu" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Testimonials");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Testimonials");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "SmsTemplates");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "SmsTemplates");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Inquiries");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
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
                value: "$2a$11$sltgx8Vbt4eaAVoiL/8TReJ7uSaUS.it7/BeBk0n535lj9CXCTOwa");
        }
    }
}
