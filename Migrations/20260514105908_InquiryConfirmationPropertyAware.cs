using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstateApi.Migrations
{
    /// <inheritdoc />
    public partial class InquiryConfirmationPropertyAware : Migration
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
                columns: new[] { "AvailableVars", "Body", "Description" },
                values: new object[] { "name,phone,propertyId,propertyTitle,propertyContext", "Hi {name}, thank you for reaching Jose For Land{propertyContext}. Our team will call you back within 2-5 hours. For urgent help, dial +91 99944 88490.", "Sent to the public visitor right after they submit an inquiry. Includes the property title when the inquiry was attached to a listing." });

            migrationBuilder.InsertData(
                table: "SmsTemplates",
                columns: new[] { "Id", "AvailableVars", "Body", "CreatedAt", "DeletedAt", "Description", "IsActive", "IsDeleted", "Key", "Label", "UpdatedAt" },
                values: new object[] { 13, "name,phone,propertyId,propertyTitle,propertyContext", "Hi {name}, your request for documents{propertyContext} has been received. Our team will share EC / Patta / Chitta copies within 2-5 hours. Urgent? Call +91 99944 88490.", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Sent to the visitor right after they click 'Request to view documents' on a property — calls out that it's a document request and which property it's for.", true, false, "inquiry.documentRequestConfirmation", "Inquiry — document request visitor confirmation", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "$2a$11$Opgu4SLvHpcA7PeiVkCQnOWlc1Qxa.bWb3SRcqiS4XEZTcNVF/vRC");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "SmsTemplates",
                keyColumn: "Id",
                keyValue: 13);

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
                columns: new[] { "AvailableVars", "Body", "Description" },
                values: new object[] { "name,phone,propertyId", "Hi {name}, thank you for reaching Jose For Land. Our team will call you back within 2-5 hours. For urgent help, dial +91 99944 88490.", "Sent to the public visitor right after they submit an inquiry." });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "$2a$11$gewKTjKF4snuPYCOVZcYi.QzTdouh.Ik0QUc3ddcachY/QvwQBI5.");
        }
    }
}
