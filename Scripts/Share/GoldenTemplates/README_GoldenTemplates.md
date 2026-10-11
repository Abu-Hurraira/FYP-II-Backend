# Golden Excel Templates

These templates are meant to match the API importers’ expected headers.

## Header consistency rule (matches the backend)
Backend header normalization generally does:
- trim whitespace
- lowercase
- remove spaces, `_`, and `-`

So headers like `Teacher Name`, `TeacherName`, `Teacher_Name` map to the same backend key.

## Section name format
Always `{PROGRAM}-{SEMESTER}{LETTER}` e.g. `BSCS-1A`.
`1` is the semester, `A` is the section letter.
Variants (`BSCS 1A`, `BSCS-1-A`, `bscs1a`, `BCS-1A` for program BSCS) are normalized to `BSCS-1A`.

## Course columns (catalog + offerings + enrollment)
Use `Course Code` (aliases: `Course_N`, `Code`) and/or `Course Title` (aliases: `Course Name`, `Course_Desc`).
`CSC-303` and `CSC303` match the same catalog course.

## Offered courses teacher
Use `Teacher Name` only. The importer looks up `Users.FullName` and stores `OfferedCourses.TeacherUserID` (FK). Email is not used.

## CLO codes
Always `CLO1` (not `CLO-1`). `CLO-1` / `clo 1` are stored as `CLO1`.

## Weekly TopicPath
Use `Parent > Child` (slash `/` is also accepted).
CLO Codes must match stored codes, e.g. `CLO1, CLO2`.

## Import order
Faculty → Catalog → PEOs/PLOs → Session offerings (creates sections) → Enrollment (links students only).
