#!/usr/bin/env bash
# Populates a fresh, empty CEMS database with a realistic demo dataset by driving the real running API
# end to end (not raw SQL), so every row passes through password hashing, RBAC, scheduling conflict
# checks, invoice status transitions and payroll computation. Mirrors scripts/seed-demo-data.ps1.
#
# Usage:   ./scripts/seed-demo-data.sh [API_BASE_URL]        (default: $CEMS_API_URL or http://localhost:5272)
# Needs:   bash and curl. Works on Linux, macOS and Git Bash on Windows.
# NOT idempotent: run it once against an empty database (a second run fails on duplicate emails).
set -euo pipefail
export LC_ALL=C

BASE="${1:-${CEMS_API_URL:-http://localhost:5272}}"
BASE="${BASE%/}"
OWNER_EMAIL="owner@codecamp.demo"
PASSWORD="DemoPass123"

command -v curl >/dev/null 2>&1 || { echo "curl is required." >&2; exit 1; }

# ---- helpers ------------------------------------------------------------------------------------

# api METHOD PATH TOKEN [JSON_BODY] -> prints the response body; exits non-zero (printing the body) on HTTP >= 400.
api() {
    local method="$1" path="$2" token="${3:-}" body="${4:-}" out status
    local args=(-sS -X "$method" "$BASE$path" -H "Content-Type: application/json" -w $'\n%{http_code}')
    [ -n "$token" ] && args+=(-H "Authorization: Bearer $token")
    case "$method" in POST|PUT|PATCH) args+=(--data "$body") ;; esac
    out="$(curl "${args[@]}")" || { echo "FAILED: $method $path (could not reach $BASE)" >&2; exit 1; }
    status="${out##*$'\n'}"
    out="${out%$'\n'*}"
    if [ "$status" -ge 400 ]; then
        echo "FAILED: $method $path -> HTTP $status" >&2
        echo "$out" >&2
        exit 1
    fi
    printf '%s' "$out"
}

# First "key":"value" string in a JSON document (ids, tokens). The API returns flat camelCase JSON.
json_str() { printf '%s' "$2" | grep -o "\"$1\":\"[^\"]*\"" | head -n 1 | cut -d'"' -f4; }
json_num() { printf '%s' "$2" | grep -o "\"$1\":[0-9.]*" | head -n 1 | cut -d: -f2; }

# Formats an epoch (seconds) as UTC. GNU date uses -d @N, BSD/macOS date uses -r N.
fmt_utc() { date -u -d "@$1" +"$2" 2>/dev/null || date -u -r "$1" +"$2"; }

NOW="$(date -u +%s)"
TODAY_MIDNIGHT=$(( NOW - NOW % 86400 ))
TODAY="$(fmt_utc "$NOW" '%Y-%m-%d')"
TODAY_DOW="$(fmt_utc "$NOW" '%A')"

# Next Monday (UTC) strictly after today, and the Monday after that.
DOW_NUM="$(fmt_utc "$NOW" '%u')"                         # 1=Mon ... 7=Sun
DAYS_TO_MONDAY=$(( (8 - DOW_NUM) % 7 ))
[ "$DAYS_TO_MONDAY" -eq 0 ] && DAYS_TO_MONDAY=7
NEXT_MONDAY="$(fmt_utc $(( TODAY_MIDNIGHT + DAYS_TO_MONDAY * 86400 )) '%Y-%m-%d')"
MONDAY_AFTER="$(fmt_utc $(( TODAY_MIDNIGHT + (DAYS_TO_MONDAY + 7) * 86400 )) '%Y-%m-%d')"
ISO='%Y-%m-%dT%H:%M:%SZ'
TWO_HOURS_AGO="$(fmt_utc $(( NOW - 7200 )) "$ISO")"
ONE_HOUR_AGO="$(fmt_utc $(( NOW - 3600 )) "$ISO")"
IN_14_DAYS="$(fmt_utc $(( NOW + 14 * 86400 )) '%Y-%m-%d')"
IN_30_DAYS="$(fmt_utc $(( NOW + 30 * 86400 )) '%Y-%m-%d')"
SEVEN_DAYS_AGO="$(fmt_utc $(( NOW - 7 * 86400 )) '%Y-%m-%d')"

