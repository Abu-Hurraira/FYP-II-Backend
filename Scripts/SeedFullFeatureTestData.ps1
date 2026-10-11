# SeedFullFeatureTestData.ps1
# Builds master-folder PDFs + runs SQL for end-to-end feature testing.
# Target: CAI-261 BAI-4A (OC1) complete assessments + submitted folder for HOD review
#          CAI-262 BAI-4A (OC2) pending content for HOD approve/reject
#          CAI-261 BAI-4B (OC8) partial folder for teacher upload testing

$ErrorActionPreference = "Stop"

$ApiRoot = "e:\Projects\Folder Management\Project\FolderManagementSystemAPI\FolderManagementSystemAPI"
$SqlScript = "e:\Projects\Folder Management\Project\FolderManagementSystemAPI\Scripts\SeedFullFeatureTestData.sql"
$PdfSource = "e:\Projects\Folder Management\UploadFiles\Project_TestFiles_PDF\SubmissionUploadFiles"
$Server = "DESKTOP-JPM26B8\FOLDERMANAGEMENT"
$Database = "FolderManagementDB"

function New-MinimalPdf([string]$text) {
    $escaped = $text -replace '\\', '\\\\' -replace '\(', '\(' -replace '\)', '\)'
    $stream = "BT /F1 12 Tf 50 750 Td ($escaped) Tj ET"
    $len = [System.Text.Encoding]::ASCII.GetByteCount($stream)
    $pdf = @"
%PDF-1.4
1 0 obj<< /Type /Catalog /Pages 2 0 R >>endobj
2 0 obj<< /Type /Pages /Kids [3 0 R] /Count 1 >>endobj
3 0 obj<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources<< /Font<< /F1 5 0 R >> >> >>endobj
4 0 obj<< /Length $len >>stream
$stream
endstream
endobj
5 0 obj<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>endobj
xref
0 6
0000000000 65535 f 
trailer<< /Size 6 /Root 1 0 R >>
startxref
0
%%EOF
"@
    return [System.Text.Encoding]::ASCII.GetBytes($pdf)
}

function Ensure-Pdf([string]$path, [string]$label, [string]$sourcePath = $null) {
    $dir = Split-Path $path -Parent
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    if ($sourcePath -and (Test-Path $sourcePath)) {
        Copy-Item $sourcePath $path -Force
    } else {
        [System.IO.File]::WriteAllBytes($path, (New-MinimalPdf $label))
    }
}

function Get-SourcePdf([string]$rel) {
    $p = Join-Path $PdfSource $rel
    if (Test-Path $p) { return $p }
    return $null
}

Write-Host "Creating CAI-261 BAI-4A master folder files..."

$owner = "AftabProgrammingforAIBAI4A"
$master = Join-Path $ApiRoot "src\Folders\$owner\MasterFolder"

$dirs = @{
    a = "a-Course Objectives"
    b = "b-CLO-GA-Mapping"
    c = "c-Course Contents"
    d = "d-Weekly Plan"
    e = "e-Attendance Record"
    f = "f-Reference Material"
    g = "g-Quiz-Assignments-Exams"
    h = "h-Solutions"
    i = "i-Samples"
    j = "j-Marks Distribution and Grading"
    k = "k-Result"
    l = "l-Outcomes Assessment"
    m = "m-Technology Involved"
    n = "n-Design Techniques Practiced"
    o = "o-Complete Analysis of Course"
}

foreach ($d in $dirs.Values) {
    New-Item -ItemType Directory -Path (Join-Path $master $d) -Force | Out-Null
}

