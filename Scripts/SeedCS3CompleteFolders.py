"""Complete Adeel CS3 course folders (OC 666/667/668) and submit them to HOD."""
from __future__ import annotations

import json
import os
import subprocess
import urllib.error
import urllib.request
from pathlib import Path

API_ROOT = Path(r"e:\Projects\Folder Management\Project\FolderManagementSystemAPI\FolderManagementSystemAPI")
SERVER = r"DESKTOP-JPM26B8\FOLDERMANAGEMENT"
DATABASE = "FolderManagementDB"
API_BASE = "http://127.0.0.1:5161/api"
TEACHER_ID = 9
OCS = (666, 667, 668)

SIMPLE = {
    "a": ("Course Objective", "CourseObjective_01.pdf"),
    "b": ("CLO-Task Mapping", "CLO_Task_Mapping_01.pdf"),
    "c": ("Course Content", "CourseContent_01.pdf"),
    "d": ("Weekly Plan", "WeeklyPlan_01.pdf"),
    "e": ("Attendance Record", "AttendanceRecord_01.pdf"),
    "f": ("Reference Material", "ReferenceMaterial_01.pdf"),
    "j": ("Marks Distribution and Grading", "MarksDistribution_01.pdf"),
    "k": ("Result", "Result_01.pdf"),
    "l": ("Outcomes Assessment", "OutcomesAssessment_01.pdf"),
    "m": ("Technology Involved", "TechnologyInvolved_01.pdf"),
    "n": ("Design Techniques Practiced", "DesignTechniques_01.pdf"),
    "o": ("Complete Analysis of Course", "CompleteAnalysis_01.pdf"),
}


def sqlcmd(query: str) -> str:
    result = subprocess.run(
        [
            "sqlcmd",
            "-S",
            SERVER,
            "-U",
            "sa",
            "-P",
            "Folder",
            "-C",
            "-d",
            DATABASE,
            "-h",
            "-1",
            "-W",
            "-s",
            "|",
            "-Q",
            query,
        ],
        capture_output=True,
        text=True,
        check=False,
    )
    if result.returncode != 0:
        raise RuntimeError(result.stderr or result.stdout or "sqlcmd failed")
    return result.stdout


def sqlcmd_file(path: Path) -> None:
    result = subprocess.run(
        [
            "sqlcmd",
            "-S",
            SERVER,
            "-U",
            "sa",
            "-P",
            "Folder",
            "-C",
            "-d",
            DATABASE,
            "-i",
            str(path),
        ],
        capture_output=True,
        text=True,
        check=False,
    )
    if result.returncode != 0:
        raise RuntimeError(result.stderr or result.stdout or "sqlcmd -i failed")


def api(method: str, path: str, token: str | None = None, body: dict | None = None):
    data = None
    headers = {"Content-Type": "application/json"}
    if token:
        headers["Authorization"] = f"Bearer {token}"
    if body is not None:
        data = json.dumps(body).encode("utf-8")
    req = urllib.request.Request(
        f"{API_BASE}{path}", data=data, headers=headers, method=method
    )
    try:
        with urllib.request.urlopen(req, timeout=120) as resp:
            raw = resp.read().decode("utf-8")
            return json.loads(raw) if raw else {}
    except urllib.error.HTTPError as ex:
        detail = ex.read().decode("utf-8", errors="replace")
        raise RuntimeError(f"{ex.code} {detail}") from ex


def minimal_pdf(text: str) -> bytes:
    escaped = (
        text.replace("\\", "\\\\").replace("(", "\\(").replace(")", "\\)")[:80]
    )
    stream = f"BT /F1 12 Tf 50 750 Td ({escaped}) Tj ET"
    length = len(stream.encode("ascii"))
    pdf = f"""%PDF-1.4
1 0 obj<< /Type /Catalog /Pages 2 0 R >>endobj
2 0 obj<< /Type /Pages /Kids [3 0 R] /Count 1 >>endobj
3 0 obj<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources<< /Font<< /F1 5 0 R >> >> >>endobj
4 0 obj<< /Length {length} >>stream
{stream}
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
"""
    return pdf.encode("ascii")


