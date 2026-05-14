using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstateApi.Migrations
{
    /// <inheritdoc />
    public partial class SmsTemplatesDropPlusPrefix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                table: "SmsTemplates",
                keyColumn: "Id",
                keyValue: 1,
                column: "Body",
                value: "Hi {name}, thank you for reaching Jose For Land{propertyContext}. Our team will call you back within 2-5 hours. For urgent help, dial 99944 88490.");

            migrationBuilder.UpdateData(
                table: "SmsTemplates",
                keyColumn: "Id",
                keyValue: 8,
                column: "Body",
                value: "Hi {name}, your submission '{title}' could not be approved.{reasonSuffix} Call 99944 88490 for help.");

            migrationBuilder.UpdateData(
                table: "SmsTemplates",
                keyColumn: "Id",
                keyValue: 13,
                column: "Body",
                value: "Hi {name}, your request for documents{propertyContext} has been received. Our team will share EC / Patta / Chitta copies within 2-5 hours. Urgent? Call 99944 88490.");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "$2a$11$Tf3HfYhJNSeXVLddTWYVZ.hWQQZtOOGMNm85P6RcoCTUhzQcVWw7S");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
                table: "SmsTemplates",
                keyColumn: "Id",
                keyValue: 1,
                column: "Body",
                value: "Hi {name}, thank you for reaching Jose For Land{propertyContext}. Our team will call you back within 2-5 hours. For urgent help, dial +91 99944 88490.");

            migrationBuilder.UpdateData(
                table: "SmsTemplates",
                keyColumn: "Id",
                keyValue: 8,
                column: "Body",
                value: "Hi {name}, your submission '{title}' could not be approved.{reasonSuffix} Call +91 99944 88490 for help.");

            migrationBuilder.UpdateData(
                table: "SmsTemplates",
                keyColumn: "Id",
                keyValue: 13,
                column: "Body",
                value: "Hi {name}, your request for documents{propertyContext} has been received. Our team will share EC / Patta / Chitta copies within 2-5 hours. Urgent? Call +91 99944 88490.");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "$2a$11$Opgu4SLvHpcA7PeiVkCQnOWlc1Qxa.bWb3SRcqiS4XEZTcNVF/vRC");
        }
    }
}
