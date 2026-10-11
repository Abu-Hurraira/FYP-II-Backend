/*
  Fast wipe of ALL BSCS3 programs, then rely on SeedBSCS3TestProgram.sql to create one clean copy.
*/
USE FolderManagementDB;
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

DECLARE @Prog TABLE (ProgramID INT);
INSERT INTO @Prog SELECT ProgramID FROM AcademicPrograms WHERE ProgramCode = N'BSCS3';

IF NOT EXISTS (SELECT 1 FROM @Prog)
BEGIN
    PRINT 'No BSCS3 to wipe.';
    RETURN;
END

DECLARE @Sess TABLE (SessionID INT);
INSERT INTO @Sess SELECT SessionID FROM AcademicSessions WHERE ProgramID IN (SELECT ProgramID FROM @Prog);

DECLARE @Sec TABLE (SectionID INT);
INSERT INTO @Sec SELECT SectionID FROM Sections WHERE SessionID IN (SELECT SessionID FROM @Sess);

DECLARE @OC TABLE (OfferedCourseID INT PRIMARY KEY);
INSERT INTO @OC SELECT OfferedCourseID FROM OfferedCourses WHERE SectionID IN (SELECT SectionID FROM @Sec);

DECLARE @PC TABLE (ProgramCourseID INT PRIMARY KEY);
INSERT INTO @PC SELECT ProgramCourseID FROM AcademicProgramCourses WHERE ProgramID IN (SELECT ProgramID FROM @Prog);

DECLARE @Fold TABLE (CourseFolderID INT PRIMARY KEY);
INSERT INTO @Fold SELECT CourseFolderID FROM CourseFolders WHERE OfferedCourseID IN (SELECT OfferedCourseID FROM @OC);

DECLARE @A TABLE (AssessmentID INT PRIMARY KEY);
INSERT INTO @A SELECT AssessmentID FROM Assessments WHERE OfferedCourseID IN (SELECT OfferedCourseID FROM @OC);

DECLARE @Q TABLE (QuestionID INT PRIMARY KEY);
INSERT INTO @Q SELECT QuestionID FROM AssessmentQuestions WHERE AssessmentID IN (SELECT AssessmentID FROM @A);

DECLARE @OcCount INT = (SELECT COUNT(*) FROM @OC);
DECLARE @ACount INT = (SELECT COUNT(*) FROM @A);
PRINT CONCAT('Wiping OCs=', @OcCount, ' assessments=', @ACount);

DELETE sqm FROM StudentQuestionMarks sqm WHERE sqm.QuestionID IN (SELECT QuestionID FROM @Q);
DELETE m FROM AssessmentQuestionCLOMappings m WHERE m.QuestionID IN (SELECT QuestionID FROM @Q);
DELETE aq FROM AssessmentQuestions aq WHERE aq.QuestionID IN (SELECT QuestionID FROM @Q);
DELETE sam FROM StudentAssessmentMarks sam WHERE sam.AssessmentID IN (SELECT AssessmentID FROM @A);
DELETE sel FROM StudentCourseResultSelections sel WHERE sel.OfferedCourseID IN (SELECT OfferedCourseID FROM @OC);
DELETE a FROM Assessments a WHERE a.AssessmentID IN (SELECT AssessmentID FROM @A);
DELETE ar FROM AttendanceRecords ar WHERE ar.OfferedCourseID IN (SELECT OfferedCourseID FROM @OC);

DELETE r FROM CLOAttainmentResults r WHERE r.OfferedCourseID IN (SELECT OfferedCourseID FROM @OC);
DELETE r FROM PLOAttainmentResults r WHERE r.OfferedCourseID IN (SELECT OfferedCourseID FROM @OC);

IF OBJECT_ID(N'dbo.OfferedCourseHomeworkMarkSchemes', N'U') IS NOT NULL
    DELETE h FROM OfferedCourseHomeworkMarkSchemes h WHERE h.OfferedCourseID IN (SELECT OfferedCourseID FROM @OC);
