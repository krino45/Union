using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace UniScheduler.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPairTimeSlots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PairTimeSlots",
                columns: table => new
                {
                    PairNumber = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PairTimeSlots", x => x.PairNumber);
                });

            migrationBuilder.InsertData(
                table: "PairTimeSlots",
                columns: new[] { "PairNumber", "EndTime", "StartTime" },
                values: new object[,]
                {
                    { 1, new TimeOnly(9, 35, 0), new TimeOnly(8, 0, 0) },
                    { 2, new TimeOnly(11, 25, 0), new TimeOnly(9, 50, 0) },
                    { 3, new TimeOnly(13, 15, 0), new TimeOnly(11, 40, 0) },
                    { 4, new TimeOnly(15, 20, 0), new TimeOnly(13, 45, 0) },
                    { 5, new TimeOnly(17, 10, 0), new TimeOnly(15, 35, 0) },
                    { 6, new TimeOnly(19, 0, 0), new TimeOnly(17, 25, 0) },
                    { 7, new TimeOnly(20, 50, 0), new TimeOnly(19, 15, 0) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PairTimeSlots");
        }
    }
}