echo "=== CEMS demo data seeder: CodeCamp (Alexandria) -> $BASE ==="

# Wait for the API to answer (any HTTP status means the server is up).
for i in $(seq 1 60); do
    code="$(curl -s -o /dev/null -w '%{http_code}' -X POST "$BASE/api/auth/login" -H 'Content-Type: application/json' --data '{}' || true)"
    [ "$code" != "000" ] && break
    [ "$i" -eq 60 ] && { echo "The API at $BASE did not respond after 60s." >&2; exit 1; }
    sleep 1
done

# ---- 1. Owner -----------------------------------------------------------------------------------
echo; echo "[1/12] Bootstrapping Owner account..."
# bootstrap-owner is anonymous but disables itself the moment any user exists, so it can't be used as a signup path.
resp="$(api POST /api/auth/bootstrap-owner "" "{\"email\":\"$OWNER_EMAIL\",\"password\":\"$PASSWORD\",\"fullName\":\"Mostafa El-Sayed\",\"phoneNumber\":\"01012340001\"}")"
TOKEN="$(json_str token "$resp")"
echo "  Owner ready: $OWNER_EMAIL / $PASSWORD"

# create PATH JSON -> echoes the new record's id
create() { json_str id "$(api POST "$1" "$TOKEN" "$2")"; }
# create_user JSON -> echoes the new account's userId
create_user() { json_str userId "$(api POST /api/users/staff "$TOKEN" "$1")"; }

# ---- 2. Branches and rooms ---------------------------------------------------------------------
echo; echo "[2/12] Creating branches and rooms..."
SMOUHA="$(create /api/branches '{"name":"CodeCamp Smouha","address":"14 Fawzy Moaz St, Smouha, Alexandria","phone":"034567001"}')"
KAFR="$(create /api/branches '{"name":"CodeCamp Kafr Abdo","address":"9 Abdel Salam Aref St, Kafr Abdo, Alexandria","phone":"034567002"}')"
SMOUHA_LAB1="$(create "/api/branches/$SMOUHA/rooms" '{"name":"Lab 1","capacity":15}')"
SMOUHA_LAB2="$(create "/api/branches/$SMOUHA/rooms" '{"name":"Lab 2 (1:1)","capacity":1}')"
KAFR_LAB1="$(create "/api/branches/$KAFR/rooms" '{"name":"Lab 1","capacity":15}')"
echo "  CodeCamp Smouha ($SMOUHA), CodeCamp Kafr Abdo ($KAFR)"

# ---- 3. Curricula, courses, packages -----------------------------------------------------------
echo; echo "[3/12] Creating curricula, courses, and packages..."
JUNIOR="$(create /api/curricula '{"name":"Junior Coding","description":"Foundational programming and computational thinking for ages 8-14, taught through block-based tools before moving to text-based code."}')"
WEB="$(create /api/curricula '{"name":"Web & Software Development","description":"Applied programming tracks in Python, JavaScript, and full-stack web development for teens and adults."}')"
PY_COURSE="$(create /api/courses "{\"name\":\"Python Fundamentals - Group A\",\"deliveryMode\":\"Group\",\"curriculumId\":\"$WEB\",\"branchId\":\"$SMOUHA\"}")"
JS_COURSE="$(create /api/courses "{\"name\":\"Web Development with JavaScript - 1:1\",\"deliveryMode\":\"OneOnOne\",\"curriculumId\":\"$WEB\",\"branchId\":\"$SMOUHA\"}")"
SCRATCH_COURSE="$(create /api/courses "{\"name\":\"Scratch Programming - Group A\",\"deliveryMode\":\"Group\",\"curriculumId\":\"$JUNIOR\",\"branchId\":\"$KAFR\"}")"
PY_PACKAGE="$(create "/api/courses/$PY_COURSE/packages" '{"sessionCount":12,"price":2400}')"
JS_PACKAGE="$(create "/api/courses/$JS_COURSE/packages" '{"sessionCount":12,"price":3600}')"
echo "  2 curricula, 3 courses, 2 packages"

