# SeedCS3CompleteFolders.ps1
# Completes Adeel CS3 course folders (OC 666/667/668) and submits them to HOD.
$ErrorActionPreference = "Stop"

$ApiRoot = "e:\Projects\Folder Management\Project\FolderManagementSystemAPI\FolderManagementSystemAPI"
$Server = "DESKTOP-JPM26B8\FOLDERMANAGEMENT"
$Database = "FolderManagementDB"
$ApiBase = "http://127.0.0.1:5161/api"
$TeacherId = 9

function New-MinimalPdf([string]$text) {
    $escaped = ($text -replace '\\', '\\' -replace '\(', '\(' -replace '\)', '\)')
    if ($escaped.Length -gt 80) { $escaped = $escaped.Substring(0, 80) }
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

function Ensure-Pdf([string]$path, [string]$label) {
    $dir = Split-Path $path -Parent
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    [System.IO.File]::WriteAllBytes($path, (New-MinimalPdf $label))
}

function Invoke-Sql([string]$query) {
    sqlcmd -S $Server -U sa -P Folder -C -d $Database -Q $query -h -1 -W
}

Write-Host "Logging in as Adeel..."
$login = Invoke-RestMethod -Uri "$ApiBase/Auth/login" -Method POST -ContentType "application/json" `
    -Body (@{ email = "adeel@biit.edu.pk"; password = "Teacher@123" } | ConvertTo-Json)
$headers = @{ Authorization = "Bearer $($login.token)" }

$ocs = @(666, 667, 668)
foreach ($ocId in $ocs) {
    Write-Host "Ensuring master folder for OC $ocId..."
    try {
        Invoke-RestMethod -Uri "$ApiBase/TeacherSubmission/offered-course/$ocId/create-master-folder?teacherUserId=$TeacherId" `
            -Method POST -Headers $headers | Out-Null
    } catch {
        # Already exists is fine; continue
        Write-Host "  master-folder call: $($_.Exception.Message)"
    }
}

# Resolve folder paths from DB
$folderRows = sqlcmd -S $Server -U sa -P Folder -C -d $Database -h -1 -W -s "|" -Q "
SET NOCOUNT ON;
SELECT oc.OfferedCourseID, cf.CourseFolderID, cf.RootFolderName, cf.RootFolderPath, c.CourseCode
FROM CourseFolders cf
JOIN OfferedCourses oc ON oc.OfferedCourseID = cf.OfferedCourseID
JOIN Courses c ON c.CourseID = oc.CourseID
WHERE oc.OfferedCourseID IN (666,667,668);
"

$templates = @{
    a = @{ Name = "a-Course Objectives"; Cat = "Course Objective"; Files = @(@{ N = "CourseObjective_01.pdf"; L = "Course Objective" }) }
    b = @{ Name = "b-CLO-Task Mapping"; Cat = "CLO-Task Mapping"; Files = @(@{ N = "CLO_Task_Mapping_01.pdf"; L = "CLO-Task Mapping" }) }
    c = @{ Name = "c-Course Contents"; Cat = "Course Content"; Files = @(@{ N = "CourseContent_01.pdf"; L = "Course Content" }) }
    d = @{ Name = "d-Weekly Plan"; Cat = "Weekly Plan"; Files = @(@{ N = "WeeklyPlan_01.pdf"; L = "Weekly Plan" }) }
    e = @{ Name = "e-Attendance Record"; Cat = "Attendance Record"; Files = @(@{ N = "AttendanceRecord_01.pdf"; L = "Attendance Record" }) }
    f = @{ Name = "f-Reference Material"; Cat = "Reference Material"; Files = @(@{ N = "ReferenceMaterial_01.pdf"; L = "Reference Material" }) }
    j = @{ Name = "j-Marks Distribution and Grading"; Cat = "Marks Distribution and Grading"; Files = @(@{ N = "MarksDistribution_01.pdf"; L = "Marks Distribution" }) }
    k = @{ Name = "k-Result"; Cat = "Result"; Files = @(@{ N = "Result_01.pdf"; L = "Result" }) }
    l = @{ Name = "l-Outcomes Assessment"; Cat = "Outcomes Assessment"; Files = @(@{ N = "OutcomesAssessment_01.pdf"; L = "Outcomes Assessment" }) }
    m = @{ Name = "m-Technology Involved"; Cat = "Technology Involved"; Files = @(@{ N = "TechnologyInvolved_01.pdf"; L = "Technology Involved" }) }
    n = @{ Name = "n-Design Techniques Practiced"; Cat = "Design Techniques Practiced"; Files = @(@{ N = "DesignTechniques_01.pdf"; L = "Design Techniques" }) }
    o = @{ Name = "o-Complete Analysis of Course"; Cat = "Complete Analysis of Course"; Files = @(@{ N = "CompleteAnalysis_01.pdf"; L = "Complete Analysis" }) }
}

