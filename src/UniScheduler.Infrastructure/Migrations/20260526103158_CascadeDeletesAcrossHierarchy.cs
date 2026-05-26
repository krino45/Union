using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniScheduler.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CascadeDeletesAcrossHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BuildingDistances_Buildings_FromBuildingId",
                table: "BuildingDistances");

            migrationBuilder.DropForeignKey(
                name: "FK_BuildingDistances_Buildings_ToBuildingId",
                table: "BuildingDistances");

            migrationBuilder.DropForeignKey(
                name: "FK_Departments_Faculties_FacultyId",
                table: "Departments");

            migrationBuilder.DropForeignKey(
                name: "FK_FloorPlanEdges_FloorPlanNodes_FromNodeId",
                table: "FloorPlanEdges");

            migrationBuilder.DropForeignKey(
                name: "FK_FloorPlanEdges_FloorPlanNodes_ToNodeId",
                table: "FloorPlanEdges");

            migrationBuilder.DropForeignKey(
                name: "FK_RescheduleRequests_Rooms_ProposedRoomId",
                table: "RescheduleRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_RescheduleRequests_ScheduleEntries_OriginalEntryId",
                table: "RescheduleRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_RescheduleRequests_Teachers_RequestedByTeacherId",
                table: "RescheduleRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_Rooms_Buildings_BuildingId",
                table: "Rooms");

            migrationBuilder.DropForeignKey(
                name: "FK_ScheduleEntries_Rooms_RoomId",
                table: "ScheduleEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_ScheduleEntries_Subjects_SubjectId",
                table: "ScheduleEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_ScheduleEntries_Teachers_TeacherId",
                table: "ScheduleEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_ScheduleEntryStudentGroups_StudentGroups_StudentGroupId",
                table: "ScheduleEntryStudentGroups");

            migrationBuilder.DropForeignKey(
                name: "FK_Schedules_Faculties_FacultyId",
                table: "Schedules");

            migrationBuilder.DropForeignKey(
                name: "FK_StudentGroups_Faculties_FacultyId",
                table: "StudentGroups");

            migrationBuilder.AddForeignKey(
                name: "FK_BuildingDistances_Buildings_FromBuildingId",
                table: "BuildingDistances",
                column: "FromBuildingId",
                principalTable: "Buildings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BuildingDistances_Buildings_ToBuildingId",
                table: "BuildingDistances",
                column: "ToBuildingId",
                principalTable: "Buildings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Departments_Faculties_FacultyId",
                table: "Departments",
                column: "FacultyId",
                principalTable: "Faculties",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FloorPlanEdges_FloorPlanNodes_FromNodeId",
                table: "FloorPlanEdges",
                column: "FromNodeId",
                principalTable: "FloorPlanNodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FloorPlanEdges_FloorPlanNodes_ToNodeId",
                table: "FloorPlanEdges",
                column: "ToNodeId",
                principalTable: "FloorPlanNodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RescheduleRequests_Rooms_ProposedRoomId",
                table: "RescheduleRequests",
                column: "ProposedRoomId",
                principalTable: "Rooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_RescheduleRequests_ScheduleEntries_OriginalEntryId",
                table: "RescheduleRequests",
                column: "OriginalEntryId",
                principalTable: "ScheduleEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RescheduleRequests_Teachers_RequestedByTeacherId",
                table: "RescheduleRequests",
                column: "RequestedByTeacherId",
                principalTable: "Teachers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Rooms_Buildings_BuildingId",
                table: "Rooms",
                column: "BuildingId",
                principalTable: "Buildings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ScheduleEntries_Rooms_RoomId",
                table: "ScheduleEntries",
                column: "RoomId",
                principalTable: "Rooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ScheduleEntries_Subjects_SubjectId",
                table: "ScheduleEntries",
                column: "SubjectId",
                principalTable: "Subjects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ScheduleEntries_Teachers_TeacherId",
                table: "ScheduleEntries",
                column: "TeacherId",
                principalTable: "Teachers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ScheduleEntryStudentGroups_StudentGroups_StudentGroupId",
                table: "ScheduleEntryStudentGroups",
                column: "StudentGroupId",
                principalTable: "StudentGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Schedules_Faculties_FacultyId",
                table: "Schedules",
                column: "FacultyId",
                principalTable: "Faculties",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_StudentGroups_Faculties_FacultyId",
                table: "StudentGroups",
                column: "FacultyId",
                principalTable: "Faculties",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BuildingDistances_Buildings_FromBuildingId",
                table: "BuildingDistances");

            migrationBuilder.DropForeignKey(
                name: "FK_BuildingDistances_Buildings_ToBuildingId",
                table: "BuildingDistances");

            migrationBuilder.DropForeignKey(
                name: "FK_Departments_Faculties_FacultyId",
                table: "Departments");

            migrationBuilder.DropForeignKey(
                name: "FK_FloorPlanEdges_FloorPlanNodes_FromNodeId",
                table: "FloorPlanEdges");

            migrationBuilder.DropForeignKey(
                name: "FK_FloorPlanEdges_FloorPlanNodes_ToNodeId",
                table: "FloorPlanEdges");

            migrationBuilder.DropForeignKey(
                name: "FK_RescheduleRequests_Rooms_ProposedRoomId",
                table: "RescheduleRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_RescheduleRequests_ScheduleEntries_OriginalEntryId",
                table: "RescheduleRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_RescheduleRequests_Teachers_RequestedByTeacherId",
                table: "RescheduleRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_Rooms_Buildings_BuildingId",
                table: "Rooms");

            migrationBuilder.DropForeignKey(
                name: "FK_ScheduleEntries_Rooms_RoomId",
                table: "ScheduleEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_ScheduleEntries_Subjects_SubjectId",
                table: "ScheduleEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_ScheduleEntries_Teachers_TeacherId",
                table: "ScheduleEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_ScheduleEntryStudentGroups_StudentGroups_StudentGroupId",
                table: "ScheduleEntryStudentGroups");

            migrationBuilder.DropForeignKey(
                name: "FK_Schedules_Faculties_FacultyId",
                table: "Schedules");

            migrationBuilder.DropForeignKey(
                name: "FK_StudentGroups_Faculties_FacultyId",
                table: "StudentGroups");

            migrationBuilder.AddForeignKey(
                name: "FK_BuildingDistances_Buildings_FromBuildingId",
                table: "BuildingDistances",
                column: "FromBuildingId",
                principalTable: "Buildings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BuildingDistances_Buildings_ToBuildingId",
                table: "BuildingDistances",
                column: "ToBuildingId",
                principalTable: "Buildings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Departments_Faculties_FacultyId",
                table: "Departments",
                column: "FacultyId",
                principalTable: "Faculties",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FloorPlanEdges_FloorPlanNodes_FromNodeId",
                table: "FloorPlanEdges",
                column: "FromNodeId",
                principalTable: "FloorPlanNodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FloorPlanEdges_FloorPlanNodes_ToNodeId",
                table: "FloorPlanEdges",
                column: "ToNodeId",
                principalTable: "FloorPlanNodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RescheduleRequests_Rooms_ProposedRoomId",
                table: "RescheduleRequests",
                column: "ProposedRoomId",
                principalTable: "Rooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RescheduleRequests_ScheduleEntries_OriginalEntryId",
                table: "RescheduleRequests",
                column: "OriginalEntryId",
                principalTable: "ScheduleEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RescheduleRequests_Teachers_RequestedByTeacherId",
                table: "RescheduleRequests",
                column: "RequestedByTeacherId",
                principalTable: "Teachers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Rooms_Buildings_BuildingId",
                table: "Rooms",
                column: "BuildingId",
                principalTable: "Buildings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ScheduleEntries_Rooms_RoomId",
                table: "ScheduleEntries",
                column: "RoomId",
                principalTable: "Rooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ScheduleEntries_Subjects_SubjectId",
                table: "ScheduleEntries",
                column: "SubjectId",
                principalTable: "Subjects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ScheduleEntries_Teachers_TeacherId",
                table: "ScheduleEntries",
                column: "TeacherId",
                principalTable: "Teachers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ScheduleEntryStudentGroups_StudentGroups_StudentGroupId",
                table: "ScheduleEntryStudentGroups",
                column: "StudentGroupId",
                principalTable: "StudentGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Schedules_Faculties_FacultyId",
                table: "Schedules",
                column: "FacultyId",
                principalTable: "Faculties",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StudentGroups_Faculties_FacultyId",
                table: "StudentGroups",
                column: "FacultyId",
                principalTable: "Faculties",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
