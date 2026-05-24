using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniScheduler.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MultiTenancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── New tables ────────────────────────────────────────────────────────────

            migrationBuilder.CreateTable(
                name: "Universities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ShortName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    LogoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Universities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserUniversityAccesses",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UniversityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserUniversityAccesses", x => new { x.UserId, x.UniversityId });
                    table.ForeignKey(
                        name: "FK_UserUniversityAccesses_AppUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserUniversityAccesses_Universities_UniversityId",
                        column: x => x.UniversityId,
                        principalTable: "Universities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FloorPlanDrafts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BuildingId = table.Column<Guid>(type: "uuid", nullable: false),
                    DraftJson = table.Column<string>(type: "text", nullable: false),
                    LastModified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FloorPlanDrafts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FloorPlanDrafts_Buildings_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "Buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ── PairTimeSlots: replace PairNumber PK with Guid Id ────────────────────

            migrationBuilder.DropPrimaryKey(
                name: "PK_PairTimeSlots",
                table: "PairTimeSlots");

            // Remove old seeded rows (they have PairNumber as int PK and no UniversityId)
            for (int p = 1; p <= 7; p++)
                migrationBuilder.Sql($@"DELETE FROM ""PairTimeSlots"" WHERE ""PairNumber"" = {p};");

            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "PairTimeSlots",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<Guid>(
                name: "UniversityId",
                table: "PairTimeSlots",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddPrimaryKey(
                name: "PK_PairTimeSlots",
                table: "PairTimeSlots",
                column: "Id");

            // ── Add UniversityId to root-level tenant entities ────────────────────────

            migrationBuilder.AddColumn<Guid>(
                name: "UniversityId",
                table: "Faculties",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "UniversityId",
                table: "Buildings",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "UniversityId",
                table: "Teachers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "UniversityId",
                table: "CalendarPlans",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "UniversityId",
                table: "SolverSettings",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "UniversityId",
                table: "Schedules",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // ── Backfill: create default university and assign all existing rows ──────

            const string defaultUniversityId = "00000000-0000-0000-0000-000000000001";
            migrationBuilder.Sql($@"
                INSERT INTO ""Universities"" (""Id"", ""Name"", ""ShortName"")
                SELECT '{defaultUniversityId}', 'Университет', 'УН'
                WHERE NOT EXISTS (SELECT 1 FROM ""Universities"" WHERE ""Id"" = '{defaultUniversityId}');

                UPDATE ""Faculties""      SET ""UniversityId"" = '{defaultUniversityId}' WHERE ""UniversityId"" = '00000000-0000-0000-0000-000000000000';
                UPDATE ""Buildings""     SET ""UniversityId"" = '{defaultUniversityId}' WHERE ""UniversityId"" = '00000000-0000-0000-0000-000000000000';
                UPDATE ""Teachers""      SET ""UniversityId"" = '{defaultUniversityId}' WHERE ""UniversityId"" = '00000000-0000-0000-0000-000000000000';
                UPDATE ""CalendarPlans"" SET ""UniversityId"" = '{defaultUniversityId}' WHERE ""UniversityId"" = '00000000-0000-0000-0000-000000000000';
                UPDATE ""SolverSettings"" SET ""UniversityId"" = '{defaultUniversityId}' WHERE ""UniversityId"" = '00000000-0000-0000-0000-000000000000';
                UPDATE ""Schedules""     SET ""UniversityId"" = '{defaultUniversityId}' WHERE ""UniversityId"" = '00000000-0000-0000-0000-000000000000';
            ");

            // ── Drop old unique indexes (now per-university) ──────────────────────────

            migrationBuilder.DropIndex(
                name: "IX_Teachers_Email",
                table: "Teachers");

            migrationBuilder.DropIndex(
                name: "IX_Faculties_ShortCode",
                table: "Faculties");

            // ── New indexes ───────────────────────────────────────────────────────────

            migrationBuilder.CreateIndex(
                name: "IX_Universities_ShortName",
                table: "Universities",
                column: "ShortName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserUniversityAccesses_UniversityId",
                table: "UserUniversityAccesses",
                column: "UniversityId");

            migrationBuilder.CreateIndex(
                name: "IX_FloorPlanDrafts_BuildingId",
                table: "FloorPlanDrafts",
                column: "BuildingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PairTimeSlots_UniversityId_PairNumber",
                table: "PairTimeSlots",
                columns: new[] { "UniversityId", "PairNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Faculties_UniversityId_ShortCode",
                table: "Faculties",
                columns: new[] { "UniversityId", "ShortCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Teachers_UniversityId_Email",
                table: "Teachers",
                columns: new[] { "UniversityId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Buildings_UniversityId",
                table: "Buildings",
                column: "UniversityId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarPlans_UniversityId",
                table: "CalendarPlans",
                column: "UniversityId");

            migrationBuilder.CreateIndex(
                name: "IX_SolverSettings_UniversityId",
                table: "SolverSettings",
                column: "UniversityId");

            migrationBuilder.CreateIndex(
                name: "IX_Schedules_UniversityId",
                table: "Schedules",
                column: "UniversityId");

            // ── Foreign keys ──────────────────────────────────────────────────────────

            migrationBuilder.AddForeignKey(
                name: "FK_Faculties_Universities_UniversityId",
                table: "Faculties",
                column: "UniversityId",
                principalTable: "Universities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Buildings_Universities_UniversityId",
                table: "Buildings",
                column: "UniversityId",
                principalTable: "Universities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Teachers_Universities_UniversityId",
                table: "Teachers",
                column: "UniversityId",
                principalTable: "Universities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CalendarPlans_Universities_UniversityId",
                table: "CalendarPlans",
                column: "UniversityId",
                principalTable: "Universities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PairTimeSlots_Universities_UniversityId",
                table: "PairTimeSlots",
                column: "UniversityId",
                principalTable: "Universities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SolverSettings_Universities_UniversityId",
                table: "SolverSettings",
                column: "UniversityId",
                principalTable: "Universities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Schedules_Universities_UniversityId",
                table: "Schedules",
                column: "UniversityId",
                principalTable: "Universities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_Faculties_Universities_UniversityId", table: "Faculties");
            migrationBuilder.DropForeignKey(name: "FK_Buildings_Universities_UniversityId", table: "Buildings");
            migrationBuilder.DropForeignKey(name: "FK_Teachers_Universities_UniversityId", table: "Teachers");
            migrationBuilder.DropForeignKey(name: "FK_CalendarPlans_Universities_UniversityId", table: "CalendarPlans");
            migrationBuilder.DropForeignKey(name: "FK_PairTimeSlots_Universities_UniversityId", table: "PairTimeSlots");
            migrationBuilder.DropForeignKey(name: "FK_SolverSettings_Universities_UniversityId", table: "SolverSettings");
            migrationBuilder.DropForeignKey(name: "FK_Schedules_Universities_UniversityId", table: "Schedules");

            migrationBuilder.DropTable(name: "FloorPlanDrafts");
            migrationBuilder.DropTable(name: "UserUniversityAccesses");
            migrationBuilder.DropTable(name: "Universities");

            migrationBuilder.DropIndex(name: "IX_Teachers_UniversityId_Email", table: "Teachers");
            migrationBuilder.DropIndex(name: "IX_Faculties_UniversityId_ShortCode", table: "Faculties");
            migrationBuilder.DropIndex(name: "IX_PairTimeSlots_UniversityId_PairNumber", table: "PairTimeSlots");
            migrationBuilder.DropIndex(name: "IX_Buildings_UniversityId", table: "Buildings");
            migrationBuilder.DropIndex(name: "IX_CalendarPlans_UniversityId", table: "CalendarPlans");
            migrationBuilder.DropIndex(name: "IX_SolverSettings_UniversityId", table: "SolverSettings");
            migrationBuilder.DropIndex(name: "IX_Schedules_UniversityId", table: "Schedules");

            migrationBuilder.DropPrimaryKey(name: "PK_PairTimeSlots", table: "PairTimeSlots");
            migrationBuilder.DropColumn(name: "Id", table: "PairTimeSlots");
            migrationBuilder.DropColumn(name: "UniversityId", table: "PairTimeSlots");
            migrationBuilder.DropColumn(name: "UniversityId", table: "Faculties");
            migrationBuilder.DropColumn(name: "UniversityId", table: "Buildings");
            migrationBuilder.DropColumn(name: "UniversityId", table: "Teachers");
            migrationBuilder.DropColumn(name: "UniversityId", table: "CalendarPlans");
            migrationBuilder.DropColumn(name: "UniversityId", table: "SolverSettings");
            migrationBuilder.DropColumn(name: "UniversityId", table: "Schedules");

            migrationBuilder.AddPrimaryKey(name: "PK_PairTimeSlots", table: "PairTimeSlots", column: "PairNumber");

            migrationBuilder.CreateIndex(name: "IX_Teachers_Email", table: "Teachers", column: "Email", unique: true);
            migrationBuilder.CreateIndex(name: "IX_Faculties_ShortCode", table: "Faculties", column: "ShortCode", unique: true);
        }
    }
}
