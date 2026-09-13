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

Write-Host "=== CEMS demo data seeder ===" -ForegroundColor Cyan

# --- 1. Bootstrap the Owner account -----------------------------------------------------------
Write-Host "`n[1/12] Bootstrapping Owner account..." -ForegroundColor Yellow

$ownerEmail = "owner@cems.demo"
$ownerPassword = "DemoPass123"

# Every other account-creation path requires an authenticated Owner/staff token, so the very first
# account has to come from somewhere else. bootstrap-owner is anonymous but self-disables the
# instant any user exists (see BootstrapOwnerCommandHandler) -- it can't be used as a signup path.
$ownerToken = (Invoke-Api POST "/api/auth/bootstrap-owner" $null @{
    email = $ownerEmail; password = $ownerPassword; fullName = "Dana Owner"; phoneNumber = "0100000001"
}).token

Write-Host "  Owner ready: $ownerEmail / $ownerPassword"

# --- 2. Branches and rooms ---------------------------------------------------------------------
Write-Host "`n[2/12] Creating branches and rooms..." -ForegroundColor Yellow

$downtown = Invoke-Api POST "/api/branches" $ownerToken @{ name = "Downtown Center"; address = "1 Main St"; phone = "0200000001" }
$uptown = Invoke-Api POST "/api/branches" $ownerToken @{ name = "Uptown Center"; address = "99 High St"; phone = "0200000002" }

$downtownRoomA = Invoke-Api POST "/api/branches/$($downtown.id)/rooms" $ownerToken @{ name = "Room A"; capacity = 15 }
$downtownRoomB = Invoke-Api POST "/api/branches/$($downtown.id)/rooms" $ownerToken @{ name = "Room B"; capacity = 1 }
$uptownRoomA = Invoke-Api POST "/api/branches/$($uptown.id)/rooms" $ownerToken @{ name = "Room A"; capacity = 15 }

Write-Host "  Downtown Center ($($downtown.id)), Uptown Center ($($uptown.id))"

# --- 3. Curriculum, subjects, courses, packages ------------------------------------------------
Write-Host "`n[3/12] Creating curriculum, subjects, courses, and packages..." -ForegroundColor Yellow

$curriculum = Invoke-Api POST "/api/curricula" $ownerToken @{ name = "IG"; description = "International General Certificate" }
$mathSubject = Invoke-Api POST "/api/subjects" $ownerToken @{ name = "Mathematics"; curriculumId = $curriculum.id }
$physicsSubject = Invoke-Api POST "/api/subjects" $ownerToken @{ name = "Physics"; curriculumId = $curriculum.id }

$mathCourseDowntown = Invoke-Api POST "/api/courses" $ownerToken @{ name = "IG Mathematics - Group A"; deliveryMode = "Group"; subjectId = $mathSubject.id; branchId = $downtown.id }
$physicsCourseDowntown = Invoke-Api POST "/api/courses" $ownerToken @{ name = "IG Physics - 1:1"; deliveryMode = "OneOnOne"; subjectId = $physicsSubject.id; branchId = $downtown.id }
$mathCourseUptown = Invoke-Api POST "/api/courses" $ownerToken @{ name = "IG Mathematics - Group A"; deliveryMode = "Group"; subjectId = $mathSubject.id; branchId = $uptown.id }

$mathPackageDowntown = Invoke-Api POST "/api/courses/$($mathCourseDowntown.id)/packages" $ownerToken @{ sessionCount = 12; price = 600 }
$physicsPackageDowntown = Invoke-Api POST "/api/courses/$($physicsCourseDowntown.id)/packages" $ownerToken @{ sessionCount = 12; price = 900 }

Write-Host "  Curriculum '$($curriculum.name)' with 2 subjects, 3 courses, 2 packages"

# --- 4. Staff accounts -------------------------------------------------------------------------
Write-Host "`n[4/12] Creating staff accounts..." -ForegroundColor Yellow

