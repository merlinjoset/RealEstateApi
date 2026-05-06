using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RealEstateApi.Migrations
{
    /// <inheritdoc />
    public partial class AddSmsTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SmsTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Key = table.Column<string>(type: "text", nullable: false),
                    Label = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Body = table.Column<string>(type: "text", nullable: false),
                    AvailableVars = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmsTemplates", x => x.Id);
                });

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

            migrationBuilder.InsertData(
                table: "SmsTemplates",
                columns: new[] { "Id", "AvailableVars", "Body", "CreatedAt", "Description", "IsActive", "Key", "Label", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, "name,phone,propertyId", "Hi {name}, thank you for reaching Jose For Land. Our team will call you back within 2-5 hours. For urgent help, dial +91 99944 88490.", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Sent to the public visitor right after they submit an inquiry.", true, "inquiry.confirmation", "Inquiry — visitor confirmation", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, "name,phone,propertyContext", "📩 New inquiry from {name} ({phone}){propertyContext}. Check the admin panel.", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Notifies the admin team whenever a new inquiry comes in.", true, "inquiry.adminNotification", "Inquiry — admin alert", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3, "name,phone", "📨 New inquiry assigned to you: {name} ({phone}). Open the admin panel to review.", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Notifies an employee that an inquiry has been assigned to them.", true, "inquiry.assignment", "Inquiry — assigned to employee", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 4, "id,name,actor,prevStatus,newStatus,noteSuffix", "🔔 Inquiry #{id} ({name}) updated by {actor}: {prevStatus} → {newStatus}{noteSuffix}", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Notifies admins when an employee updates an inquiry status.", true, "inquiry.statusUpdate", "Inquiry — status changed", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 5, "name,title", "Hi {name}, thank you for submitting '{title}' on Jose For Land. Our team will review it within 24 hours and get back to you.", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Sent to a seller after they submit a property for review.", true, "property.submittedConfirmation", "Property — submitter confirmation", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 6, "title,priceLakhs,area,name,phone", "🏡 New property pending: '{title}' (₹{priceLakhs}L / {area} cents) from {name} ({phone})", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Notifies admins of a new property submission awaiting approval.", true, "property.adminPending", "Property — admin pending alert", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 7, "name,title", "🎉 Hi {name}, your property '{title}' is now LIVE on Jose For Land! Buyers can now view it. Visit joseforland.com to see it online.", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Sent to the seller when their property is approved by an admin.", true, "property.approved", "Property — approved (LIVE)", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 8, "name,title,reasonSuffix", "Hi {name}, your submission '{title}' could not be approved.{reasonSuffix} Call +91 99944 88490 for help.", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Sent to the seller if their property is rejected by an admin.", true, "property.rejected", "Property — rejected", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "$2a$11$sltgx8Vbt4eaAVoiL/8TReJ7uSaUS.it7/BeBk0n535lj9CXCTOwa");

            migrationBuilder.CreateIndex(
                name: "IX_SmsTemplates_Key",
                table: "SmsTemplates",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SmsTemplates");

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
                value: "$2a$11$LhaWkUZ/DGoDR3oFhUpT4eK2V9UZabzHNuwuvBQmUlLyVWC3juTI6");
        }
    }
}
