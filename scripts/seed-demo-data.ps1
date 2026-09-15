<#
.SYNOPSIS
    Populates a fresh `cems` database with a realistic demo dataset by driving the real API
    end-to-end (not raw SQL inserts) -- every row here passes through the actual business logic
    it would in production: password hashing, RBAC checks, scheduling conflict detection, invoice
    status transitions, payroll computation, etc.

.DESCRIPTION
    Run this once against a freshly-migrated, empty `cems` database. It is NOT idempotent --
    running it twice will fail on duplicate emails/branches. That's intentional: this is a demo
    seeder for a clean environment, not a repeatable fixture loader.

    The dataset models a small coding school ("CodeCamp") running two branches in Alexandria,
    Egypt -- Smouha and Kafr Abdo -- teaching programming courses from block-based basics for
    kids through Python and full-stack web development for teens and adults.

    Prerequisites:
      - The API must already be running (dotnet run --project Backend/src/CEMS.Api)
      - ConnectionStrings__Default and Jwt__Key must already be set (see README.md "Local setup")
      - PostgreSQL must be reachable at the path/credentials in ConnectionStrings__Default

.PARAMETER ApiBaseUrl
    Base URL of the running API. Defaults to http://localhost:5080.
#>

param(
    [string]$ApiBaseUrl = "http://localhost:5080"
)

$ErrorActionPreference = "Stop"

function Invoke-Api {
    param($Method, $Path, $Token, $Body)
    $headers = @{}
    if ($Token) { $headers["Authorization"] = "Bearer $Token" }
    try {
        if ($Body) {
            return Invoke-RestMethod -Uri "$ApiBaseUrl$Path" -Method $Method -Headers $headers -Body ($Body | ConvertTo-Json) -ContentType "application/json" -UseBasicParsing
        } else {
            return Invoke-RestMethod -Uri "$ApiBaseUrl$Path" -Method $Method -Headers $headers -UseBasicParsing
        }
    } catch {
        $stream = $_.Exception.Response.GetResponseStream()
        $body = if ($stream) { (New-Object System.IO.StreamReader($stream)).ReadToEnd() } else { "" }
        Write-Host "FAILED: $Method $Path" -ForegroundColor Red
        Write-Host $body -ForegroundColor Red
        throw
    }
}

function Get-NextWeekday {
    param([DayOfWeek]$DayOfWeek, [int]$WeeksAhead = 0)
    $today = (Get-Date).Date
    $daysUntil = (([int]$DayOfWeek - [int]$today.DayOfWeek) + 7) % 7
    if ($daysUntil -eq 0) { $daysUntil = 7 }
    return $today.AddDays($daysUntil + ($WeeksAhead * 7))
}

Write-Host "=== CEMS demo data seeder: CodeCamp (Alexandria) ===" -ForegroundColor Cyan

# --- 1. Bootstrap the Owner account -----------------------------------------------------------
Write-Host "`n[1/12] Bootstrapping Owner account..." -ForegroundColor Yellow

$ownerEmail = "owner@codecamp.demo"
$ownerPassword = "DemoPass123"

# Every other account-creation path requires an authenticated Owner/staff token, so the very first
# account has to come from somewhere else. bootstrap-owner is anonymous but self-disables the
# instant any user exists (see BootstrapOwnerCommandHandler) -- it can't be used as a signup path.
$ownerToken = (Invoke-Api POST "/api/auth/bootstrap-owner" $null @{
    email = $ownerEmail; password = $ownerPassword; fullName = "Mostafa El-Sayed"; phoneNumber = "01012340001"
}).token

Write-Host "  Owner ready: $ownerEmail / $ownerPassword"

# --- 2. Branches and rooms ---------------------------------------------------------------------
Write-Host "`n[2/12] Creating branches and rooms..." -ForegroundColor Yellow

$smouha = Invoke-Api POST "/api/branches" $ownerToken @{ name = "CodeCamp Smouha"; address = "14 Fawzy Moaz St, Smouha, Alexandria"; phone = "034567001" }
$kafrAbdo = Invoke-Api POST "/api/branches" $ownerToken @{ name = "CodeCamp Kafr Abdo"; address = "9 Abdel Salam Aref St, Kafr Abdo, Alexandria"; phone = "034567002" }

