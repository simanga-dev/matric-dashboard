using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MatricDasbhoard.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMatricMarksSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "marks");

            migrationBuilder.CreateTable(
                name: "Schools",
                schema: "marks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Province = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DistrictName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EmisNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CentreNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CentreName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Quintile = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_Schools", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SchoolPerformances",
                schema: "marks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    ProgressedNumber = table.Column<int>(type: "integer", nullable: false),
                    TotalWrote = table.Column<int>(type: "integer", nullable: false),
                    TotalAchieved = table.Column<int>(type: "integer", nullable: false),
                    PercentAchieved = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: false),
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
                    table.PrimaryKey("PK_SchoolPerformances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchoolPerformances_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalSchema: "marks",
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SchoolPerformances_IsDeleted",
                schema: "marks",
                table: "SchoolPerformances",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolPerformances_SchoolId_Year",
                schema: "marks",
                table: "SchoolPerformances",
                columns: new[] { "SchoolId", "Year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchoolPerformances_Year",
                schema: "marks",
                table: "SchoolPerformances",
                column: "Year");

            migrationBuilder.CreateIndex(
                name: "IX_Schools_DistrictName",
                schema: "marks",
                table: "Schools",
                column: "DistrictName");

            migrationBuilder.CreateIndex(
                name: "IX_Schools_EmisNumber",
                schema: "marks",
                table: "Schools",
                column: "EmisNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Schools_IsDeleted",
                schema: "marks",
                table: "Schools",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Schools_Province",
                schema: "marks",
                table: "Schools",
                column: "Province");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SchoolPerformances",
                schema: "marks");

            migrationBuilder.DropTable(
                name: "Schools",
                schema: "marks");
        }
    }
}
