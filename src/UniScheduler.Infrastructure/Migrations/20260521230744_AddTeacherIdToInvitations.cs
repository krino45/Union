using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using UniScheduler.Infrastructure.Persistence;

#nullable disable

namespace UniScheduler.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260521230744_AddTeacherIdToInvitations")]
    public partial class AddTeacherIdToInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TeacherId",
                table: "Invitations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_TeacherId",
                table: "Invitations",
                column: "TeacherId");

            migrationBuilder.AddForeignKey(
                name: "FK_Invitations_Teachers_TeacherId",
                table: "Invitations",
                column: "TeacherId",
                principalTable: "Teachers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invitations_Teachers_TeacherId",
                table: "Invitations");

            migrationBuilder.DropIndex(
                name: "IX_Invitations_TeacherId",
                table: "Invitations");

            migrationBuilder.DropColumn(
                name: "TeacherId",
                table: "Invitations");
        }
    }
}