def ensure_pdf(path: Path, label: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(minimal_pdf(label))


def ghi_files():
    files = []
    for i in range(1, 5):
        files.append(("g", f"Assignment{i}_Questions.pdf", "Assignment Question", f"Assignment {i} Q"))
        files.append(("h", f"Assignment{i}_Solution.pdf", "Assignment Solution", f"Assignment {i} Sol"))
    for i in range(1, 9):
        files.append(("g", f"Quiz{i}_Questions.pdf", "Quiz Question", f"Quiz {i} Q"))
        files.append(("h", f"Quiz{i}_Solution.pdf", "Quiz Solution", f"Quiz {i} Sol"))
    files += [
        ("g", "MidExam1_Questions.pdf", "Mid Exam Question", "Mid Q"),
        ("g", "FinalExam1_Questions.pdf", "Final Exam Question", "Final Q"),
        ("h", "MidExam1_Solution.pdf", "Mid Exam Solution", "Mid Sol"),
        ("h", "FinalExam1_Solution.pdf", "Final Exam Solution", "Final Sol"),
    ]
    samples = [
        ("Assignment_Best.pdf", "Assignment Best Sample"),
        ("Assignment_Average.pdf", "Assignment Average Sample"),
        ("Assignment_Worst.pdf", "Assignment Worst Sample"),
        ("Quiz_Best.pdf", "Quiz Best Sample"),
        ("Quiz_Average.pdf", "Quiz Average Sample"),
        ("Quiz_Worst.pdf", "Quiz Worst Sample"),
        ("Mid_Best.pdf", "Mid Exam Best Sample"),
        ("Mid_Average.pdf", "Mid Exam Average Sample"),
        ("Mid_Worst.pdf", "Mid Exam Worst Sample"),
        ("Final_Best.pdf", "Final Exam Best Sample"),
        ("Final_Average.pdf", "Final Exam Average Sample"),
        ("Final_Worst.pdf", "Final Exam Worst Sample"),
    ]
    for name, cat in samples:
        files.append(("i", name, cat, cat))
    return files


def load_dir_names() -> dict[str, str]:
    out = {}
    for line in sqlcmd(
        "SET NOCOUNT ON; SELECT TemplateCode, FolderItemName FROM CourseFolderTemplateItems;"
    ).splitlines():
        line = line.strip()
        if "|" not in line:
            continue
        code, item = [p.strip() for p in line.split("|", 1)]
        out[code] = f"{code}-{item}"
    return out


def ensure_dirs_sql(folder_id: int, root_rel: str) -> str:
    root = root_rel.replace("'", "''")
    return f"""
SET NOCOUNT ON;
DECLARE @FolderId INT = {folder_id};
DECLARE @Root NVARCHAR(400) = N'{root}';
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
"""


def main() -> None:
    print("Logging in as Adeel...")
    login = api(
        "POST",
        "/Auth/login",
        body={"email": "adeel@biit.edu.pk", "password": "Teacher@123"},
    )
    token = login["token"]

    for oc_id in OCS:
        print(f"Ensuring master folder for OC {oc_id}...")
        try:
            api(
                "POST",
                f"/TeacherSubmission/offered-course/{oc_id}/create-master-folder?teacherUserId={TEACHER_ID}",
                token=token,
            )
        except Exception as ex:
            print(f"  master-folder call: {ex}")

    dir_names = load_dir_names()
    ghi = ghi_files()

    for oc_id in OCS:
        meta = sqlcmd(
            f"""
SET NOCOUNT ON;
SELECT cf.CourseFolderID, cf.RootFolderPath, cf.RootFolderName, c.CourseCode, cf.FolderStatus
FROM CourseFolders cf
JOIN OfferedCourses oc ON oc.OfferedCourseID = cf.OfferedCourseID
JOIN Courses c ON c.CourseID = oc.CourseID
WHERE oc.OfferedCourseID = {oc_id};
"""
        ).strip().splitlines()
        meta = [m for m in meta if m.strip()]
        if not meta:
            raise RuntimeError(f"No CourseFolder for OC {oc_id}")
        parts = [p.strip() for p in meta[0].split("|")]
        folder_id = int(parts[0])
        root_rel = parts[1]
        course_code = parts[3]
        status = parts[4]

        if status in ("Submitted", "Approved"):
            print(f"OC {oc_id} ({course_code}) already {status} — skip fill.")
            continue

        print(f"Filling folder {folder_id} for {course_code} ({root_rel})...")
        sqlcmd(ensure_dirs_sql(folder_id, root_rel))

        abs_root = API_ROOT / root_rel.replace("/", os.sep)
        abs_root.mkdir(parents=True, exist_ok=True)

        all_files = []
        for code, (cat, fname) in SIMPLE.items():
            dn = dir_names.get(code, f"{code}-{cat}")
            all_files.append((code, dn, fname, cat, f"{course_code} {cat}"))
        for code, name, cat, label in ghi:
            dn = dir_names[code]
            all_files.append((code, dn, name, cat, f"{course_code} {label}"))

        inserts = ["SET NOCOUNT ON;"]
        for code, dn, name, cat, label in all_files:
            rel_file = f"{root_rel}/{dn}/{name}"
            abs_file = API_ROOT / rel_file.replace("/", os.sep)
            ensure_pdf(abs_file, label)
            safe_name = name.replace("'", "''")
            safe_path = rel_file.replace("'", "''")
            safe_cat = cat.replace("'", "''")
            inserts.append(
                "INSERT INTO CourseFolderFiles (DirectoryID, UploadedByUserID, FileName, FilePath, FileCategory, Remarks) "
                f"SELECT d.DirectoryID, {TEACHER_ID}, N'{safe_name}', N'{safe_path}', N'{safe_cat}', N'CS3 complete folder seed' "
                "FROM CourseFolderDirectories d "
                "JOIN CourseFolderTemplateItems t ON t.TemplateItemID = d.TemplateItemID "
                f"WHERE d.CourseFolderID = {folder_id} AND t.TemplateCode = N'{code}';"
            )

        tmp = Path(os.environ.get("TEMP", ".")) / f"cs3_folder_{oc_id}.sql"
        tmp.write_text("\n".join(inserts), encoding="utf-8")
        sqlcmd_file(tmp)
        print(f"  Inserted {len(all_files)} files for {course_code}")

    print("\nSubmitting folders to HOD...")
    for oc_id in OCS:
        try:
            result = api(
                "POST",
                f"/TeacherSubmission/offered-course/{oc_id}/submit?teacherUserId={TEACHER_ID}",
                token=token,
            )
            print(
                f"OC {oc_id} : {result.get('message')} -> {result.get('submittedFolderPath')}"
            )
        except Exception as ex:
            print(f"OC {oc_id} submit failed: {ex}")

    print("\nFinal status:")
    print(
        sqlcmd(
            """
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
"""
        )
    )


if __name__ == "__main__":
    main()
