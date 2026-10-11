-- Per-student subject enrollment: Student ↔ OfferedCourse.
-- Students.SectionID remains the home/admin section; mark sheets and attainment
-- use only students linked here for that offering.

IF OBJECT_ID(N'dbo.StudentOfferedCourses', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.StudentOfferedCourses
    (
        StudentOfferedCourseID INT IDENTITY(1,1) NOT NULL,
        StudentID INT NOT NULL,
        OfferedCourseID INT NOT NULL,
        CONSTRAINT PK_StudentOfferedCourses PRIMARY KEY (StudentOfferedCourseID),
        CONSTRAINT UQ_StudentOfferedCourses_Student_Offered UNIQUE (StudentID, OfferedCourseID),
        CONSTRAINT FK_StudentOfferedCourses_Student
            FOREIGN KEY (StudentID) REFERENCES dbo.Students(StudentID),
        CONSTRAINT FK_StudentOfferedCourses_OfferedCourse
            FOREIGN KEY (OfferedCourseID) REFERENCES dbo.OfferedCourses(OfferedCourseID)
    );
END
GO

-- Backfill: existing section students → every offering already in their section.
-- New offerings after this need explicit enrollment (session Excel with Course columns).
INSERT INTO dbo.StudentOfferedCourses (StudentID, OfferedCourseID)
SELECT s.StudentID, oc.OfferedCourseID
FROM dbo.Students s
INNER JOIN dbo.OfferedCourses oc ON oc.SectionID = s.SectionID
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.StudentOfferedCourses soc
    WHERE soc.StudentID = s.StudentID
      AND soc.OfferedCourseID = oc.OfferedCourseID
);
GO
