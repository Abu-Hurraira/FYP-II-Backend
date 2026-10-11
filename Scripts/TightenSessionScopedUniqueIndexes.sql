USE FolderManagementDB;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* =========================================================
   Session-scoped unique indexes

   Scope rules (do not change program-level tables):
   - Program-level KEEP AS-IS:
       ProgramCoursePLOMappings (ProgramCourseID, PLOID)
       PEOPLEMappings (program outcome structure)
   - Session-scoped (course outcomes / content per academic session):
       CLOs
       PLOCLOMappings
       CourseTopics
   - Already session-scoped:
       CourseAssessmentTypeCLOMappings (UQ_CourseAssessmentTypeCLOMappings_Session)
   - OfferedCourse / Section scoped stay on OfferedCourseID (Assessments, Attendance, etc.)

   Run with: sqlcmd ... -I  (enables QUOTED_IDENTIFIER)
   ========================================================= */

PRINT 'Checking for duplicate CLOs that would block (ProgramCourseID, SessionID, CLOCode)...';
SELECT ProgramCourseID, SessionID, CLOCode, COUNT(*) AS DupCount
FROM dbo.CLOs
WHERE SessionID IS NOT NULL
GROUP BY ProgramCourseID, SessionID, CLOCode
HAVING COUNT(*) > 1;

PRINT 'Checking for duplicate PLOCLOMappings that would block (ProgramCourseID, SessionID, PLOID, CLOID)...';
SELECT ProgramCourseID, SessionID, PLOID, CLOID, COUNT(*) AS DupCount
FROM dbo.PLOCLOMappings
WHERE SessionID IS NOT NULL
GROUP BY ProgramCourseID, SessionID, PLOID, CLOID
HAVING COUNT(*) > 1;

PRINT 'Checking for duplicate CourseTopics that would block (ProgramCourseID, SessionID, ParentTopicID, TopicTitle)...';
SELECT ProgramCourseID, SessionID, ParentTopicID, TopicTitle, COUNT(*) AS DupCount
FROM dbo.CourseTopics
WHERE SessionID IS NOT NULL
GROUP BY ProgramCourseID, SessionID, ParentTopicID, TopicTitle
HAVING COUNT(*) > 1;
GO

SET QUOTED_IDENTIFIER ON;
GO

/* ---- CLOs ---- */
IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'UQ__CLOs__6D75512123F6DAD9'
      AND object_id = OBJECT_ID('dbo.CLOs')
)
BEGIN
    ALTER TABLE dbo.CLOs DROP CONSTRAINT UQ__CLOs__6D75512123F6DAD9;
END
GO

SET QUOTED_IDENTIFIER ON;
DECLARE @sqlClos NVARCHAR(MAX) = N'';
SELECT @sqlClos = @sqlClos + N'ALTER TABLE dbo.CLOs DROP CONSTRAINT [' + kc.name + N'];'
FROM sys.key_constraints kc
INNER JOIN sys.index_columns ic
    ON ic.object_id = kc.parent_object_id AND ic.index_id = kc.unique_index_id
INNER JOIN sys.columns c
    ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE kc.parent_object_id = OBJECT_ID('dbo.CLOs')
  AND kc.type = 'UQ'
GROUP BY kc.name
HAVING SUM(CASE WHEN c.name IN ('ProgramCourseID', 'CLOCode') THEN 1 ELSE 0 END) = 2
   AND SUM(CASE WHEN c.name = 'SessionID' THEN 1 ELSE 0 END) = 0;

IF LEN(@sqlClos) > 0 EXEC sp_executesql @sqlClos;
GO

SET QUOTED_IDENTIFIER ON;
IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'UQ_CLOs_Session_Code' AND object_id = OBJECT_ID('dbo.CLOs')
)
    DROP INDEX UQ_CLOs_Session_Code ON dbo.CLOs;
GO

SET QUOTED_IDENTIFIER ON;
CREATE UNIQUE INDEX UQ_CLOs_Session_Code
ON dbo.CLOs (ProgramCourseID, SessionID, CLOCode)
WHERE SessionID IS NOT NULL;
GO