IF OBJECT_ID(N'dbo.OfferedCourseLabMarkSchemes', N'U') IS NOT NULL
    DELETE h FROM OfferedCourseLabMarkSchemes h WHERE h.OfferedCourseID IN (SELECT OfferedCourseID FROM @OC);
IF OBJECT_ID(N'dbo.StudentCourseResults', N'U') IS NOT NULL
    DELETE h FROM StudentCourseResults h WHERE h.OfferedCourseID IN (SELECT OfferedCourseID FROM @OC);
IF OBJECT_ID(N'dbo.OfferedCourseResultSchemes', N'U') IS NOT NULL
    DELETE h FROM OfferedCourseResultSchemes h WHERE h.OfferedCourseID IN (SELECT OfferedCourseID FROM @OC);

DELETE f FROM CourseFolderFiles f
WHERE f.DirectoryID IN (SELECT DirectoryID FROM CourseFolderDirectories WHERE CourseFolderID IN (SELECT CourseFolderID FROM @Fold));
DELETE d FROM CourseFolderDirectories d WHERE d.CourseFolderID IN (SELECT CourseFolderID FROM @Fold);
DELETE fs FROM FolderSubmissions fs WHERE fs.CourseFolderID IN (SELECT CourseFolderID FROM @Fold);
DELETE cf FROM CourseFolders cf WHERE cf.CourseFolderID IN (SELECT CourseFolderID FROM @Fold);

DELETE soc FROM StudentOfferedCourses soc WHERE soc.OfferedCourseID IN (SELECT OfferedCourseID FROM @OC);
DELETE oc FROM OfferedCourses oc WHERE oc.OfferedCourseID IN (SELECT OfferedCourseID FROM @OC);
DELETE st FROM Students st WHERE st.SectionID IN (SELECT SectionID FROM @Sec);
DELETE s FROM Sections s WHERE s.SectionID IN (SELECT SectionID FROM @Sec);

DELETE m FROM PLOCLOMappings m WHERE m.ProgramCourseID IN (SELECT ProgramCourseID FROM @PC);
DELETE m FROM CourseAssessmentTypeCLOMappings m WHERE m.ProgramCourseID IN (SELECT ProgramCourseID FROM @PC);
DELETE t FROM CourseTopics t WHERE t.ProgramCourseID IN (SELECT ProgramCourseID FROM @PC);
DELETE c FROM CLOs c WHERE c.ProgramCourseID IN (SELECT ProgramCourseID FROM @PC);
DELETE s FROM ProgramCourseContentSubmissions s WHERE s.ProgramCourseID IN (SELECT ProgramCourseID FROM @PC);
DELETE tp FROM TeacherPermissions tp WHERE tp.ProgramCourseID IN (SELECT ProgramCourseID FROM @PC);
DELETE m FROM ProgramCoursePLOMappings m WHERE m.ProgramCourseID IN (SELECT ProgramCourseID FROM @PC);
DELETE pc FROM AcademicProgramCourses pc WHERE pc.ProgramCourseID IN (SELECT ProgramCourseID FROM @PC);

DELETE m FROM PEOPLEMappings m WHERE m.PEOID IN (SELECT PEOID FROM PEOs WHERE ProgramID IN (SELECT ProgramID FROM @Prog));
DELETE p FROM PLOs p WHERE p.ProgramID IN (SELECT ProgramID FROM @Prog);
DELETE p FROM PEOs p WHERE p.ProgramID IN (SELECT ProgramID FROM @Prog);
DELETE s FROM AcademicSessions s WHERE s.ProgramID IN (SELECT ProgramID FROM @Prog);
DELETE p FROM AcademicPrograms p WHERE p.ProgramID IN (SELECT ProgramID FROM @Prog);

PRINT 'All BSCS3 programs wiped.';
GO