$smouhaRoomA = Invoke-Api POST "/api/branches/$($smouha.id)/rooms" $ownerToken @{ name = "Lab 1"; capacity = 15 }
$smouhaLab2 = Invoke-Api POST "/api/branches/$($smouha.id)/rooms" $ownerToken @{ name = "Lab 2 (1:1)"; capacity = 1 }
$kafrAbdoRoomA = Invoke-Api POST "/api/branches/$($kafrAbdo.id)/rooms" $ownerToken @{ name = "Lab 1"; capacity = 15 }

Write-Host "  CodeCamp Smouha ($($smouha.id)), CodeCamp Kafr Abdo ($($kafrAbdo.id))"

# --- 3. Curricula, courses, packages ------------------------------------------------------------
Write-Host "`n[3/12] Creating curricula, courses, and packages..." -ForegroundColor Yellow

$juniorCoding = Invoke-Api POST "/api/curricula" $ownerToken @{ name = "Junior Coding"; description = "Foundational programming and computational thinking for ages 8-14, taught through block-based tools before moving to text-based code." }
$webAndSoftware = Invoke-Api POST "/api/curricula" $ownerToken @{ name = "Web & Software Development"; description = "Applied programming tracks in Python, JavaScript, and full-stack web development for teens and adults." }

$pythonCourseSmouha = Invoke-Api POST "/api/courses" $ownerToken @{ name = "Python Fundamentals - Group A"; deliveryMode = "Group"; curriculumId = $webAndSoftware.id; branchId = $smouha.id }
$webDevCourseSmouha = Invoke-Api POST "/api/courses" $ownerToken @{ name = "Web Development with JavaScript - 1:1"; deliveryMode = "OneOnOne"; curriculumId = $webAndSoftware.id; branchId = $smouha.id }
$scratchCourseKafrAbdo = Invoke-Api POST "/api/courses" $ownerToken @{ name = "Scratch Programming - Group A"; deliveryMode = "Group"; curriculumId = $juniorCoding.id; branchId = $kafrAbdo.id }

$pythonPackageSmouha = Invoke-Api POST "/api/courses/$($pythonCourseSmouha.id)/packages" $ownerToken @{ sessionCount = 12; price = 2400 }
$webDevPackageSmouha = Invoke-Api POST "/api/courses/$($webDevCourseSmouha.id)/packages" $ownerToken @{ sessionCount = 12; price = 3600 }

Write-Host "  2 curricula ('Junior Coding', 'Web & Software Development'), 3 courses, 2 packages"

# --- 4. Staff accounts -------------------------------------------------------------------------
Write-Host "`n[4/12] Creating staff accounts..." -ForegroundColor Yellow

$bmSmouha = Invoke-Api POST "/api/users/staff" $ownerToken @{ email = "bm.smouha@codecamp.demo"; password = "DemoPass123"; fullName = "Nourhan Adel"; phoneNumber = "01012340002"; role = "BranchManager"; branchId = $smouha.id }
$bmKafrAbdo = Invoke-Api POST "/api/users/staff" $ownerToken @{ email = "bm.kafrabdo@codecamp.demo"; password = "DemoPass123"; fullName = "Hossam Fathy"; phoneNumber = "01012340003"; role = "BranchManager"; branchId = $kafrAbdo.id }
$fdSmouha = Invoke-Api POST "/api/users/staff" $ownerToken @{ email = "fd.smouha@codecamp.demo"; password = "DemoPass123"; fullName = "Mariam Younis"; phoneNumber = "01012340004"; role = "FrontDesk"; branchId = $smouha.id }
$teacherUserSmouha = Invoke-Api POST "/api/users/staff" $ownerToken @{ email = "teacher.smouha@codecamp.demo"; password = "DemoPass123"; fullName = "Ahmed Nabil"; phoneNumber = "01012340005"; role = "Teacher"; branchId = $null }
$teacherUserKafrAbdo = Invoke-Api POST "/api/users/staff" $ownerToken @{ email = "teacher.kafrabdo@codecamp.demo"; password = "DemoPass123"; fullName = "Sara Ibrahim"; phoneNumber = "01012340006"; role = "Teacher"; branchId = $null }

Write-Host "  2 BranchManagers, 1 FrontDesk, 2 Teachers (login accounts only so far)"

# --- 5. Teacher profiles, branch assignment, availability --------------------------------------
Write-Host "`n[5/12] Creating teacher profiles, branch assignments, and availability..." -ForegroundColor Yellow