$bmDowntown = Invoke-Api POST "/api/users/staff" $ownerToken @{ email = "bm.downtown@cems.demo"; password = "DemoPass123"; fullName = "Blair Manager"; phoneNumber = "0300000001"; role = "BranchManager"; branchId = $downtown.id }
$bmUptown = Invoke-Api POST "/api/users/staff" $ownerToken @{ email = "bm.uptown@cems.demo"; password = "DemoPass123"; fullName = "Uma Manager"; phoneNumber = "0300000002"; role = "BranchManager"; branchId = $uptown.id }
$fdDowntown = Invoke-Api POST "/api/users/staff" $ownerToken @{ email = "fd.downtown@cems.demo"; password = "DemoPass123"; fullName = "Frankie Desk"; phoneNumber = "0300000003"; role = "FrontDesk"; branchId = $downtown.id }
$teacherUserDowntown = Invoke-Api POST "/api/users/staff" $ownerToken @{ email = "teacher.downtown@cems.demo"; password = "DemoPass123"; fullName = "Tara Teacher"; phoneNumber = "0300000004"; role = "Teacher"; branchId = $null }
$teacherUserUptown = Invoke-Api POST "/api/users/staff" $ownerToken @{ email = "teacher.uptown@cems.demo"; password = "DemoPass123"; fullName = "Tom Teacher"; phoneNumber = "0300000005"; role = "Teacher"; branchId = $null }

Write-Host "  2 BranchManagers, 1 FrontDesk, 2 Teachers (login accounts only so far)"

# --- 5. Teacher profiles, branch assignment, availability --------------------------------------
Write-Host "`n[5/12] Creating teacher profiles, branch assignments, and availability..." -ForegroundColor Yellow

$teacherDowntown = Invoke-Api POST "/api/teachers" $ownerToken @{ userId = $teacherUserDowntown.userId; hireDate = "2024-01-01"; payType = "Hourly"; payRate = 150 }
$teacherUptown = Invoke-Api POST "/api/teachers" $ownerToken @{ userId = $teacherUserUptown.userId; hireDate = "2024-06-01"; payType = "PerSession"; payRate = 100 }

Invoke-Api POST "/api/teachers/$($teacherDowntown.id)/branches" $ownerToken @{ branchId = $downtown.id } | Out-Null
Invoke-Api POST "/api/teachers/$($teacherUptown.id)/branches" $ownerToken @{ branchId = $uptown.id } | Out-Null

# Wide Monday window so demo session times below never hit an availability conflict.
Invoke-Api POST "/api/teachers/$($teacherDowntown.id)/availability" $ownerToken @{ branchId = $downtown.id; dayOfWeek = "Monday"; startTime = "09:00"; endTime = "17:00" } | Out-Null
Invoke-Api POST "/api/teachers/$($teacherUptown.id)/availability" $ownerToken @{ branchId = $uptown.id; dayOfWeek = "Monday"; startTime = "09:00"; endTime = "17:00" } | Out-Null

Write-Host "  Both teachers assigned to their branch with Monday 09:00-17:00 availability"

# --- 6. Guardians and students --------------------------------------------------------------------
Write-Host "`n[6/12] Creating guardian contacts and students..." -ForegroundColor Yellow

# Guardians are contact records only (name/phone/email) - CEMS has no parent-facing login or
# self-service portal, so these are created directly by staff rather than via self-registration.
$guardian1 = Invoke-Api POST "/api/guardians" $ownerToken @{ fullName = "Pat Parent"; phone = "0400000001"; email = "pat.parent@example.com" }
$guardian2 = Invoke-Api POST "/api/guardians" $ownerToken @{ fullName = "Robin Parent"; phone = "0400000002"; email = "robin.parent@example.com" }

$student1 = Invoke-Api POST "/api/students" $ownerToken @{ fullName = "Sam Student"; dateOfBirth = "2013-04-12"; gender = "Male"; branchId = $downtown.id }
$student2 = Invoke-Api POST "/api/students" $ownerToken @{ fullName = "Sky Student"; dateOfBirth = "2014-08-22"; gender = "Female"; branchId = $downtown.id }
$student3 = Invoke-Api POST "/api/students" $ownerToken @{ fullName = "Riley Student"; dateOfBirth = "2012-11-02"; gender = "Female"; branchId = $uptown.id }

