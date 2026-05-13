using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstateApi.Migrations
{
    /// <inheritdoc />
    public partial class PropertyVerificationAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AssignedToVerifyAt",
                table: "Properties",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AssignedToVerifyUserId",
                table: "Properties",
                type: "integer",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Properties",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "AssignedToVerifyAt", "AssignedToVerifyUserId", "Features", "Images", "NearbyLandmarks" },
                values: new object[] { null, null, new List<string> { "Road Access", "Clear Title", "Near Market" }, new List<string>(), new List<string> { "Nagercoil Railway Station (2 km)", "NH 44 (200 m)" } });

            migrationBuilder.UpdateData(
                table: "Properties",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "AssignedToVerifyAt", "AssignedToVerifyUserId", "Features", "Images", "NearbyLandmarks" },
                values: new object[] { null, null, new List<string> { "Road Access", "Electricity", "Water Source" }, new List<string>(), new List<string> { "Marthandam Bus Stand (500 m)" } });

            migrationBuilder.UpdateData(
                table: "Properties",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "AssignedToVerifyAt", "AssignedToVerifyUserId", "Features", "Images", "NearbyLandmarks" },
                values: new object[] { null, null, new List<string> { "Water Source", "Fertile Soil" }, new List<string>(), new List<string>() });

            migrationBuilder.InsertData(
                table: "SmsTemplates",
                columns: new[] { "Id", "AvailableVars", "Body", "CreatedAt", "DeletedAt", "Description", "IsActive", "IsDeleted", "Key", "Label", "UpdatedAt" },
                values: new object[] { 11, "assigneeFirstName,title,city,submitter,submitterPhone,propertyId", "🏷 Hi {assigneeFirstName}, you've been assigned property #{propertyId} '{title}' in {city} to verify. Seller: {submitter} ({submitterPhone}). Please schedule a site visit.", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Sent to an Employee/Agent when an admin assigns them a property to verify.", true, false, "property.assignedForVerification", "Property — verification assignment", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "$2a$11$LBgzWzJGB1/gW70VKLED7e0UdNUj6Hy9qOcuMOpDGb7tPZGIto932");

            migrationBuilder.CreateIndex(
                name: "IX_Properties_AssignedToVerifyUserId",
                table: "Properties",
                column: "AssignedToVerifyUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Properties_Users_AssignedToVerifyUserId",
                table: "Properties",
                column: "AssignedToVerifyUserId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Properties_Users_AssignedToVerifyUserId",
                table: "Properties");

            migrationBuilder.DropIndex(
                name: "IX_Properties_AssignedToVerifyUserId",
                table: "Properties");

            migrationBuilder.DeleteData(
                table: "SmsTemplates",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DropColumn(
                name: "AssignedToVerifyAt",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "AssignedToVerifyUserId",
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
                value: "$2a$11$2ueApk8esfRLDYUZtISOIOjhAUfVU1IKSY2/UOpbJP8Jla9rTlT92");
        }
    }
}