# a-d auto-style placeholders
Ensure-Pdf (Join-Path $master "$($dirs.a)\CourseObjective_01.pdf") "Course Objective CAI-261" (Get-SourcePdf "a-Course Objectives\CourseObjective_01.pdf")
Ensure-Pdf (Join-Path $master "$($dirs.b)\CLO_Task_Mapping_01.pdf") "CLO-Task Mapping CAI-261" (Get-SourcePdf "b-CLO-GA-Mapping\CLO_GA_Mapping_01.pdf")
Ensure-Pdf (Join-Path $master "$($dirs.c)\CourseContent_01.pdf") "Course Content CAI-261" (Get-SourcePdf "c-Course Contents\CourseContent_01.pdf")
Ensure-Pdf (Join-Path $master "$($dirs.d)\WeeklyPlan_01.pdf") "Weekly Plan CAI-261" (Get-SourcePdf "d-Weekly Plan\WeeklyPlan_01.pdf")
Ensure-Pdf (Join-Path $master "$($dirs.e)\AttendanceRecord_01.pdf") "Attendance Record" (Get-SourcePdf "e-Attendance Record\AttendanceRecord_01.pdf")
Ensure-Pdf (Join-Path $master "$($dirs.f)\ReferenceMaterial_01.pdf") "Reference Material" (Get-SourcePdf "f-Reference Material\ReferenceMaterial_01.pdf")

# g questions: 4 assignment + 8 quiz + mid + final
1..4 | ForEach-Object {
    Ensure-Pdf (Join-Path $master "$($dirs.g)\Assignment$_`_Questions.pdf") "Assignment $_ Questions" `
        (Get-SourcePdf ("g-Quiz-Assignments-Exams\AssignmentQuestion_{0:D2}.pdf" -f $_))
}
1..8 | ForEach-Object {
    Ensure-Pdf (Join-Path $master "$($dirs.g)\Quiz$_`_Questions.pdf") "Quiz $_ Questions" `
        (Get-SourcePdf ("g-Quiz-Assignments-Exams\QuizQuestion_{0:D2}.pdf" -f $_))
}
Ensure-Pdf (Join-Path $master "$($dirs.g)\MidExam1_Questions.pdf") "Mid Exam Questions" (Get-SourcePdf "g-Quiz-Assignments-Exams\MidExamQuestion_01.pdf")
Ensure-Pdf (Join-Path $master "$($dirs.g)\FinalExam1_Questions.pdf") "Final Exam Questions" (Get-SourcePdf "g-Quiz-Assignments-Exams\FinalExamQuestion_01.pdf")

# h solutions
1..4 | ForEach-Object {
    Ensure-Pdf (Join-Path $master "$($dirs.h)\Assignment$_`_Solution.pdf") "Assignment $_ Solution" `
        (Get-SourcePdf ("h-Solutions\AssignmentSolution_{0:D2}.pdf" -f $_))
}
1..8 | ForEach-Object {
    Ensure-Pdf (Join-Path $master "$($dirs.h)\Quiz$_`_Solution.pdf") "Quiz $_ Solution" `
        (Get-SourcePdf ("h-Solutions\QuizSolution_{0:D2}.pdf" -f $_))
}
Ensure-Pdf (Join-Path $master "$($dirs.h)\MidExam1_Solution.pdf") "Mid Solution" (Get-SourcePdf "h-Solutions\MidExamSolution_01.pdf")
Ensure-Pdf (Join-Path $master "$($dirs.h)\FinalExam1_Solution.pdf") "Final Solution" (Get-SourcePdf "h-Solutions\FinalExamSolution_01.pdf")

