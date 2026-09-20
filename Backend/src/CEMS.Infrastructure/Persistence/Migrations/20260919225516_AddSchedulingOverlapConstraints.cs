using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CEMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSchedulingOverlapConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "ck_course_sessions_end_after_start",
                table: "CourseSessions",
                sql: "\"EndUtc\" > \"StartUtc\"");

            // Exclusion constraints are PostgreSQL-specific and have no EF model representation, so they
            // live here. They are the last line of defence behind the advisory locks the scheduling
            // handlers take: even a code path that forgets the lock cannot store two ordinary
            // (non-overridden, non-cancelled) sessions that overlap in the same room or for the same
            // teacher. Overridden sessions are deliberately exempt -- an authorised manager may
            // knowingly double-book, and that decision is recorded on the row itself. Ranges are
            // half-open, so back-to-back sessions (end == next start) do not conflict. Status 2 is
            // SessionStatus.Cancelled. btree_gist lets a gist index compare uuid equality.
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");

            migrationBuilder.Sql(
                """
                ALTER TABLE "CourseSessions"
                    ADD CONSTRAINT ex_course_sessions_room_no_overlap
                    EXCLUDE USING gist ("RoomId" WITH =, tstzrange("StartUtc", "EndUtc") WITH &&)
                    WHERE (NOT "Overridden" AND "Status" <> 2);
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "CourseSessions"
                    ADD CONSTRAINT ex_course_sessions_teacher_no_overlap
                    EXCLUDE USING gist ("TeacherId" WITH =, tstzrange("StartUtc", "EndUtc") WITH &&)
                    WHERE (NOT "Overridden" AND "Status" <> 2);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE \"CourseSessions\" DROP CONSTRAINT IF EXISTS ex_course_sessions_teacher_no_overlap;");
            migrationBuilder.Sql("ALTER TABLE \"CourseSessions\" DROP CONSTRAINT IF EXISTS ex_course_sessions_room_no_overlap;");

            migrationBuilder.DropCheckConstraint(
                name: "ck_course_sessions_end_after_start",
                table: "CourseSessions");
        }
    }
}
