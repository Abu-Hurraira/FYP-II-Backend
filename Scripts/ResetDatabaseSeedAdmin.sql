USE FolderManagementDB;
GO

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

/* =========================================================
   Fresh database reset
   - Clears all operational data
   - Keeps reference/lookup rows:
       Roles
       AssessmentTypes
       CourseFolderTemplateItems
       ContentSubmissionTypes
       TeacherPermissionTypes
   - Seeds one Admin user for first login
   ========================================================= */

PRINT 'Disabling foreign key constraints...';
EXEC sp_MSforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL';
GO

PRINT 'Clearing operational tables...';

-- Deep leaf / dependent tables first (order still helps after NOCHECK)
DELETE FROM dbo.FolderSubmissionIssues;
DELETE FROM dbo.FolderSubmissions;
DELETE FROM dbo.CourseFolderFiles;
DELETE FROM dbo.CourseFolderDirectories;
DELETE FROM dbo.CourseFolders;

DELETE FROM dbo.StudentQuestionMarks;
DELETE FROM dbo.StudentAssessmentMarks;
IF OBJECT_ID(N'dbo.StudentCourseResultSelections', N'U') IS NOT NULL
    DELETE FROM dbo.StudentCourseResultSelections;
IF OBJECT_ID(N'dbo.StudentCourseResults', N'U') IS NOT NULL
    DELETE FROM dbo.StudentCourseResults;
IF OBJECT_ID(N'dbo.OfferedCourseResultSchemes', N'U') IS NOT NULL
    DELETE FROM dbo.OfferedCourseResultSchemes;
DELETE FROM dbo.AssessmentQuestionCLOMappings;
DELETE FROM dbo.AssessmentQuestions;
DELETE FROM dbo.AssessmentFiles;
DELETE FROM dbo.Assessments;
DELETE FROM dbo.AssessmentWeightages;

DELETE FROM dbo.AttendanceRecords;
DELETE FROM dbo.LessonPlans;
DELETE FROM dbo.GraderTasks;
DELETE FROM dbo.GraderAllocations;

DELETE FROM dbo.CLOAttainmentResults;
IF OBJECT_ID('dbo.PLOAttainmentResults', 'U') IS NOT NULL DELETE FROM dbo.PLOAttainmentResults;
DELETE FROM dbo.CourseAssessmentTypeCLOMappings;
DELETE FROM dbo.CourseTopicCLOMappings;
DELETE FROM dbo.PLOCLOMappings;
DELETE FROM dbo.CLOs;
DELETE FROM dbo.CourseTopics;

DELETE FROM dbo.OfferedCourseTopicProgress;
IF OBJECT_ID(N'dbo.StudentOfferedCourses', N'U') IS NOT NULL
    DELETE FROM dbo.StudentOfferedCourses;
DELETE FROM dbo.OfferedCourses;

DELETE FROM dbo.ProgramCourseContentSubmissions;
DELETE FROM dbo.ProgramCoursePLOMappings;
DELETE FROM dbo.PEOPLEMappings;
DELETE FROM dbo.TeacherPermissions;

DELETE FROM dbo.Students;
DELETE FROM dbo.Sections;
DELETE FROM dbo.AcademicSessions;
DELETE FROM dbo.AcademicProgramCourses;
DELETE FROM dbo.PEOs;
DELETE FROM dbo.PLOs;
DELETE FROM dbo.AcademicPrograms;
DELETE FROM dbo.Courses;

DELETE FROM dbo.Notifications;
DELETE FROM dbo.Users;

-- Unrelated / leftover sample tables if present
IF OBJECT_ID('dbo.BookImage', 'U') IS NOT NULL DELETE FROM dbo.BookImage;
IF OBJECT_ID('dbo.Book', 'U') IS NOT NULL DELETE FROM dbo.Book;

-- Optional cache table
IF OBJECT_ID('dbo.HodOfferingProgressCache', 'U') IS NOT NULL DELETE FROM dbo.HodOfferingProgressCache;
GO

PRINT 'Reseeding identities on cleared tables...';
DBCC CHECKIDENT ('dbo.Users', RESEED, 0);
DBCC CHECKIDENT ('dbo.Courses', RESEED, 0);
DBCC CHECKIDENT ('dbo.AcademicPrograms', RESEED, 0);
DBCC CHECKIDENT ('dbo.AcademicSessions', RESEED, 0);
DBCC CHECKIDENT ('dbo.Sections', RESEED, 0);
DBCC CHECKIDENT ('dbo.Students', RESEED, 0);
DBCC CHECKIDENT ('dbo.OfferedCourses', RESEED, 0);
DBCC CHECKIDENT ('dbo.CLOs', RESEED, 0);
DBCC CHECKIDENT ('dbo.CourseTopics', RESEED, 0);
DBCC CHECKIDENT ('dbo.CourseFolders', RESEED, 0);
DBCC CHECKIDENT ('dbo.Assessments', RESEED, 0);
DBCC CHECKIDENT ('dbo.Notifications', RESEED, 0);
GO

PRINT 'Re-enabling foreign key constraints...';
EXEC sp_MSforeachtable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL';
GO

PRINT 'Ensuring Admin role exists...';
IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE RoleName = N'Admin')
BEGIN
    SET IDENTITY_INSERT dbo.Roles ON;
    INSERT INTO dbo.Roles (RoleID, RoleName) VALUES (1, N'Admin');
    SET IDENTITY_INSERT dbo.Roles OFF;
END
GO

PRINT 'Seeding Admin user...';
DECLARE @AdminRoleId INT = (SELECT TOP 1 RoleID FROM dbo.Roles WHERE RoleName = N'Admin');

IF @AdminRoleId IS NULL
    THROW 50001, 'Admin role is missing.', 1;

-- Password: Admin@BIIT#2026  (ASP.NET Identity PasswordHasher v3)
INSERT INTO dbo.Users (FullName, Email, PasswordHash, RoleID, Rank, AridNo)
VALUES (
    N'System Administrator',
    N'admin@biit.edu.pk',
    N'AQAAAAIAAYagAAAAEJhJzKqMq+z6JbpmpItGDAijul3TpfIXMDJ6e2DKTV2heEcI83qtKcu/PV2TnKotGw==',
    @AdminRoleId,
    N'Administrator',
    NULL
);
GO

PRINT 'Verification...';
SELECT u.UserID, u.FullName, u.Email, r.RoleName, u.Rank
FROM dbo.Users u
INNER JOIN dbo.Roles r ON r.RoleID = u.RoleID;

SELECT 'Users' AS [Table], COUNT(*) AS Rows FROM dbo.Users
UNION ALL SELECT 'Roles', COUNT(*) FROM dbo.Roles
UNION ALL SELECT 'Courses', COUNT(*) FROM dbo.Courses
UNION ALL SELECT 'AcademicPrograms', COUNT(*) FROM dbo.AcademicPrograms
UNION ALL SELECT 'OfferedCourses', COUNT(*) FROM dbo.OfferedCourses
UNION ALL SELECT 'AssessmentTypes', COUNT(*) FROM dbo.AssessmentTypes
UNION ALL SELECT 'CourseFolderTemplateItems', COUNT(*) FROM dbo.CourseFolderTemplateItems
UNION ALL SELECT 'TeacherPermissionTypes', COUNT(*) FROM dbo.TeacherPermissionTypes
UNION ALL SELECT 'ContentSubmissionTypes', COUNT(*) FROM dbo.ContentSubmissionTypes;
GO

PRINT 'Fresh database ready. Login: admin@biit.edu.pk / Admin@BIIT#2026';
GO
