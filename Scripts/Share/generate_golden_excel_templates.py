import os
from openpyxl import Workbook
from openpyxl.styles import Font, Alignment


OUT_DIR = r"E:\Projects\Folder Management\Project\FolderManagementSystemAPI\Scripts\Share\GoldenTemplates"


def make_template(filename: str, headers: list[str], example_row: list[object]) -> None:
    os.makedirs(OUT_DIR, exist_ok=True)

    wb = Workbook()
    ws = wb.active
    ws.title = "Template"

    ws.append(headers)
    for cell in ws[1]:
        cell.font = Font(bold=True)
        cell.alignment = Alignment(horizontal="center")

    ws.append(example_row)
    ws.freeze_panes = "A2"

    path = os.path.join(OUT_DIR, filename)
    wb.save(path)
    print("Wrote", path)


def main() -> None:
    # Columns are chosen to match backend header normalization:
    # trim + lower + remove spaces, '_' and '-'.
    make_template(
        "01_Faculty_Import_Template.xlsx",
        ["Name", "Email", "Password", "Role ID", "Rank", "ARID"],
        ["Dr. Example", "teacher3@example.edu", "Pass@1234", "3", "Assistant Professor", "ARID-1001"],
    )
    make_template(
        "02_Courses_Import_Template.xlsx",
        ["Course Code", "Course Title", "Class Credit Hours", "Lab Credit Hours"],
        ["CSE101", "Programming Fundamentals", "3", "1"],
    )
    make_template(
        "03_PEO_Import_Template.xlsx",
        ["PEO Code", "PEO Description"],
        ["PEO-1", "Apply programming knowledge to solve real-world problems"],
    )
    make_template(
        "04_PLO_Import_Template.xlsx",
        ["PLO Code", "PLO Description"],
        ["PLO-1", "Demonstrate ability to design and analyze software solutions"],
    )
    make_template(
        "05_OfferedCourses_Import_Template.xlsx",
        ["Section", "Course Code", "Course Title", "Teacher Name"],
        ["BSCS-1A", "CSE101", "Programming Fundamentals", "Dr. Example"],
    )
    make_template(
        "06_Students_SectionRoster_Template.xlsx",
        ["StuName", "Reg_no", "Email"],
        ["Student One", "S-2001", "student.one@example.edu"],
    )
    make_template(
        "07_Students_SessionEnrollment_Template.xlsx",
        [
            "Reg_no",
            "StuName",
            "Section",
            "Course Code",
            "Course Title",
            "Email",
        ],
        [
            "S-2001",
            "Student One",
            "BSCS-1A",
            "CSE101",
            "Programming Fundamentals",
            "student.one@example.edu",
        ],
    )
    make_template(
        "08_CLOs_Import_Template.xlsx",
        ["CLO Code", "CLO Description"],
        ["CLO1", "Demonstrate understanding of programming fundamentals"],
    )
    make_template(
        "09_WeeklyContent_Import_Template.xlsx",
        ["WeekNo", "TopicPath", "DisplayOrder", "CLO Codes", "Description"],
        [1, "Week 1 > Topic 1", 1, "CLO1", "Introductory topic"],
    )

    readme_path = os.path.join(OUT_DIR, "README_GoldenTemplates.md")
    readme = """# Golden Excel Templates

These templates are meant to match the API importers’ expected headers.

## Header consistency rule (matches the backend)
Backend header normalization generally does:
- trim whitespace
- lowercase
- remove spaces, `_`, and `-`

So headers like `Teacher Email`, `TeacherEmail`, `Teacher_Email` map to the same backend key.

## Section name format
Always `{PROGRAM}-{SEMESTER}{LETTER}` e.g. `BSCS-1A`.
`1` is the semester, `A` is the section letter.
Variants (`BSCS 1A`, `BSCS-1-A`, `bscs1a`, `BCS-1A` for program BSCS) are normalized to `BSCS-1A`.

## Course columns (catalog + offerings + enrollment)
Use `Course Code` (aliases: `Course_N`, `Code`) and/or `Course Title` (aliases: `Course Name`, `Course_Desc`).
`CSC-303` and `CSC303` match the same catalog course.

## CLO codes
Always `CLO1` (not `CLO-1`). `CLO-1` / `clo 1` are stored as `CLO1`.

## Weekly TopicPath
Use `Parent > Child` (slash `/` is also accepted).
CLO Codes must match stored codes, e.g. `CLO1, CLO2`.

## Import order
Faculty → Catalog → PEOs/PLOs → Session offerings (creates sections) → Enrollment (links students only).
"""
    with open(readme_path, "w", encoding="utf-8", newline="\n") as f:
        f.write(readme)

    print("Wrote", readme_path)


if __name__ == "__main__":
    main()

