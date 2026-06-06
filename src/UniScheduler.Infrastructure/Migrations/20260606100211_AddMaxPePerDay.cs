using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniScheduler.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMaxPePerDay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxPePerDay",
                table: "SolverSettings",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxPePerDay",
                table: "SolverSettings");
        }
    }
}
