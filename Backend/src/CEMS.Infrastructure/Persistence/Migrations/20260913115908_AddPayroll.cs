using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CEMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPayroll : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PayrollRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollRuns_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollLineItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PayrollRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(10,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollLineItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollLineItems_CourseSessions_CourseSessionId",
                        column: x => x.CourseSessionId,
                        principalTable: "CourseSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollLineItems_PayrollRuns_PayrollRunId",
                        column: x => x.PayrollRunId,
                        principalTable: "PayrollRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollLineItems_CourseSessionId",
                table: "PayrollLineItems",
                column: "CourseSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollLineItems_PayrollRunId",
                table: "PayrollLineItems",
                column: "PayrollRunId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRuns_TeacherId",
                table: "PayrollRuns",
                column: "TeacherId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayrollLineItems");

            migrationBuilder.DropTable(
                name: "PayrollRuns");
        }
    }
}
