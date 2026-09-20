using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CEMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEnrollmentIndexesAndCheckConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CourseEnrollments_CourseId",
                table: "CourseEnrollments");

            migrationBuilder.DropIndex(
                name: "IX_CourseEnrollments_StudentId",
                table: "CourseEnrollments");

            migrationBuilder.AddCheckConstraint(
                name: "ck_teachers_pay_rate_positive",
                table: "Teachers",
                sql: "\"PayRate\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_staff_payroll_runs_valid",
                table: "StaffPayrollRuns",
                sql: "\"Amount\" > 0 AND \"PeriodEnd\" >= \"PeriodStart\"");

            migrationBuilder.AddCheckConstraint(
                name: "ck_payroll_runs_period_ordered",
                table: "PayrollRuns",
                sql: "\"PeriodEnd\" >= \"PeriodStart\"");

            migrationBuilder.AddCheckConstraint(
                name: "ck_payments_amount_positive",
                table: "Payments",
                sql: "\"AmountPaid\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_packages_price_and_sessions_positive",
                table: "Packages",
                sql: "\"Price\" > 0 AND \"SessionCount\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_invoices_amount_positive",
                table: "Invoices",
                sql: "\"Amount\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_grades_score_not_negative",
                table: "Grades",
                sql: "\"Score\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_exams_max_score_positive",
                table: "Exams",
                sql: "\"MaxScore\" > 0");

            migrationBuilder.CreateIndex(
                name: "ux_course_enrollments_live_per_student_course",
                table: "CourseEnrollments",
                columns: new[] { "StudentId", "CourseId" },
                unique: true,
                filter: "\"Status\" <> 1");

            migrationBuilder.CreateIndex(
                name: "ux_course_enrollments_waitlist_position",
                table: "CourseEnrollments",
                columns: new[] { "CourseId", "Position" },
                unique: true,
                filter: "\"Position\" IS NOT NULL");

            // A teacher or staff member cannot have two payroll runs whose (inclusive) periods overlap. The
            // generate handlers check this under a lock; the constraints make it hold even if that is bypassed.
            // Adding them fails if existing data already overlaps -- resolve those runs first.
            migrationBuilder.Sql(
                """
                ALTER TABLE "PayrollRuns"
                    ADD CONSTRAINT ex_payroll_runs_teacher_no_period_overlap
                    EXCLUDE USING gist ("TeacherId" WITH =, daterange("PeriodStart", "PeriodEnd", '[]') WITH &&);
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "StaffPayrollRuns"
                    ADD CONSTRAINT ex_staff_payroll_runs_user_no_period_overlap
                    EXCLUDE USING gist ("UserId" WITH =, daterange("PeriodStart", "PeriodEnd", '[]') WITH &&);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE \"StaffPayrollRuns\" DROP CONSTRAINT IF EXISTS ex_staff_payroll_runs_user_no_period_overlap;");
            migrationBuilder.Sql("ALTER TABLE \"PayrollRuns\" DROP CONSTRAINT IF EXISTS ex_payroll_runs_teacher_no_period_overlap;");

            migrationBuilder.DropCheckConstraint(
                name: "ck_teachers_pay_rate_positive",
                table: "Teachers");

            migrationBuilder.DropCheckConstraint(
                name: "ck_staff_payroll_runs_valid",
                table: "StaffPayrollRuns");

            migrationBuilder.DropCheckConstraint(
                name: "ck_payroll_runs_period_ordered",
                table: "PayrollRuns");

            migrationBuilder.DropCheckConstraint(
                name: "ck_payments_amount_positive",
                table: "Payments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_packages_price_and_sessions_positive",
                table: "Packages");

            migrationBuilder.DropCheckConstraint(
                name: "ck_invoices_amount_positive",
                table: "Invoices");

            migrationBuilder.DropCheckConstraint(
                name: "ck_grades_score_not_negative",
                table: "Grades");

            migrationBuilder.DropCheckConstraint(
                name: "ck_exams_max_score_positive",
                table: "Exams");

            migrationBuilder.DropIndex(
                name: "ux_course_enrollments_live_per_student_course",
                table: "CourseEnrollments");

            migrationBuilder.DropIndex(
                name: "ux_course_enrollments_waitlist_position",
                table: "CourseEnrollments");

            migrationBuilder.CreateIndex(
                name: "IX_CourseEnrollments_CourseId",
                table: "CourseEnrollments",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseEnrollments_StudentId",
                table: "CourseEnrollments",
                column: "StudentId");
        }
    }
}
