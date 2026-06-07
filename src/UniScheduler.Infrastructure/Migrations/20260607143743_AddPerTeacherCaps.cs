using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniScheduler.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPerTeacherCaps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LanguagePerTeacherCap",
                table: "SolverSettings",
                type: "integer",
                nullable: false,
                defaultValue: 15);

            migrationBuilder.AddColumn<int>(
                name: "PhysicalEducationPerTeacherCap",
                table: "SolverSettings",
                type: "integer",
                nullable: false,
                defaultValue: 40);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LanguagePerTeacherCap",
                table: "SolverSettings");

            migrationBuilder.DropColumn(
                name: "PhysicalEducationPerTeacherCap",
                table: "SolverSettings");
        }
    }
}
