USE FolderManagementDB;
GO

IF COL_LENGTH('dbo.AcademicSessions', 'StartDate') IS NULL
BEGIN
    ALTER TABLE dbo.AcademicSessions
    ADD StartDate DATE NULL;
END
GO