$teacherSmouha = Invoke-Api POST "/api/teachers" $ownerToken @{ userId = $teacherUserSmouha.userId; hireDate = "2024-01-01"; payType = "Hourly"; payRate = 150 }
$teacherKafrAbdo = Invoke-Api POST "/api/teachers" $ownerToken @{ userId = $teacherUserKafrAbdo.userId; hireDate = "2024-06-01"; payType = "PerSession"; payRate = 100 }

Invoke-Api POST "/api/teachers/$($teacherSmouha.id)/branches" $ownerToken @{ branchId = $smouha.id } | Out-Null
Invoke-Api POST "/api/teachers/$($teacherKafrAbdo.id)/branches" $ownerToken @{ branchId = $kafrAbdo.id } | Out-Null

# Wide Monday window so the upcoming demo sessions below never hit an availability conflict.
Invoke-Api POST "/api/teachers/$($teacherSmouha.id)/availability" $ownerToken @{ branchId = $smouha.id; dayOfWeek = "Monday"; startTime = "09:00"; endTime = "17:00" } | Out-Null
Invoke-Api POST "/api/teachers/$($teacherKafrAbdo.id)/availability" $ownerToken @{ branchId = $kafrAbdo.id; dayOfWeek = "Monday"; startTime = "09:00"; endTime = "17:00" } | Out-Null

# A wide-open window for today (UTC), too -- one demo session below is deliberately scheduled a
# couple of hours in the past (so attendance/grading has something already-completed to act on),
# and that only avoids an availability conflict if it's covered no matter which weekday "today" is.
$todayUtcDow = (Get-Date).ToUniversalTime().DayOfWeek.ToString()
Invoke-Api POST "/api/teachers/$($teacherSmouha.id)/availability" $ownerToken @{ branchId = $smouha.id; dayOfWeek = $todayUtcDow; startTime = "00:00"; endTime = "23:59" } | Out-Null

Write-Host "  Both teachers assigned to their branch with Monday 09:00-17:00 availability"

# --- 6. Guardians and students --------------------------------------------------------------------
Write-Host "`n[6/12] Creating guardian contacts and students..." -ForegroundColor Yellow

# Guardians are contact records only (name/phone/email) - CEMS has no parent-facing login or
# self-service portal, so these are created directly by staff rather than via self-registration.
$guardianHany = Invoke-Api POST "/api/guardians" $ownerToken @{ fullName = "Hany Mahmoud"; phone = "01123450001"; email = "hany.mahmoud@example.com" }
$guardianDoaa = Invoke-Api POST "/api/guardians" $ownerToken @{ fullName = "Doaa Kamel"; phone = "01123450002"; email = "doaa.kamel@example.com" }
$guardianTarek = Invoke-Api POST "/api/guardians" $ownerToken @{ fullName = "Tarek Aboulfotouh"; phone = "01123450003"; email = "tarek.aboulfotouh@example.com" }

# A student can't be created without a guardian attached atomically -- CreateStudent takes either
# an existingGuardianId (used here, since all three guardians above already exist) or a full set
# of new-guardian fields; there's no separate "create student, then attach guardian" step anymore.
$studentKhaled = Invoke-Api POST "/api/students" $ownerToken @{ fullName = "Khaled Hany"; dateOfBirth = "2013-04-12"; gender = "Male"; branchId = $smouha.id; existingGuardianId = $guardianHany.id; relationshipType = "Father"; isPrimaryContact = $true }
$studentLina = Invoke-Api POST "/api/students" $ownerToken @{ fullName = "Lina Hany"; dateOfBirth = "2014-08-22"; gender = "Female"; branchId = $smouha.id; existingGuardianId = $guardianHany.id; relationshipType = "Father"; isPrimaryContact = $true }
$studentMalak = Invoke-Api POST "/api/students" $ownerToken @{ fullName = "Malak Kamel"; dateOfBirth = "2012-11-02"; gender = "Female"; branchId = $kafrAbdo.id; existingGuardianId = $guardianDoaa.id; relationshipType = "Mother"; isPrimaryContact = $true }
$studentOmar = Invoke-Api POST "/api/students" $ownerToken @{ fullName = "Omar Tarek"; dateOfBirth = "2011-02-15"; gender = "Male"; branchId = $smouha.id; existingGuardianId = $guardianTarek.id; relationshipType = "Father"; isPrimaryContact = $true }
$studentRana = Invoke-Api POST "/api/students" $ownerToken @{ fullName = "Rana Tarek"; dateOfBirth = "2015-09-30"; gender = "Female"; branchId = $kafrAbdo.id; existingGuardianId = $guardianTarek.id; relationshipType = "Father"; isPrimaryContact = $true }

