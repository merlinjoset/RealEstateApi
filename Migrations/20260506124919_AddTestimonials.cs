using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RealEstateApi.Migrations
{
    /// <inheritdoc />
    public partial class AddTestimonials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Testimonials",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Location = table.Column<string>(type: "text", nullable: false),
                    PropertyDetail = table.Column<string>(type: "text", nullable: true),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    Excerpt = table.Column<string>(type: "text", nullable: false),
                    Thumbnail = table.Column<string>(type: "text", nullable: true),
                    VideoUrl = table.Column<string>(type: "text", nullable: true),
                    Duration = table.Column<string>(type: "text", nullable: true),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Testimonials", x => x.Id);
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
                table: "Testimonials",
                columns: new[] { "Id", "CreatedAt", "Duration", "Excerpt", "IsPublished", "Location", "Name", "Order", "PropertyDetail", "Rating", "Thumbnail", "UpdatedAt", "VideoUrl" },
                values: new object[,]
                {
                    { 1, new DateTime(2024, 1, 12, 0, 0, 0, 0, DateTimeKind.Utc), "1:24", "They visited the site with us, explained every document, and stayed honest throughout. Bought my first land through Jose For Land — no regrets.", true, "Nagercoil", "Rajan Kumar", 1, "15 cents · Open Land", 5, "https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=900&q=80&auto=format&fit=crop", new DateTime(2024, 1, 12, 0, 0, 0, 0, DateTimeKind.Utc), "https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/BigBuckBunny.mp4" },
                    { 2, new DateTime(2024, 1, 18, 0, 0, 0, 0, DateTimeKind.Utc), "0:58", "The free doorstep consultation is real — they came to our village, walked the plot with us, and answered every question. Genuine team.", true, "Marthandam", "Priya Selvam", 2, "10 cents · Residential Plot", 5, "https://images.unsplash.com/photo-1464082354059-27db6ce50048?w=900&q=80&auto=format&fit=crop", new DateTime(2024, 1, 18, 0, 0, 0, 0, DateTimeKind.Utc), "https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/ElephantsDream.mp4" },
                    { 3, new DateTime(2024, 1, 22, 0, 0, 0, 0, DateTimeKind.Utc), "1:42", "Compared with three other agents — Jose For Land had the cleanest documentation and the most transparent pricing. Highly recommend.", true, "Colachel", "Xavier Joseph", 3, "50 cents · Agricultural", 5, "https://images.unsplash.com/photo-1500076656116-558758c991c1?w=900&q=80&auto=format&fit=crop", new DateTime(2024, 1, 22, 0, 0, 0, 0, DateTimeKind.Utc), "https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/ForBiggerBlazes.mp4" }
                });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "$2a$11$OU.pu52vrSTcK023aupeluP8Hd6UdGGlqCuP2OJlZo7iKerxA9elK");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Testimonials");

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
                value: "$2a$11$enRuypI2TqOAzhOJbqZLZOojpiYp116sEvaKeVHcq3ztJhEHt9qZG");
        }
    }
}
