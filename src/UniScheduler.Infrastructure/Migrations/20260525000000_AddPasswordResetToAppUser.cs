using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using UniScheduler.Infrastructure.Persistence;

#nullable disable

namespace UniScheduler.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260525000000_AddPasswordResetToAppUser")]
    public partial class AddPasswordResetToAppUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PasswordResetTokenHash",
                table: "AppUsers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PasswordResetExpiresAt",
                table: "AppUsers",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PasswordResetTokenHash",
                table: "AppUsers");

            migrationBuilder.DropColumn(
                name: "PasswordResetExpiresAt",
                table: "AppUsers");
        }
    }
}
