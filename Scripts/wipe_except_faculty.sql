/*
  Wipe transactional data for a clean test import.
  KEEPS: Roles, Admin/Teacher/HOD users, permission types, content types,
         assessment types, folder template items.
*/
USE [FolderManagementDB];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
BEGIN TRANSACTION;

DECLARE @sql NVARCHAR(MAX) = N'';
SELECT @sql += N'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(fk.parent_object_id))
    + N'.' + QUOTENAME(OBJECT_NAME(fk.parent_object_id))
    + N' NOCHECK CONSTRAINT ' + QUOTENAME(fk.name) + N';'
FROM sys.foreign_keys fk;
IF LEN(@sql) > 0 EXEC sp_executesql @sql;

DECLARE @keep TABLE (TableName SYSNAME PRIMARY KEY);
INSERT INTO @keep (TableName) VALUES
    (N'Roles'),
    (N'Users'),
    (N'TeacherPermissionTypes'),
    (N'ContentSubmissionTypes'),
    (N'AssessmentTypes'),
    (N'CourseFolderTemplateItems'),
    (N'sysdiagrams');

SET @sql = N'';
SELECT @sql += N'DELETE FROM ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) + N';'
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE t.is_ms_shipped = 0
  AND t.name NOT IN (SELECT TableName FROM @keep);
IF LEN(@sql) > 0 EXEC sp_executesql @sql;

DELETE u
FROM dbo.Users u
INNER JOIN dbo.Roles r ON r.RoleID = u.RoleID
WHERE r.RoleName NOT IN (N'Admin', N'Teacher', N'HOD');

SET @sql = N'';
SELECT @sql += N'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(fk.parent_object_id))
    + N'.' + QUOTENAME(OBJECT_NAME(fk.parent_object_id))
    + N' WITH CHECK CHECK CONSTRAINT ' + QUOTENAME(fk.name) + N';'
FROM sys.foreign_keys fk;
IF LEN(@sql) > 0 EXEC sp_executesql @sql;

COMMIT TRANSACTION;
GO

SELECT r.RoleName, COUNT(*) AS UserCount
FROM dbo.Users u
JOIN dbo.Roles r ON r.RoleID = u.RoleID
GROUP BY r.RoleName
ORDER BY r.RoleName;

SELECT
    (SELECT COUNT(*) FROM dbo.AcademicPrograms) AS Programs,
    (SELECT COUNT(*) FROM dbo.AcademicSessions) AS Sessions,
    (SELECT COUNT(*) FROM dbo.Sections) AS Sections,
    (SELECT COUNT(*) FROM dbo.Courses) AS Courses,
    (SELECT COUNT(*) FROM dbo.OfferedCourses) AS Offerings,
    (SELECT COUNT(*) FROM dbo.Students) AS Students;
GO