Invoke-Api POST "/api/students/$($student1.id)/guardians" $ownerToken @{ guardianId = $guardian1.id; relationshipType = "Father"; isPrimaryContact = $true } | Out-Null
Invoke-Api POST "/api/students/$($student2.id)/guardians" $ownerToken @{ guardianId = $guardian1.id; relationshipType = "Father"; isPrimaryContact = $true } | Out-Null
Invoke-Api POST "/api/students/$($student3.id)/guardians" $ownerToken @{ guardianId = $guardian2.id; relationshipType = "Mother"; isPrimaryContact = $true } | Out-Null

Write-Host "  2 guardian contacts, 3 students (Pat Parent has 2 kids across the same branch)"

# --- 7. Enrollments ------------------------------------------------------------------------------
Write-Host "`n[7/12] Enrolling students in courses..." -ForegroundColor Yellow

Invoke-Api POST "/api/courses/$($mathCourseDowntown.id)/enrollments" $ownerToken @{ studentId = $student1.id } | Out-Null
$enrollment2 = Invoke-Api POST "/api/courses/$($physicsCourseDowntown.id)/enrollments" $ownerToken @{ studentId = $student2.id }
Invoke-Api POST "/api/courses/$($mathCourseUptown.id)/enrollments" $ownerToken @{ studentId = $student3.id } | Out-Null

Write-Host "  3 active enrollments"

# --- 8. Scheduled sessions -----------------------------------------------------------------------
Write-Host "`n[8/12] Scheduling sessions..." -ForegroundColor Yellow

$nextMonday = Get-NextWeekday -DayOfWeek Monday
$mondayAfter = Get-NextWeekday -DayOfWeek Monday -WeeksAhead 1

$session1 = Invoke-Api POST "/api/courses/$($mathCourseDowntown.id)/sessions" $ownerToken @{
    roomId = $downtownRoomA.id; teacherId = $teacherDowntown.id
    startUtc = $nextMonday.AddHours(10).ToString("yyyy-MM-ddTHH:mm:ssZ"); endUtc = $nextMonday.AddHours(11).ToString("yyyy-MM-ddTHH:mm:ssZ")
    override = $false; overrideReason = $null
}
Invoke-Api POST "/api/courses/$($mathCourseDowntown.id)/sessions" $ownerToken @{
    roomId = $downtownRoomA.id; teacherId = $teacherDowntown.id
    startUtc = $mondayAfter.AddHours(10).ToString("yyyy-MM-ddTHH:mm:ssZ"); endUtc = $mondayAfter.AddHours(11).ToString("yyyy-MM-ddTHH:mm:ssZ")
    override = $false; overrideReason = $null
} | Out-Null
Invoke-Api POST "/api/courses/$($physicsCourseDowntown.id)/sessions" $ownerToken @{
    roomId = $downtownRoomB.id; teacherId = $teacherDowntown.id
    startUtc = $nextMonday.AddHours(13).ToString("yyyy-MM-ddTHH:mm:ssZ"); endUtc = $nextMonday.AddHours(14).ToString("yyyy-MM-ddTHH:mm:ssZ")
    override = $false; overrideReason = $null
} | Out-Null
Invoke-Api POST "/api/courses/$($mathCourseUptown.id)/sessions" $ownerToken @{
    roomId = $uptownRoomA.id; teacherId = $teacherUptown.id
    startUtc = $nextMonday.AddHours(10).ToString("yyyy-MM-ddTHH:mm:ssZ"); endUtc = $nextMonday.AddHours(11).ToString("yyyy-MM-ddTHH:mm:ssZ")
    override = $false; overrideReason = $null
} | Out-Null

Write-Host "  4 sessions scheduled across both branches"

# --- 9. Attendance --------------------------------------------------------------------------------
Write-Host "`n[9/12] Marking attendance..." -ForegroundColor Yellow

