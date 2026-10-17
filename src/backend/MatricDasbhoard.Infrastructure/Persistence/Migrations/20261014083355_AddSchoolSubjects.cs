using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MatricDasbhoard.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSchoolSubjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Subjects",
                schema: "marks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsGateway = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_Subjects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SchoolSubjectPerformances",
                schema: "marks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    TotalWrote = table.Column<int>(type: "integer", nullable: false),
                    Achieved = table.Column<int>(type: "integer", nullable: true),
                    PercentAchieved = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: true),
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
                    table.PrimaryKey("PK_SchoolSubjectPerformances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchoolSubjectPerformances_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalSchema: "marks",
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SchoolSubjectPerformances_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalSchema: "marks",
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SchoolSubjectPerformances_IsDeleted",
                schema: "marks",
                table: "SchoolSubjectPerformances",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolSubjectPerformances_SchoolId_SubjectId_Year",
                schema: "marks",
                table: "SchoolSubjectPerformances",
                columns: new[] { "SchoolId", "SubjectId", "Year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchoolSubjectPerformances_SubjectId_Year",
                schema: "marks",
                table: "SchoolSubjectPerformances",
                columns: new[] { "SubjectId", "Year" });

            migrationBuilder.CreateIndex(
                name: "IX_SchoolSubjectPerformances_Year",
                schema: "marks",
                table: "SchoolSubjectPerformances",
                column: "Year");

            migrationBuilder.CreateIndex(
                name: "IX_Subjects_Code",
                schema: "marks",
                table: "Subjects",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subjects_IsDeleted",
                schema: "marks",
                table: "Subjects",
                column: "IsDeleted");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SchoolSubjectPerformances",
                schema: "marks");

            migrationBuilder.DropTable(
                name: "Subjects",
                schema: "marks");
        }
    }
}
