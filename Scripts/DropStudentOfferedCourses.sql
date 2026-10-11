/*
  Drops StudentOfferedCourses if present.
  Prefer this only when intentionally reverting to section-only rosters.
*/
IF OBJECT_ID(N'dbo.StudentOfferedCourses', N'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.StudentOfferedCourses;
END
GO
