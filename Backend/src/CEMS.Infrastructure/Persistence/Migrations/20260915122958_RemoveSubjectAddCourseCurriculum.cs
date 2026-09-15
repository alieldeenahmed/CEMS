using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CEMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSubjectAddCourseCurriculum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Courses_Subjects_SubjectId",
                table: "Courses");

            // The column below is renamed (not dropped/recreated), which preserves whatever GUID
            // values it already holds -- but those are Subject ids, not Curriculum ids. Rewrite them
            // to the Subject's own CurriculumId now that the FK to Subjects is gone (so the
            // in-progress values are never checked against it), while the Subjects table itself is
            // still there to look the mapping up from.
            migrationBuilder.Sql(
                "UPDATE \"Courses\" SET \"SubjectId\" = \"Subjects\".\"CurriculumId\" " +
                "FROM \"Subjects\" WHERE \"Subjects\".\"Id\" = \"Courses\".\"SubjectId\";");

            migrationBuilder.DropTable(
                name: "Subjects");

            migrationBuilder.RenameColumn(
                name: "SubjectId",
                table: "Courses",
                newName: "CurriculumId");

            migrationBuilder.RenameIndex(
                name: "IX_Courses_SubjectId",
                table: "Courses",
                newName: "IX_Courses_CurriculumId");

            migrationBuilder.AddForeignKey(
                name: "FK_Courses_Curricula_CurriculumId",
                table: "Courses",
                column: "CurriculumId",
                principalTable: "Curricula",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Courses_Curricula_CurriculumId",
                table: "Courses");

            migrationBuilder.RenameColumn(
                name: "CurriculumId",
                table: "Courses",
                newName: "SubjectId");

            migrationBuilder.RenameIndex(
                name: "IX_Courses_CurriculumId",
                table: "Courses",
                newName: "IX_Courses_SubjectId");

            migrationBuilder.CreateTable(
                name: "Subjects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CurriculumId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Subjects_Curricula_CurriculumId",
                        column: x => x.CurriculumId,
                        principalTable: "Curricula",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Subjects_CurriculumId",
                table: "Subjects",
                column: "CurriculumId");

            migrationBuilder.AddForeignKey(
                name: "FK_Courses_Subjects_SubjectId",
                table: "Courses",
                column: "SubjectId",
                principalTable: "Subjects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
