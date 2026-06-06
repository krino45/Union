using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniScheduler.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDraftsOwnershipAndFloorPlansAndInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing drafts have no owner - wipe them before adding the OwnerUserId FK.
            migrationBuilder.Sql("DELETE FROM \"FloorPlanDrafts\";");

            migrationBuilder.DropIndex(
                name: "IX_FloorPlanDrafts_BuildingId",
                table: "FloorPlanDrafts");

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "Universities",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOpenToAdmins",
                table: "Schedules",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Schedules",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerUserId",
                table: "Schedules",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOpenToAdmins",
                table: "FloorPlanDrafts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "FloorPlanDrafts",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerUserId",
                table: "FloorPlanDrafts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "FloorPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BuildingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FloorPlanJson = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FloorPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FloorPlans_AppUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_FloorPlans_Buildings_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "Buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Invitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    UniversityId = table.Column<Guid>(type: "uuid", nullable: false),
                    UniversityRole = table.Column<int>(type: "integer", nullable: false),
                    SystemRole = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    OtpHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    InvitedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConsumedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invitations_AppUsers_ConsumedByUserId",
                        column: x => x.ConsumedByUserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Invitations_AppUsers_InvitedByUserId",
                        column: x => x.InvitedByUserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Invitations_Universities_UniversityId",
                        column: x => x.UniversityId,
                        principalTable: "Universities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Schedules_OwnerUserId",
                table: "Schedules",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FloorPlanDrafts_BuildingId_OwnerUserId",
                table: "FloorPlanDrafts",
                columns: new[] { "BuildingId", "OwnerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_FloorPlanDrafts_OwnerUserId",
                table: "FloorPlanDrafts",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FloorPlans_BuildingId",
                table: "FloorPlans",
                column: "BuildingId",
                unique: true,
                filter: "\"IsActive\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_FloorPlans_CreatedByUserId",
                table: "FloorPlans",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_ConsumedByUserId",
                table: "Invitations",
                column: "ConsumedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_Email",
                table: "Invitations",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_InvitedByUserId",
                table: "Invitations",
                column: "InvitedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_OtpHash",
                table: "Invitations",
                column: "OtpHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_UniversityId",
                table: "Invitations",
                column: "UniversityId");

            migrationBuilder.AddForeignKey(
                name: "FK_FloorPlanDrafts_AppUsers_OwnerUserId",
                table: "FloorPlanDrafts",
                column: "OwnerUserId",
                principalTable: "AppUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Schedules_AppUsers_OwnerUserId",
                table: "Schedules",
                column: "OwnerUserId",
                principalTable: "AppUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FloorPlanDrafts_AppUsers_OwnerUserId",
                table: "FloorPlanDrafts");

            migrationBuilder.DropForeignKey(
                name: "FK_Schedules_AppUsers_OwnerUserId",
                table: "Schedules");

            migrationBuilder.DropTable(
                name: "FloorPlans");

            migrationBuilder.DropTable(
                name: "Invitations");

            migrationBuilder.DropIndex(
                name: "IX_Schedules_OwnerUserId",
                table: "Schedules");

            migrationBuilder.DropIndex(
                name: "IX_FloorPlanDrafts_BuildingId_OwnerUserId",
                table: "FloorPlanDrafts");

            migrationBuilder.DropIndex(
                name: "IX_FloorPlanDrafts_OwnerUserId",
                table: "FloorPlanDrafts");

            migrationBuilder.DropColumn(
                name: "City",
                table: "Universities");

            migrationBuilder.DropColumn(
                name: "IsOpenToAdmins",
                table: "Schedules");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "Schedules");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "Schedules");

            migrationBuilder.DropColumn(
                name: "IsOpenToAdmins",
                table: "FloorPlanDrafts");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "FloorPlanDrafts");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "FloorPlanDrafts");

            migrationBuilder.CreateIndex(
                name: "IX_FloorPlanDrafts_BuildingId",
                table: "FloorPlanDrafts",
                column: "BuildingId",
                unique: true);
        }
    }
}