# Actual template folder names from DB (FolderItemName)
$dirNames = @{}
$sqlDirs = sqlcmd -S $Server -U sa -P Folder -C -d $Database -h -1 -W -s "|" -Q "
SET NOCOUNT ON;
SELECT TemplateCode, FolderItemName FROM CourseFolderTemplateItems;
"
foreach ($line in ($sqlDirs -split "`n")) {
    $line = $line.Trim()
    if (-not $line -or $line -notmatch '\|') { continue }
    $parts = $line -split '\|'
    if ($parts.Count -ge 2) {
        $code = $parts[0].Trim()
        $item = $parts[1].Trim()
        $dirNames[$code] = "$code-$item"
    }
}

function Get-GhiFiles {
    $files = @()
    1..4 | ForEach-Object {
        $files += @{ Code = "g"; Name = "Assignment${_}_Questions.pdf"; Cat = "Assignment Question"; Label = "Assignment $_ Q" }
        $files += @{ Code = "h"; Name = "Assignment${_}_Solution.pdf"; Cat = "Assignment Solution"; Label = "Assignment $_ Sol" }
    }
    1..8 | ForEach-Object {
        $files += @{ Code = "g"; Name = "Quiz${_}_Questions.pdf"; Cat = "Quiz Question"; Label = "Quiz $_ Q" }
        $files += @{ Code = "h"; Name = "Quiz${_}_Solution.pdf"; Cat = "Quiz Solution"; Label = "Quiz $_ Sol" }
    }
    $files += @{ Code = "g"; Name = "MidExam1_Questions.pdf"; Cat = "Mid Exam Question"; Label = "Mid Q" }
    $files += @{ Code = "g"; Name = "FinalExam1_Questions.pdf"; Cat = "Final Exam Question"; Label = "Final Q" }
    $files += @{ Code = "h"; Name = "MidExam1_Solution.pdf"; Cat = "Mid Exam Solution"; Label = "Mid Sol" }
    $files += @{ Code = "h"; Name = "FinalExam1_Solution.pdf"; Cat = "Final Exam Solution"; Label = "Final Sol" }

    $samples = @(
        @{ N = "Assignment_Best.pdf"; C = "Assignment Best Sample" },
        @{ N = "Assignment_Average.pdf"; C = "Assignment Average Sample" },
        @{ N = "Assignment_Worst.pdf"; C = "Assignment Worst Sample" },
        @{ N = "Quiz_Best.pdf"; C = "Quiz Best Sample" },
        @{ N = "Quiz_Average.pdf"; C = "Quiz Average Sample" },
        @{ N = "Quiz_Worst.pdf"; C = "Quiz Worst Sample" },
        @{ N = "Mid_Best.pdf"; C = "Mid Exam Best Sample" },
        @{ N = "Mid_Average.pdf"; C = "Mid Exam Average Sample" },
        @{ N = "Mid_Worst.pdf"; C = "Mid Exam Worst Sample" },
        @{ N = "Final_Best.pdf"; C = "Final Exam Best Sample" },
        @{ N = "Final_Average.pdf"; C = "Final Exam Average Sample" },
        @{ N = "Final_Worst.pdf"; C = "Final Exam Worst Sample" }
    )
    foreach ($s in $samples) {
        $files += @{ Code = "i"; Name = $s.N; Cat = $s.C; Label = $s.C }
    }
    return $files
}

