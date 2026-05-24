using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniScheduler.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEntranceConnections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EntranceConnections",
                columns: table => new
                {
                    FromNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToBuildingId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromBuildingId = table.Column<Guid>(type: "uuid", nullable: false),
                    DistanceMeters = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntranceConnections", x => new { x.FromNodeId, x.ToBuildingId });
                    table.ForeignKey(
                        name: "FK_EntranceConnections_Buildings_FromBuildingId",
                        column: x => x.FromBuildingId,
                        principalTable: "Buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EntranceConnections_Buildings_ToBuildingId",
                        column: x => x.ToBuildingId,
                        principalTable: "Buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EntranceConnections_FloorPlanNodes_FromNodeId",
                        column: x => x.FromNodeId,
                        principalTable: "FloorPlanNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EntranceConnections_FromBuildingId",
                table: "EntranceConnections",
                column: "FromBuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_EntranceConnections_ToBuildingId",
                table: "EntranceConnections",
                column: "ToBuildingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EntranceConnections");
        }
    }
}