Write-Host "  3 guardian contacts, 5 students (Hany Mahmoud and Tarek Aboulfotouh each have 2 kids)"

# --- 7. Enrollments ------------------------------------------------------------------------------
Write-Host "`n[7/12] Enrolling students in courses..." -ForegroundColor Yellow

Invoke-Api POST "/api/courses/$($pythonCourseSmouha.id)/enrollments" $ownerToken @{ studentId = $studentKhaled.id } | Out-Null
Invoke-Api POST "/api/courses/$($pythonCourseSmouha.id)/enrollments" $ownerToken @{ studentId = $studentOmar.id } | Out-Null
$enrollmentWebDev = Invoke-Api POST "/api/courses/$($webDevCourseSmouha.id)/enrollments" $ownerToken @{ studentId = $studentLina.id }
Invoke-Api POST "/api/courses/$($scratchCourseKafrAbdo.id)/enrollments" $ownerToken @{ studentId = $studentMalak.id } | Out-Null
Invoke-Api POST "/api/courses/$($scratchCourseKafrAbdo.id)/enrollments" $ownerToken @{ studentId = $studentRana.id } | Out-Null

Write-Host "  5 active enrollments"

# --- 8. Scheduled sessions -----------------------------------------------------------------------
Write-Host "`n[8/12] Scheduling sessions..." -ForegroundColor Yellow

$nextMonday = Get-NextWeekday -DayOfWeek Monday
$mondayAfter = Get-NextWeekday -DayOfWeek Monday -WeeksAhead 1
$nowUtc = (Get-Date).ToUniversalTime()

# Deliberately in the recent past (not "next Monday" like the others) so there's a completed
# session to mark attendance and record a grade against -- MarkAttendanceCommandHandler rejects
# a session that hasn't started yet or ended more than 4 hours ago, so this has to be real.
$session1 = Invoke-Api POST "/api/courses/$($pythonCourseSmouha.id)/sessions" $ownerToken @{
    roomId = $smouhaRoomA.id; teacherId = $teacherSmouha.id
    startUtc = $nowUtc.AddHours(-2).ToString("yyyy-MM-ddTHH:mm:ssZ"); endUtc = $nowUtc.AddHours(-1).ToString("yyyy-MM-ddTHH:mm:ssZ")
    override = $false; overrideReason = $null
}
Invoke-Api POST "/api/courses/$($pythonCourseSmouha.id)/sessions" $ownerToken @{
    roomId = $smouhaRoomA.id; teacherId = $teacherSmouha.id
    startUtc = $mondayAfter.AddHours(10).ToString("yyyy-MM-ddTHH:mm:ssZ"); endUtc = $mondayAfter.AddHours(11).ToString("yyyy-MM-ddTHH:mm:ssZ")
    override = $false; overrideReason = $null
} | Out-Null
Invoke-Api POST "/api/courses/$($webDevCourseSmouha.id)/sessions" $ownerToken @{
    roomId = $smouhaLab2.id; teacherId = $teacherSmouha.id
    startUtc = $nextMonday.AddHours(13).ToString("yyyy-MM-ddTHH:mm:ssZ"); endUtc = $nextMonday.AddHours(14).ToString("yyyy-MM-ddTHH:mm:ssZ")
    override = $false; overrideReason = $null
} | Out-Null
Invoke-Api POST "/api/courses/$($scratchCourseKafrAbdo.id)/sessions" $ownerToken @{
    roomId = $kafrAbdoRoomA.id; teacherId = $teacherKafrAbdo.id
    startUtc = $nextMonday.AddHours(10).ToString("yyyy-MM-ddTHH:mm:ssZ"); endUtc = $nextMonday.AddHours(11).ToString("yyyy-MM-ddTHH:mm:ssZ")
    override = $false; overrideReason = $null
} | Out-Null

Write-Host "  4 sessions scheduled across both branches"

# --- 9. Attendance --------------------------------------------------------------------------------
Write-Host "`n[9/12] Marking attendance..." -ForegroundColor Yellow

