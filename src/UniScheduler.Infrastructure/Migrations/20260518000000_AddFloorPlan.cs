using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using UniScheduler.Infrastructure.Persistence;

#nullable disable

namespace UniScheduler.Infrastructure.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260518000000_AddFloorPlan")]
    public partial class AddFloorPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Replace StairsDistancePerFloor with NumberOfFloors + NumberOfBasementFloors on Buildings
            migrationBuilder.DropColumn(name: "StairsDistancePerFloor", table: "Buildings");
            migrationBuilder.AddColumn<int>(name: "NumberOfFloors", table: "Buildings",
                nullable: false, defaultValue: 5);
            migrationBuilder.AddColumn<int>(name: "NumberOfBasementFloors", table: "Buildings",
                nullable: false, defaultValue: 0);

            // Remove DistanceFromStairsMeters from Rooms
            migrationBuilder.DropColumn(name: "DistanceFromStairsMeters", table: "Rooms");

            // Create FloorPlanNodes table
            migrationBuilder.CreateTable(
                name: "FloorPlanNodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    BuildingId = table.Column<Guid>(nullable: false),
                    Floor = table.Column<int>(nullable: false),
                    X = table.Column<double>(nullable: false),
                    Y = table.Column<double>(nullable: false),
                    NodeType = table.Column<int>(nullable: false),
                    RoomId = table.Column<Guid>(nullable: true),
                    Label = table.Column<string>(maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FloorPlanNodes", x => x.Id);
                    table.ForeignKey(name: "FK_FloorPlanNodes_Buildings_BuildingId",
                        column: x => x.BuildingId, principalTable: "Buildings",
                        principalColumn: "Id", onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(name: "FK_FloorPlanNodes_Rooms_RoomId",
                        column: x => x.RoomId, principalTable: "Rooms",
                        principalColumn: "Id", onDelete: ReferentialAction.SetNull);
                });

            // Create FloorPlanEdges table
            migrationBuilder.CreateTable(
                name: "FloorPlanEdges",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    BuildingId = table.Column<Guid>(nullable: false),
                    FromNodeId = table.Column<Guid>(nullable: false),
                    ToNodeId = table.Column<Guid>(nullable: false),
                    DistanceMeters = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FloorPlanEdges", x => x.Id);
                    table.ForeignKey(name: "FK_FloorPlanEdges_Buildings_BuildingId",
                        column: x => x.BuildingId, principalTable: "Buildings",
                        principalColumn: "Id", onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(name: "FK_FloorPlanEdges_FloorPlanNodes_FromNodeId",
                        column: x => x.FromNodeId, principalTable: "FloorPlanNodes",
                        principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(name: "FK_FloorPlanEdges_FloorPlanNodes_ToNodeId",
                        column: x => x.ToNodeId, principalTable: "FloorPlanNodes",
                        principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(name: "IX_FloorPlanNodes_BuildingId",
                table: "FloorPlanNodes", column: "BuildingId");
            migrationBuilder.CreateIndex(name: "IX_FloorPlanNodes_RoomId",
                table: "FloorPlanNodes", column: "RoomId");
            migrationBuilder.CreateIndex(name: "IX_FloorPlanEdges_BuildingId",
                table: "FloorPlanEdges", column: "BuildingId");
            migrationBuilder.CreateIndex(name: "IX_FloorPlanEdges_FromNodeId",
                table: "FloorPlanEdges", column: "FromNodeId");
            migrationBuilder.CreateIndex(name: "IX_FloorPlanEdges_ToNodeId",
                table: "FloorPlanEdges", column: "ToNodeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "FloorPlanEdges");
            migrationBuilder.DropTable(name: "FloorPlanNodes");

            migrationBuilder.DropColumn(name: "NumberOfFloors", table: "Buildings");
            migrationBuilder.DropColumn(name: "NumberOfBasementFloors", table: "Buildings");
            migrationBuilder.AddColumn<int>(name: "StairsDistancePerFloor", table: "Buildings",
                nullable: false, defaultValue: 20);

            migrationBuilder.AddColumn<int>(name: "DistanceFromStairsMeters", table: "Rooms",
                nullable: false, defaultValue: 0);
        }
    }
}