# ---- 4. Staff accounts -------------------------------------------------------------------------
echo; echo "[4/12] Creating staff accounts..."
staff() { # email name phone role branchId|null
    local branch="null"; [ "$5" != "null" ] && branch="\"$5\""
    create_user "{\"email\":\"$1\",\"password\":\"$PASSWORD\",\"fullName\":\"$2\",\"phoneNumber\":\"$3\",\"role\":\"$4\",\"branchId\":$branch}"
}
staff bm.smouha@codecamp.demo "Nourhan Adel" 01012340002 BranchManager "$SMOUHA" >/dev/null
staff bm.kafrabdo@codecamp.demo "Hossam Fathy" 01012340003 BranchManager "$KAFR" >/dev/null
staff fd.smouha@codecamp.demo "Mariam Younis" 01012340004 FrontDesk "$SMOUHA" >/dev/null
TEACHER_USER_SMOUHA="$(staff teacher.smouha@codecamp.demo "Ahmed Nabil" 01012340005 Teacher null)"
TEACHER_USER_KAFR="$(staff teacher.kafrabdo@codecamp.demo "Sara Ibrahim" 01012340006 Teacher null)"
echo "  2 BranchManagers, 1 FrontDesk, 2 Teachers (login accounts only so far)"

# ---- 5. Teacher profiles, branches, availability, qualifications -------------------------------
echo; echo "[5/12] Creating teacher profiles, branch assignments, availability, and qualifications..."
T_SMOUHA="$(create /api/teachers "{\"userId\":\"$TEACHER_USER_SMOUHA\",\"hireDate\":\"2024-01-01\",\"payType\":\"Hourly\",\"payRate\":150}")"
T_KAFR="$(create /api/teachers "{\"userId\":\"$TEACHER_USER_KAFR\",\"hireDate\":\"2024-06-01\",\"payType\":\"PerSession\",\"payRate\":100}")"
api POST "/api/teachers/$T_SMOUHA/branches" "$TOKEN" "{\"branchId\":\"$SMOUHA\"}" >/dev/null
api POST "/api/teachers/$T_KAFR/branches" "$TOKEN" "{\"branchId\":\"$KAFR\"}" >/dev/null
# Wide Monday window so the upcoming demo sessions never hit an availability conflict.
api POST "/api/teachers/$T_SMOUHA/availability" "$TOKEN" "{\"branchId\":\"$SMOUHA\",\"dayOfWeek\":\"Monday\",\"startTime\":\"09:00\",\"endTime\":\"17:00\"}" >/dev/null
api POST "/api/teachers/$T_KAFR/availability" "$TOKEN" "{\"branchId\":\"$KAFR\",\"dayOfWeek\":\"Monday\",\"startTime\":\"09:00\",\"endTime\":\"17:00\"}" >/dev/null
# One demo session is deliberately scheduled in the recent past (so attendance and grading have a finished
# session to act on); that only avoids an availability conflict if today (UTC) is covered too.
api POST "/api/teachers/$T_SMOUHA/availability" "$TOKEN" "{\"branchId\":\"$SMOUHA\",\"dayOfWeek\":\"$TODAY_DOW\",\"startTime\":\"00:00\",\"endTime\":\"23:59\"}" >/dev/null
# A teacher can only be scheduled for a course they are qualified to teach (structural rule; no override).
api POST "/api/teachers/$T_SMOUHA/qualifications" "$TOKEN" "{\"courseId\":\"$PY_COURSE\"}" >/dev/null
api POST "/api/teachers/$T_SMOUHA/qualifications" "$TOKEN" "{\"courseId\":\"$JS_COURSE\"}" >/dev/null
api POST "/api/teachers/$T_KAFR/qualifications" "$TOKEN" "{\"courseId\":\"$SCRATCH_COURSE\"}" >/dev/null
echo "  Both teachers assigned to their branch with Monday 09:00-17:00 availability"