/* ---- PLOCLOMappings ---- */
SET QUOTED_IDENTIFIER ON;
IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'UQ__PLOCLOMa__15875FEC3457CF94'
      AND object_id = OBJECT_ID('dbo.PLOCLOMappings')
)
BEGIN
    ALTER TABLE dbo.PLOCLOMappings DROP CONSTRAINT UQ__PLOCLOMa__15875FEC3457CF94;
END
GO

SET QUOTED_IDENTIFIER ON;
DECLARE @sqlPloClo NVARCHAR(MAX) = N'';
SELECT @sqlPloClo = @sqlPloClo + N'ALTER TABLE dbo.PLOCLOMappings DROP CONSTRAINT [' + kc.name + N'];'
FROM sys.key_constraints kc
INNER JOIN sys.index_columns ic
    ON ic.object_id = kc.parent_object_id AND ic.index_id = kc.unique_index_id
INNER JOIN sys.columns c
    ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE kc.parent_object_id = OBJECT_ID('dbo.PLOCLOMappings')
  AND kc.type = 'UQ'
GROUP BY kc.name
HAVING SUM(CASE WHEN c.name IN ('ProgramCourseID', 'PLOID', 'CLOID') THEN 1 ELSE 0 END) = 3
   AND SUM(CASE WHEN c.name = 'SessionID' THEN 1 ELSE 0 END) = 0;

IF LEN(@sqlPloClo) > 0 EXEC sp_executesql @sqlPloClo;
GO

SET QUOTED_IDENTIFIER ON;
IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'UQ_PLOCLOMappings_Session' AND object_id = OBJECT_ID('dbo.PLOCLOMappings')
)
    DROP INDEX UQ_PLOCLOMappings_Session ON dbo.PLOCLOMappings;
GO

SET QUOTED_IDENTIFIER ON;
CREATE UNIQUE INDEX UQ_PLOCLOMappings_Session
ON dbo.PLOCLOMappings (ProgramCourseID, SessionID, PLOID, CLOID)
WHERE SessionID IS NOT NULL;
GO

/* ---- CourseTopics ---- */
SET QUOTED_IDENTIFIER ON;
IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'UQ__CourseTo__4A97F2CC616D30D3'
      AND object_id = OBJECT_ID('dbo.CourseTopics')
)
BEGIN
    ALTER TABLE dbo.CourseTopics DROP CONSTRAINT UQ__CourseTo__4A97F2CC616D30D3;
END
GO

SET QUOTED_IDENTIFIER ON;
DECLARE @sqlTopics NVARCHAR(MAX) = N'';
SELECT @sqlTopics = @sqlTopics + N'ALTER TABLE dbo.CourseTopics DROP CONSTRAINT [' + kc.name + N'];'
FROM sys.key_constraints kc
INNER JOIN sys.index_columns ic
    ON ic.object_id = kc.parent_object_id AND ic.index_id = kc.unique_index_id
INNER JOIN sys.columns c
    ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE kc.parent_object_id = OBJECT_ID('dbo.CourseTopics')
  AND kc.type = 'UQ'
GROUP BY kc.name
HAVING SUM(CASE WHEN c.name IN ('ProgramCourseID', 'ParentTopicID', 'TopicTitle') THEN 1 ELSE 0 END) = 3
   AND SUM(CASE WHEN c.name = 'SessionID' THEN 1 ELSE 0 END) = 0;

IF LEN(@sqlTopics) > 0 EXEC sp_executesql @sqlTopics;
GO

SET QUOTED_IDENTIFIER ON;
IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'UQ_CourseTopics_Session' AND object_id = OBJECT_ID('dbo.CourseTopics')
)
    DROP INDEX UQ_CourseTopics_Session ON dbo.CourseTopics;
GO

SET QUOTED_IDENTIFIER ON;
CREATE UNIQUE INDEX UQ_CourseTopics_Session
ON dbo.CourseTopics (ProgramCourseID, SessionID, ParentTopicID, TopicTitle)
WHERE SessionID IS NOT NULL;
GO

PRINT 'Session-scoped unique indexes applied.';
GO
