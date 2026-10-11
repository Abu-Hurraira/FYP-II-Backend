-- Ensure StudentOfferedCourses FKs cascade so deletes of students/offerings succeed.
IF OBJECT_ID(N'dbo.StudentOfferedCourses', N'U') IS NOT NULL
BEGIN
    IF EXISTS (
        SELECT 1 FROM sys.foreign_keys
        WHERE name = N'FK_StudentOfferedCourses_Student'
          AND parent_object_id = OBJECT_ID(N'dbo.StudentOfferedCourses'))
    BEGIN
        ALTER TABLE dbo.StudentOfferedCourses DROP CONSTRAINT FK_StudentOfferedCourses_Student;
    END

    IF EXISTS (
        SELECT 1 FROM sys.foreign_keys
        WHERE name = N'FK_StudentOfferedCourses_OfferedCourse'
          AND parent_object_id = OBJECT_ID(N'dbo.StudentOfferedCourses'))
    BEGIN
        ALTER TABLE dbo.StudentOfferedCourses DROP CONSTRAINT FK_StudentOfferedCourses_OfferedCourse;
    END

    ALTER TABLE dbo.StudentOfferedCourses
    ADD CONSTRAINT FK_StudentOfferedCourses_Student
        FOREIGN KEY (StudentID) REFERENCES dbo.Students(StudentID) ON DELETE CASCADE;

    ALTER TABLE dbo.StudentOfferedCourses
    ADD CONSTRAINT FK_StudentOfferedCourses_OfferedCourse
        FOREIGN KEY (OfferedCourseID) REFERENCES dbo.OfferedCourses(OfferedCourseID) ON DELETE CASCADE;
END
GO