# ---- 6. Guardians and students -----------------------------------------------------------------
echo; echo "[6/12] Creating guardian contacts and students..."
G_HANY="$(create /api/guardians '{"fullName":"Hany Mahmoud","phone":"01123450001","email":"hany.mahmoud@example.com"}')"
G_DOAA="$(create /api/guardians '{"fullName":"Doaa Kamel","phone":"01123450002","email":"doaa.kamel@example.com"}')"
G_TAREK="$(create /api/guardians '{"fullName":"Tarek Aboulfotouh","phone":"01123450003","email":"tarek.aboulfotouh@example.com"}')"
student() { # name dob gender branch guardian relationship
    create /api/students "{\"fullName\":\"$1\",\"dateOfBirth\":\"$2\",\"gender\":\"$3\",\"branchId\":\"$4\",\"existingGuardianId\":\"$5\",\"relationshipType\":\"$6\",\"isPrimaryContact\":true}"
}
S_KHALED="$(student "Khaled Hany" 2013-04-12 Male "$SMOUHA" "$G_HANY" Father)"
S_LINA="$(student "Lina Hany" 2014-08-22 Female "$SMOUHA" "$G_HANY" Father)"
S_MALAK="$(student "Malak Kamel" 2012-11-02 Female "$KAFR" "$G_DOAA" Mother)"
S_OMAR="$(student "Omar Tarek" 2011-02-15 Male "$SMOUHA" "$G_TAREK" Father)"
S_RANA="$(student "Rana Tarek" 2015-09-30 Female "$KAFR" "$G_TAREK" Father)"
echo "  3 guardian contacts, 5 students (Hany Mahmoud and Tarek Aboulfotouh each have 2 kids)"

# ---- 7. Enrollments ----------------------------------------------------------------------------
echo; echo "[7/12] Enrolling students in courses..."
api POST "/api/courses/$PY_COURSE/enrollments" "$TOKEN" "{\"studentId\":\"$S_KHALED\"}" >/dev/null
api POST "/api/courses/$PY_COURSE/enrollments" "$TOKEN" "{\"studentId\":\"$S_OMAR\"}" >/dev/null
api POST "/api/courses/$JS_COURSE/enrollments" "$TOKEN" "{\"studentId\":\"$S_LINA\"}" >/dev/null
api POST "/api/courses/$SCRATCH_COURSE/enrollments" "$TOKEN" "{\"studentId\":\"$S_MALAK\"}" >/dev/null
api POST "/api/courses/$SCRATCH_COURSE/enrollments" "$TOKEN" "{\"studentId\":\"$S_RANA\"}" >/dev/null
echo "  5 active enrollments"

# ---- 8. Sessions -------------------------------------------------------------------------------
echo; echo "[8/12] Scheduling sessions..."
session() { # courseId roomId teacherId start end -> echoes the session id
    create "/api/courses/$1/sessions" "{\"roomId\":\"$2\",\"teacherId\":\"$3\",\"startUtc\":\"$4\",\"endUtc\":\"$5\",\"override\":false,\"overrideReason\":null}"
}
# In the recent past on purpose: MarkAttendance only accepts a session that has started and ended < 4h ago.
SESSION1="$(session "$PY_COURSE" "$SMOUHA_LAB1" "$T_SMOUHA" "$TWO_HOURS_AGO" "$ONE_HOUR_AGO")"
session "$PY_COURSE" "$SMOUHA_LAB1" "$T_SMOUHA" "${MONDAY_AFTER}T10:00:00Z" "${MONDAY_AFTER}T11:00:00Z" >/dev/null
session "$JS_COURSE" "$SMOUHA_LAB2" "$T_SMOUHA" "${NEXT_MONDAY}T13:00:00Z" "${NEXT_MONDAY}T14:00:00Z" >/dev/null
session "$SCRATCH_COURSE" "$KAFR_LAB1" "$T_KAFR" "${NEXT_MONDAY}T10:00:00Z" "${NEXT_MONDAY}T11:00:00Z" >/dev/null
echo "  4 sessions scheduled across both branches"

# ---- 9. Attendance -----------------------------------------------------------------------------
echo; echo "[9/12] Marking attendance..."
api POST "/api/sessions/$SESSION1/attendance" "$TOKEN" "{\"studentId\":\"$S_KHALED\",\"status\":\"Present\"}" >/dev/null
echo "  1 attendance record marked (the rest stay Unmarked, on purpose -- demonstrates the roster view)"

