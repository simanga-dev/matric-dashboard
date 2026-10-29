using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MatricDasbhoard.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSchoolProfilesAndEnrolments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BuildingOwnership",
                schema: "marks",
                table: "Schools",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BuildingOwnershipRaw",
                schema: "marks",
                table: "Schools",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                schema: "marks",
                table: "Schools",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsNoFeeSchool",
                schema: "marks",
                table: "Schools",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSection21",
                schema: "marks",
                table: "Schools",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LandOwnership",
                schema: "marks",
                table: "Schools",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LandOwnershipRaw",
                schema: "marks",
                table: "Schools",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MasterlistDataYear",
                schema: "marks",
                table: "Schools",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phase",
                schema: "marks",
                table: "Schools",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Sector",
                schema: "marks",
                table: "Schools",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Telephone",
                schema: "marks",
                table: "Schools",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UrbanRural",
                schema: "marks",
                table: "Schools",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SchoolEnrolments",
                schema: "marks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Learners = table.Column<int>(type: "integer", nullable: true),
                    Educators = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolEnrolments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchoolEnrolments_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalSchema: "marks",
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SchoolEnrolments_IsDeleted",
                schema: "marks",
                table: "SchoolEnrolments",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolEnrolments_SchoolId_Year",
                schema: "marks",
                table: "SchoolEnrolments",
                columns: new[] { "SchoolId", "Year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchoolEnrolments_Year",
                schema: "marks",
                table: "SchoolEnrolments",
                column: "Year");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SchoolEnrolments",
                schema: "marks");

            migrationBuilder.DropColumn(
                name: "BuildingOwnership",
                schema: "marks",
                table: "Schools");

            migrationBuilder.DropColumn(
                name: "BuildingOwnershipRaw",
                schema: "marks",
                table: "Schools");

            migrationBuilder.DropColumn(
                name: "Email",
                schema: "marks",
                table: "Schools");

            migrationBuilder.DropColumn(
                name: "IsNoFeeSchool",
                schema: "marks",
                table: "Schools");

            migrationBuilder.DropColumn(
                name: "IsSection21",
                schema: "marks",
                table: "Schools");

            migrationBuilder.DropColumn(
                name: "LandOwnership",
                schema: "marks",
                table: "Schools");

            migrationBuilder.DropColumn(
                name: "LandOwnershipRaw",
                schema: "marks",
                table: "Schools");

            migrationBuilder.DropColumn(
                name: "MasterlistDataYear",
                schema: "marks",
                table: "Schools");

            migrationBuilder.DropColumn(
                name: "Phase",
                schema: "marks",
                table: "Schools");

            migrationBuilder.DropColumn(
                name: "Sector",
                schema: "marks",
                table: "Schools");

            migrationBuilder.DropColumn(
                name: "Telephone",
                schema: "marks",
                table: "Schools");

            migrationBuilder.DropColumn(
                name: "UrbanRural",
                schema: "marks",
                table: "Schools");
        }
    }
}
