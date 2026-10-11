/*
  Unique keys so Excel imports cannot create duplicate sections or offerings.
*/
USE [FolderManagementDB];
GO
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UQ_Sections_Session_SectionName'
      AND object_id = OBJECT_ID(N'dbo.Sections')
)
BEGIN
    CREATE UNIQUE INDEX [UQ_Sections_Session_SectionName]
        ON [dbo].[Sections] ([SessionID], [SectionName]);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UQ_OfferedCourses_Section_Course'
      AND object_id = OBJECT_ID(N'dbo.OfferedCourses')
)
BEGIN
    CREATE UNIQUE INDEX [UQ_OfferedCourses_Section_Course]
        ON [dbo].[OfferedCourses] ([SectionID], [CourseID]);
END
GO