Invoke-Api POST "/api/sessions/$($session1.id)/attendance" $ownerToken @{ studentId = $student1.id; status = "Present" } | Out-Null

Write-Host "  1 attendance record marked (the rest are left Unmarked, on purpose -- demonstrates the roster view)"

# --- 10. Exam and grades ---------------------------------------------------------------------------
Write-Host "`n[10/12] Creating an exam and recording a grade..." -ForegroundColor Yellow

$exam = Invoke-Api POST "/api/courses/$($mathCourseDowntown.id)/exams" $ownerToken @{ name = "Term 1 Midterm"; maxScore = 100; examDate = $nextMonday.ToString("yyyy-MM-dd") }
Invoke-Api POST "/api/exams/$($exam.id)/grades" $ownerToken @{ studentId = $student1.id; score = 82; comments = "Solid work" } | Out-Null

Write-Host "  1 exam with 1 grade recorded"

# --- 11. Invoices and payments -----------------------------------------------------------------------
Write-Host "`n[11/12] Creating invoices and payments..." -ForegroundColor Yellow

# Fully paid, package-based.
$invoicePaid = Invoke-Api POST "/api/students/$($student1.id)/invoices" $ownerToken @{ packageId = $mathPackageDowntown.id; amount = $null; dueDate = (Get-Date).AddDays(14).ToString("yyyy-MM-dd") }
Invoke-Api POST "/api/invoices/$($invoicePaid.id)/payments" $ownerToken @{ amountPaid = 600; paymentDate = (Get-Date).ToString("yyyy-MM-dd"); method = "Card" } | Out-Null

# Partially paid, package-based.
$invoicePartial = Invoke-Api POST "/api/students/$($student2.id)/invoices" $ownerToken @{ packageId = $physicsPackageDowntown.id; amount = $null; dueDate = (Get-Date).AddDays(14).ToString("yyyy-MM-dd") }
Invoke-Api POST "/api/invoices/$($invoicePartial.id)/payments" $ownerToken @{ amountPaid = 300; paymentDate = (Get-Date).ToString("yyyy-MM-dd"); method = "Cash" } | Out-Null

# Unpaid and overdue, ad-hoc registration fee -- demonstrates the outstanding-balance / overdue view.
Invoke-Api POST "/api/students/$($student3.id)/invoices" $ownerToken @{ packageId = $null; amount = 50; dueDate = (Get-Date).AddDays(-7).ToString("yyyy-MM-dd") } | Out-Null

Write-Host "  3 invoices: 1 fully paid, 1 partially paid, 1 unpaid + overdue"

# --- 12. Payroll -------------------------------------------------------------------------------------
Write-Host "`n[12/12] Generating a payroll run..." -ForegroundColor Yellow

$payrollRun = Invoke-Api POST "/api/teachers/$($teacherDowntown.id)/payroll-runs" $ownerToken @{ periodStart = $nextMonday.ToString("yyyy-MM-dd"); periodEnd = $nextMonday.ToString("yyyy-MM-dd") }
Invoke-Api POST "/api/payroll-runs/$($payrollRun.id)/approve" $ownerToken | Out-Null

Write-Host "  1 payroll run generated and approved for Tara Teacher (Downtown), total: $($payrollRun.totalAmount)"

# --- Summary -----------------------------------------------------------------------------------------
Write-Host "`n=== Done. Demo accounts (all passwords: DemoPass123) ===" -ForegroundColor Cyan
Write-Host "  Owner:          $ownerEmail"
Write-Host "  BranchManager:  bm.downtown@cems.demo (Downtown), bm.uptown@cems.demo (Uptown)"
Write-Host "  FrontDesk:      fd.downtown@cems.demo (Downtown)"
Write-Host "  Teacher:        teacher.downtown@cems.demo (Downtown, Hourly), teacher.uptown@cems.demo (Uptown, PerSession)"
Write-Host "  Guardians:      Pat Parent (contact only, 2 kids: Sam, Sky), Robin Parent (contact only, 1 kid: Riley)"
