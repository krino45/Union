using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniScheduler.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ParallelSessionsLanguageAndSubgroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowsSubgroups",
                table: "Subjects",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SubgroupCount",
                table: "Subjects",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "LanguageHours",
                table: "StudyPlanEntries",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<Guid>(
                name: "ParallelGroupId",
                table: "ScheduleEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubgroupLabel",
                table: "ScheduleEntries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDistributed",
                table: "Rooms",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowsSubgroups",
                table: "Subjects");

            migrationBuilder.DropColumn(
                name: "SubgroupCount",
                table: "Subjects");

            migrationBuilder.DropColumn(
                name: "LanguageHours",
                table: "StudyPlanEntries");

            migrationBuilder.DropColumn(
                name: "ParallelGroupId",
                table: "ScheduleEntries");

            migrationBuilder.DropColumn(
                name: "SubgroupLabel",
                table: "ScheduleEntries");

            migrationBuilder.DropColumn(
                name: "IsDistributed",
                table: "Rooms");
        }
    }
}
