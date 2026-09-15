using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CEMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentBranchHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StudentBranchHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromBranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToBranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransferDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TransferredByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentBranchHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentBranchHistories_Branches_FromBranchId",
                        column: x => x.FromBranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentBranchHistories_Branches_ToBranchId",
                        column: x => x.ToBranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentBranchHistories_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudentBranchHistories_FromBranchId",
                table: "StudentBranchHistories",
                column: "FromBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentBranchHistories_StudentId",
                table: "StudentBranchHistories",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentBranchHistories_ToBranchId",
                table: "StudentBranchHistories",
                column: "ToBranchId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudentBranchHistories");
        }
    }
}
