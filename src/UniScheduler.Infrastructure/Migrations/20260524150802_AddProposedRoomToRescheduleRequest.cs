using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniScheduler.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProposedRoomToRescheduleRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ProposedIsOnline",
                table: "RescheduleRequests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ProposedRoomId",
                table: "RescheduleRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RescheduleRequests_ProposedRoomId",
                table: "RescheduleRequests",
                column: "ProposedRoomId");

            migrationBuilder.AddForeignKey(
                name: "FK_RescheduleRequests_Rooms_ProposedRoomId",
                table: "RescheduleRequests",
                column: "ProposedRoomId",
                principalTable: "Rooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RescheduleRequests_Rooms_ProposedRoomId",
                table: "RescheduleRequests");

            migrationBuilder.DropIndex(
                name: "IX_RescheduleRequests_ProposedRoomId",
                table: "RescheduleRequests");

            migrationBuilder.DropColumn(
                name: "ProposedIsOnline",
                table: "RescheduleRequests");

            migrationBuilder.DropColumn(
                name: "ProposedRoomId",
                table: "RescheduleRequests");
        }
    }
}