# i samples (1 best/avg/worst per type)
$sampleMap = @(
    @{ Name = "Assignment_Best.pdf"; Cat = "Assignment Best"; Src = "i-Samples\AssignmentBestSample_01.pdf" },
    @{ Name = "Assignment_Average.pdf"; Cat = "Assignment Average"; Src = "i-Samples\AssignmentAverageSample_01.pdf" },
    @{ Name = "Assignment_Worst.pdf"; Cat = "Assignment Worst"; Src = "i-Samples\AssignmentWorstSample_01.pdf" },
    @{ Name = "Quiz_Best.pdf"; Cat = "Quiz Best"; Src = "i-Samples\QuizBestSample_01.pdf" },
    @{ Name = "Quiz_Average.pdf"; Cat = "Quiz Average"; Src = "i-Samples\QuizAverageSample_01.pdf" },
    @{ Name = "Quiz_Worst.pdf"; Cat = "Quiz Worst"; Src = "i-Samples\QuizWorstSample_01.pdf" },
    @{ Name = "Mid_Best.pdf"; Cat = "Mid Best"; Src = "i-Samples\MidExamBestSample_01.pdf" },
    @{ Name = "Mid_Average.pdf"; Cat = "Mid Average"; Src = "i-Samples\MidExamAverageSample_01.pdf" },
    @{ Name = "Mid_Worst.pdf"; Cat = "Mid Worst"; Src = "i-Samples\MidExamWorstSample_01.pdf" },
    @{ Name = "Final_Best.pdf"; Cat = "Final Best"; Src = "i-Samples\FinalExamBestSample_01.pdf" },
    @{ Name = "Final_Average.pdf"; Cat = "Final Average"; Src = "i-Samples\FinalExamAverageSample_01.pdf" },
    @{ Name = "Final_Worst.pdf"; Cat = "Final Worst"; Src = "i-Samples\FinalExamWorstSample_01.pdf" }
)
foreach ($s in $sampleMap) {
    Ensure-Pdf (Join-Path $master "$($dirs.i)\$($s.Name)") $s.Cat (Get-SourcePdf $s.Src)
}

Ensure-Pdf (Join-Path $master "$($dirs.j)\MarksDistribution_01.pdf") "Marks Distribution" (Get-SourcePdf "j-Marks Distribution and Grading\MarksDistribution_01.pdf")
Ensure-Pdf (Join-Path $master "$($dirs.k)\Result_01.pdf") "Result" (Get-SourcePdf "k-Result\Result_01.pdf")
Ensure-Pdf (Join-Path $master "$($dirs.l)\OutcomesAssessment_01.pdf") "Outcomes Assessment" (Get-SourcePdf "l-Outcomes Assessment\OutcomesAssessment_01.pdf")
Ensure-Pdf (Join-Path $master "$($dirs.m)\TechnologyInvolved_01.pdf") "Technology Involved" (Get-SourcePdf "m-Technology Involved\TechnologyInvolved_01.pdf")
Ensure-Pdf (Join-Path $master "$($dirs.n)\DesignTechniques_01.pdf") "Design Techniques" (Get-SourcePdf "n-Design Techniques Practiced\DesignTechniquesPracticed_01.pdf")
Ensure-Pdf (Join-Path $master "$($dirs.o)\CompleteAnalysis_01.pdf") "Complete Analysis" (Get-SourcePdf "o-Complete Analysis of Course\CompleteAnalysisOfCourse_01.pdf")

# Partial folder for OC8 (Shahid Jamil) — incomplete on purpose
Write-Host "Creating partial CAI-261 BAI-4B folder..."
$ownerB = "ShahidJamilProgrammingforAIBAI4B"
$masterB = Join-Path $ApiRoot "src\Folders\$ownerB\MasterFolder"
New-Item -ItemType Directory -Path (Join-Path $masterB $dirs.e) -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $masterB $dirs.f) -Force | Out-Null
Ensure-Pdf (Join-Path $masterB "$($dirs.e)\AttendanceRecord_partial.pdf") "Partial Attendance BAI-4B"
Ensure-Pdf (Join-Path $masterB "$($dirs.f)\ReferenceMaterial_partial.pdf") "Partial Reference BAI-4B"

Write-Host "Running SQL seed..."
sqlcmd -S $Server -d $Database -E -i $SqlScript
if ($LASTEXITCODE -ne 0) { throw "sqlcmd failed with exit $LASTEXITCODE" }

Write-Host ""
Write-Host "Seed complete."
Write-Host "Master folder A: $master"
Write-Host "Master folder B (partial): $masterB"
