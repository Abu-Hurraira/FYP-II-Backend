IF COL_LENGTH(N'dbo.OfferedCourseHomeworkMarkSchemes', N'ExtraItemsJson') IS NULL
BEGIN
    ALTER TABLE dbo.OfferedCourseHomeworkMarkSchemes
        ADD ExtraItemsJson NVARCHAR(MAX) NULL;
END
GO

IF COL_LENGTH(N'dbo.OfferedCourseLabMarkSchemes', N'ExtraItemsJson') IS NULL
BEGIN
    ALTER TABLE dbo.OfferedCourseLabMarkSchemes
        ADD ExtraItemsJson NVARCHAR(MAX) NULL;
END
GO

IF COL_LENGTH(N'dbo.StudentComponentMarks', N'ExtraMarksJson') IS NULL
BEGIN
    ALTER TABLE dbo.StudentComponentMarks
        ADD ExtraMarksJson NVARCHAR(MAX) NULL;
END
GO
