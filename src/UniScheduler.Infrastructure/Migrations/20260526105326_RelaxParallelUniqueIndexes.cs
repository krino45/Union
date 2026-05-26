using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniScheduler.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RelaxParallelUniqueIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ScheduleEntries_ScheduleId_RoomId_DayOfWeek_PairNumber_Week~",
                table: "ScheduleEntries");

            migrationBuilder.DropIndex(
                name: "IX_ScheduleEntries_ScheduleId_TeacherId_DayOfWeek_PairNumber_W~",
                table: "ScheduleEntries");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleEntries_ScheduleId_RoomId_DayOfWeek_PairNumber_Week~",
                table: "ScheduleEntries",
                columns: new[] { "ScheduleId", "RoomId", "DayOfWeek", "PairNumber", "WeekType" },
                unique: true,
                filter: "\"RoomId\" IS NOT NULL AND \"ParallelGroupId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleEntries_ScheduleId_TeacherId_DayOfWeek_PairNumber_W~",
                table: "ScheduleEntries",
                columns: new[] { "ScheduleId", "TeacherId", "DayOfWeek", "PairNumber", "WeekType" },
                unique: true,
                filter: "\"ParallelGroupId\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ScheduleEntries_ScheduleId_RoomId_DayOfWeek_PairNumber_Week~",
                table: "ScheduleEntries");

            migrationBuilder.DropIndex(
                name: "IX_ScheduleEntries_ScheduleId_TeacherId_DayOfWeek_PairNumber_W~",
                table: "ScheduleEntries");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleEntries_ScheduleId_RoomId_DayOfWeek_PairNumber_Week~",
                table: "ScheduleEntries",
                columns: new[] { "ScheduleId", "RoomId", "DayOfWeek", "PairNumber", "WeekType" },
                unique: true,
                filter: "\"RoomId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleEntries_ScheduleId_TeacherId_DayOfWeek_PairNumber_W~",
                table: "ScheduleEntries",
                columns: new[] { "ScheduleId", "TeacherId", "DayOfWeek", "PairNumber", "WeekType" },
                unique: true);
        }
    }
}