$ghi = Get-GhiFiles

foreach ($ocId in $ocs) {
    $meta = sqlcmd -S $Server -U sa -P Folder -C -d $Database -h -1 -W -s "|" -Q "
SET NOCOUNT ON;
SELECT cf.CourseFolderID, cf.RootFolderPath, cf.RootFolderName, c.CourseCode, cf.FolderStatus
FROM CourseFolders cf
JOIN OfferedCourses oc ON oc.OfferedCourseID = cf.OfferedCourseID
JOIN Courses c ON c.CourseID = oc.CourseID
WHERE oc.OfferedCourseID = $ocId;
"
    $meta = ($meta | Where-Object { $_ -and $_.Trim() -ne "" } | Select-Object -First 1)
    if (-not $meta) { throw "No CourseFolder for OC $ocId" }
    $parts = $meta.Trim() -split '\|'
    $folderId = [int]$parts[0].Trim()
    $rootRel = $parts[1].Trim()
    $rootName = $parts[2].Trim()
    $courseCode = $parts[3].Trim()
    $status = $parts[4].Trim()

    if ($status -eq "Submitted" -or $status -eq "Approved") {
        Write-Host "OC $ocId ($courseCode) already $status — skip fill, will try submit only if Generated."
        continue
    }

    Write-Host "Filling folder $folderId for $courseCode ($rootRel)..."

    # Ensure template directories in DB + disk (single-quoted so @@FETCH_STATUS is not parsed by PowerShell)
    $ensureSql = @'
SET NOCOUNT ON;
DECLARE @FolderId INT = __FOLDER_ID__;
DECLARE @Root NVARCHAR(400) = N'__ROOT_REL__';
DECLARE @Code NVARCHAR(5), @Item NVARCHAR(200), @Tid INT, @DirName NVARCHAR(300), @DirPath NVARCHAR(500);

DECLARE tcur CURSOR LOCAL FAST_FORWARD FOR
    SELECT TemplateCode, FolderItemName, TemplateItemID FROM CourseFolderTemplateItems ORDER BY SortOrder;
OPEN tcur;
FETCH NEXT FROM tcur INTO @Code, @Item, @Tid;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @DirName = CONCAT(@Code, N'-', @Item);
    SET @DirPath = CONCAT(@Root, N'/', @DirName);
    IF NOT EXISTS (SELECT 1 FROM CourseFolderDirectories WHERE CourseFolderID=@FolderId AND TemplateItemID=@Tid)
    BEGIN
        INSERT INTO CourseFolderDirectories (CourseFolderID, ParentDirectoryID, TemplateItemID, DirectoryName, DirectoryPath)
        VALUES (@FolderId, NULL, @Tid, @DirName, @DirPath);
    END
    ELSE
    BEGIN
        UPDATE CourseFolderDirectories
        SET DirectoryName=@DirName, DirectoryPath=@DirPath
        WHERE CourseFolderID=@FolderId AND TemplateItemID=@Tid;
    END
    FETCH NEXT FROM tcur INTO @Code, @Item, @Tid;
END
CLOSE tcur;
DEALLOCATE tcur;

DELETE FROM CourseFolderFiles
WHERE DirectoryID IN (SELECT DirectoryID FROM CourseFolderDirectories WHERE CourseFolderID=@FolderId);
'@
    $ensureSql = $ensureSql.Replace('__FOLDER_ID__', "$folderId").Replace('__ROOT_REL__', ($rootRel.Replace("'", "''")))
    sqlcmd -S $Server -U sa -P Folder -C -d $Database -Q $ensureSql | Out-Null

    $absRoot = Join-Path $ApiRoot ($rootRel -replace '/', '\')
    if (-not (Test-Path $absRoot)) { New-Item -ItemType Directory -Path $absRoot -Force | Out-Null }

    # Build file list for this course
    $allFiles = @()
    foreach ($code in @("a","b","c","d","e","f","j","k","l","m","n","o")) {
        $dn = $dirNames[$code]
        if (-not $dn) { $dn = $templates[$code].Name }
        $cat = $templates[$code].Cat
        $fname = $templates[$code].Files[0].N
        $label = "$courseCode $($templates[$code].Files[0].L)"
        $allFiles += @{ Code = $code; Dir = $dn; Name = $fname; Cat = $cat; Label = $label }
    }
    foreach ($f in $ghi) {
        $dn = $dirNames[$f.Code]
        $allFiles += @{ Code = $f.Code; Dir = $dn; Name = $f.Name; Cat = $f.Cat; Label = "$courseCode $($f.Label)" }
    }

    $insertLines = New-Object System.Collections.Generic.List[string]
    foreach ($f in $allFiles) {
        $relDir = "$rootRel/$($f.Dir)"
        $relFile = "$relDir/$($f.Name)"
        $absFile = Join-Path $ApiRoot ($relFile -replace '/', '\')
        Ensure-Pdf $absFile $f.Label

        $safeName = $f.Name.Replace("'", "''")
        $safePath = $relFile.Replace("'", "''")
        $safeCat = $f.Cat.Replace("'", "''")
        $safeCode = $f.Code
        $sqlLine = "INSERT INTO CourseFolderFiles (DirectoryID, UploadedByUserID, FileName, FilePath, FileCategory, Remarks) " +
            "SELECT d.DirectoryID, $TeacherId, N'$safeName', N'$safePath', N'$safeCat', N'CS3 complete folder seed' " +
            "FROM CourseFolderDirectories d " +
            "JOIN CourseFolderTemplateItems t ON t.TemplateItemID = d.TemplateItemID " +
            "WHERE d.CourseFolderID = $folderId AND t.TemplateCode = N'$safeCode';"
        $insertLines.Add($sqlLine)
    }

    $batchSql = "SET NOCOUNT ON;`n" + ($insertLines -join "`n")
    $tmp = Join-Path $env:TEMP "cs3_folder_$ocId.sql"
    Set-Content -Path $tmp -Value $batchSql -Encoding UTF8
    sqlcmd -S $Server -U sa -P Folder -C -d $Database -i $tmp | Out-Null
    Write-Host "  Inserted files for $courseCode"
}

Write-Host "`nSubmitting folders to HOD..."
foreach ($ocId in $ocs) {
    try {
        $result = Invoke-RestMethod -Uri "$ApiBase/TeacherSubmission/offered-course/$ocId/submit?teacherUserId=$TeacherId" `
            -Method POST -Headers $headers
        Write-Host "OC $ocId : $($result.message) -> $($result.submittedFolderPath)"
    } catch {
        $msg = $_.ErrorDetails.Message
        if (-not $msg) { $msg = $_.Exception.Message }
        Write-Host "OC $ocId submit failed: $msg"
    }
}

Write-Host "`nFinal status:"
$statusSql = @'
SET NOCOUNT ON;
SELECT oc.OfferedCourseID, c.CourseCode, cf.FolderStatus, cf.RootFolderPath,
  (SELECT COUNT(*) FROM CourseFolderFiles f
   JOIN CourseFolderDirectories d ON d.DirectoryID=f.DirectoryID
   WHERE d.CourseFolderID=cf.CourseFolderID) AS Files
FROM CourseFolders cf
JOIN OfferedCourses oc ON oc.OfferedCourseID=cf.OfferedCourseID
JOIN Courses c ON c.CourseID=oc.CourseID
WHERE oc.OfferedCourseID IN (666,667,668)
ORDER BY oc.OfferedCourseID;
'@
sqlcmd -S $Server -U sa -P Folder -C -d $Database -Q $statusSql