# ---- 10. Exam and grade ------------------------------------------------------------------------
echo; echo "[10/12] Creating an exam and recording a grade..."
EXAM="$(create "/api/courses/$PY_COURSE/exams" "{\"name\":\"Python Basics Assessment\",\"maxScore\":100,\"examDate\":\"$TODAY\"}")"
api POST "/api/exams/$EXAM/grades" "$TOKEN" "{\"studentId\":\"$S_KHALED\",\"score\":88,\"comments\":\"Great grasp of loops and functions\"}" >/dev/null
echo "  1 exam with 1 grade recorded"

# ---- 11. Invoices and payments -----------------------------------------------------------------
echo; echo "[11/12] Creating invoices and payments..."
INV_PAID="$(create "/api/students/$S_KHALED/invoices" "{\"packageId\":\"$PY_PACKAGE\",\"amount\":null,\"dueDate\":\"$IN_14_DAYS\"}")"
api POST "/api/invoices/$INV_PAID/payments" "$TOKEN" "{\"amountPaid\":2400,\"paymentDate\":\"$TODAY\",\"method\":\"Card\"}" >/dev/null
# Khaled gets one invoice in each common state, so a single student's page shows them side by side.
INV_KHALED_PARTIAL="$(create "/api/students/$S_KHALED/invoices" "{\"packageId\":null,\"amount\":500,\"dueDate\":\"$IN_14_DAYS\"}")"
api POST "/api/invoices/$INV_KHALED_PARTIAL/payments" "$TOKEN" "{\"amountPaid\":200,\"paymentDate\":\"$TODAY\",\"method\":\"Cash\"}" >/dev/null
create "/api/students/$S_KHALED/invoices" "{\"packageId\":null,\"amount\":350,\"dueDate\":\"$IN_30_DAYS\"}" >/dev/null
INV_PARTIAL="$(create "/api/students/$S_LINA/invoices" "{\"packageId\":\"$JS_PACKAGE\",\"amount\":null,\"dueDate\":\"$IN_14_DAYS\"}")"
api POST "/api/invoices/$INV_PARTIAL/payments" "$TOKEN" "{\"amountPaid\":1200,\"paymentDate\":\"$TODAY\",\"method\":\"Cash\"}" >/dev/null
# Unpaid and overdue ad-hoc registration fee: demonstrates the outstanding-balance / overdue view.
create "/api/students/$S_MALAK/invoices" "{\"packageId\":null,\"amount\":200,\"dueDate\":\"$SEVEN_DAYS_AGO\"}" >/dev/null
echo "  5 invoices: 1 fully paid, 2 partially paid, 1 pending, 1 unpaid + overdue"

# ---- 12. Payroll -------------------------------------------------------------------------------
echo; echo "[12/12] Generating a payroll run..."
RUN_RESP="$(api POST "/api/teachers/$T_SMOUHA/payroll-runs" "$TOKEN" "{\"periodStart\":\"$NEXT_MONDAY\",\"periodEnd\":\"$NEXT_MONDAY\"}")"
RUN_ID="$(json_str id "$RUN_RESP")"
api POST "/api/payroll-runs/$RUN_ID/approve" "$TOKEN" "" >/dev/null
echo "  1 payroll run generated and approved for Ahmed Nabil (Smouha), total: $(json_num totalAmount "$RUN_RESP")"

echo
echo "=== Done. Demo accounts (all passwords: $PASSWORD) ==="
echo "  Owner:          $OWNER_EMAIL (Mostafa El-Sayed)"
echo "  BranchManager:  bm.smouha@codecamp.demo (Nourhan Adel, Smouha), bm.kafrabdo@codecamp.demo (Hossam Fathy, Kafr Abdo)"
echo "  FrontDesk:      fd.smouha@codecamp.demo (Mariam Younis, Smouha)"
echo "  Teacher:        teacher.smouha@codecamp.demo (Ahmed Nabil, Smouha, Hourly), teacher.kafrabdo@codecamp.demo (Sara Ibrahim, Kafr Abdo, PerSession)"