Invoke-Api POST "/api/sessions/$($session1.id)/attendance" $ownerToken @{ studentId = $studentKhaled.id; status = "Present" } | Out-Null

Write-Host "  1 attendance record marked (the rest are left Unmarked, on purpose -- demonstrates the roster view)"

# --- 10. Exam and grades ---------------------------------------------------------------------------
Write-Host "`n[10/12] Creating an exam and recording a grade..." -ForegroundColor Yellow

$exam = Invoke-Api POST "/api/courses/$($pythonCourseSmouha.id)/exams" $ownerToken @{ name = "Python Basics Assessment"; maxScore = 100; examDate = $nowUtc.ToString("yyyy-MM-dd") }
Invoke-Api POST "/api/exams/$($exam.id)/grades" $ownerToken @{ studentId = $studentKhaled.id; score = 88; comments = "Great grasp of loops and functions" } | Out-Null

Write-Host "  1 exam with 1 grade recorded"

# --- 11. Invoices and payments -----------------------------------------------------------------------
Write-Host "`n[11/12] Creating invoices and payments..." -ForegroundColor Yellow

# Fully paid, package-based.
$invoicePaid = Invoke-Api POST "/api/students/$($studentKhaled.id)/invoices" $ownerToken @{ packageId = $pythonPackageSmouha.id; amount = $null; dueDate = (Get-Date).AddDays(14).ToString("yyyy-MM-dd") }
Invoke-Api POST "/api/invoices/$($invoicePaid.id)/payments" $ownerToken @{ amountPaid = 2400; paymentDate = (Get-Date).ToString("yyyy-MM-dd"); method = "Card" } | Out-Null

# Partially paid, package-based.
$invoicePartial = Invoke-Api POST "/api/students/$($studentLina.id)/invoices" $ownerToken @{ packageId = $webDevPackageSmouha.id; amount = $null; dueDate = (Get-Date).AddDays(14).ToString("yyyy-MM-dd") }
Invoke-Api POST "/api/invoices/$($invoicePartial.id)/payments" $ownerToken @{ amountPaid = 1200; paymentDate = (Get-Date).ToString("yyyy-MM-dd"); method = "Cash" } | Out-Null

# Unpaid and overdue, ad-hoc registration fee -- demonstrates the outstanding-balance / overdue view.
Invoke-Api POST "/api/students/$($studentMalak.id)/invoices" $ownerToken @{ packageId = $null; amount = 200; dueDate = (Get-Date).AddDays(-7).ToString("yyyy-MM-dd") } | Out-Null

Write-Host "  3 invoices: 1 fully paid, 1 partially paid, 1 unpaid + overdue"

# --- 12. Payroll -------------------------------------------------------------------------------------
Write-Host "`n[12/12] Generating a payroll run..." -ForegroundColor Yellow

$payrollRun = Invoke-Api POST "/api/teachers/$($teacherSmouha.id)/payroll-runs" $ownerToken @{ periodStart = $nextMonday.ToString("yyyy-MM-dd"); periodEnd = $nextMonday.ToString("yyyy-MM-dd") }
Invoke-Api POST "/api/payroll-runs/$($payrollRun.id)/approve" $ownerToken | Out-Null

Write-Host "  1 payroll run generated and approved for Ahmed Nabil (Smouha), total: $($payrollRun.totalAmount)"

# --- Summary -----------------------------------------------------------------------------------------
Write-Host "`n=== Done. Demo accounts (all passwords: DemoPass123) ===" -ForegroundColor Cyan
Write-Host "  Owner:          $ownerEmail (Mostafa El-Sayed)"
Write-Host "  BranchManager:  bm.smouha@codecamp.demo (Nourhan Adel, Smouha), bm.kafrabdo@codecamp.demo (Hossam Fathy, Kafr Abdo)"
Write-Host "  FrontDesk:      fd.smouha@codecamp.demo (Mariam Younis, Smouha)"
Write-Host "  Teacher:        teacher.smouha@codecamp.demo (Ahmed Nabil, Smouha, Hourly), teacher.kafrabdo@codecamp.demo (Sara Ibrahim, Kafr Abdo, PerSession)"
Write-Host "  Guardians:      Hany Mahmoud (2 kids: Khaled, Lina), Doaa Kamel (1 kid: Malak), Tarek Aboulfotouh (2 kids: Omar, Rana)"
