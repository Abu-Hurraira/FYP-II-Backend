USE FolderManagementDB;
GO

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

/* =========================================================
   SeedFullDemoData.sql
   Adds PEOs/PLOs, students, permissions, approved CLOs,
   mappings, topics, assessments, and marks for a full
   end-to-end test on subject CAI-261 (Programming for AI).

   Safe to re-run: clears prior DEMO_* / this-script rows for
   the target program-course/session before inserting.

   Demo logins (password hashes below):
     Admin:         admin@biit.edu.pk / Admin@BIIT#2026
     Admin (2nd):   sectionadmin@biit.edu.pk / SectionAdmin@123  (same Admin role)
     HOD:           munir@biit.edu.pk / Hod@123
     Teacher (A):   aftab@biit.edu.pk / Teacher@123
     Teacher (B):   shahid_jamil@biit.edu.pk / Teacher@123
   ========================================================= */

DECLARE @PwdHod NVARCHAR(200) = N'AQAAAAIAAYagAAAAEFTTBo5BE66rFJZJBMZ+CsUKh35uMNUGFaF98M1IlCANe0QM8NZeDa3PleikQxXQJA=='; -- Hod@123
DECLARE @PwdTeacher NVARCHAR(200) = N'AQAAAAIAAYagAAAAEDC7jL0eN8aLHwdmyWstxLB/1PzmsOVOUnhYpEXoj7DlBYKzxBnMN+v+HzFvqzTR1A=='; -- Teacher@123
DECLARE @PwdSa NVARCHAR(200) = N'AQAAAAIAAYagAAAAEOUBvtDuYVx2GWyTW62YvULYN/6HgF6Q7aOOu/4LCDGdcBuBGfbe99nQf7+JLMLjLQ=='; -- SectionAdmin@123

DECLARE @ProgramId INT = (SELECT TOP 1 ProgramID FROM AcademicPrograms WHERE ProgramCode = N'BSCS' ORDER BY ProgramID);
IF @ProgramId IS NULL
    SET @ProgramId = (SELECT TOP 1 ProgramID FROM AcademicPrograms ORDER BY ProgramID);

DECLARE @SessionId INT = (SELECT TOP 1 SessionID FROM AcademicSessions WHERE ProgramID = @ProgramId ORDER BY SessionID DESC);
DECLARE @HodId INT = (SELECT TOP 1 UserID FROM Users WHERE Email = N'munir@biit.edu.pk');
DECLARE @TeacherA INT = (SELECT TOP 1 UserID FROM Users WHERE Email = N'aftab@biit.edu.pk');
DECLARE @TeacherB INT = (SELECT TOP 1 UserID FROM Users WHERE Email = N'shahid_jamil@biit.edu.pk');
DECLARE @CourseId INT = (SELECT TOP 1 CourseID FROM Courses WHERE CourseCode = N'CAI-261');
DECLARE @ProgramCourseId INT = (
    SELECT TOP 1 ProgramCourseID
    FROM AcademicProgramCourses
    WHERE ProgramID = @ProgramId AND CourseID = @CourseId
);

DECLARE @SectionA INT = (
    SELECT TOP 1 s.SectionID
    FROM Sections s
    JOIN OfferedCourses oc ON oc.SectionID = s.SectionID
    WHERE s.SessionID = @SessionId AND oc.CourseID = @CourseId
      AND (s.SectionName LIKE N'%A' OR s.SectionName LIKE N'%-A' OR s.SectionName LIKE N'% A')
    ORDER BY s.SectionID
);

DECLARE @SectionB INT = (
    SELECT TOP 1 s.SectionID
    FROM Sections s
    JOIN OfferedCourses oc ON oc.SectionID = s.SectionID
    WHERE s.SessionID = @SessionId AND oc.CourseID = @CourseId
      AND s.SectionID <> ISNULL(@SectionA, -1)
    ORDER BY s.SectionID
);

DECLARE @OfferedA INT = (
    SELECT TOP 1 OfferedCourseID FROM OfferedCourses
    WHERE CourseID = @CourseId AND SectionID = @SectionA
);
DECLARE @OfferedB INT = (
    SELECT TOP 1 OfferedCourseID FROM OfferedCourses
    WHERE CourseID = @CourseId AND SectionID = @SectionB
);

IF @ProgramId IS NULL OR @SessionId IS NULL OR @CourseId IS NULL OR @ProgramCourseId IS NULL
   OR @SectionA IS NULL OR @OfferedA IS NULL OR @TeacherA IS NULL OR @HodId IS NULL
BEGIN
    RAISERROR(N'Demo seed prerequisites missing (program/session/CAI-261/Section A/teacher/HOD).', 16, 1);
    RETURN;
END

PRINT 'Updating demo account passwords...';
UPDATE Users SET PasswordHash = @PwdHod WHERE UserID = @HodId;
UPDATE Users SET PasswordHash = @PwdTeacher WHERE UserID IN (@TeacherA, ISNULL(@TeacherB, -1));

DECLARE @AdminRole INT = (SELECT RoleID FROM Roles WHERE RoleName = N'Admin');
IF @AdminRole IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Users WHERE Email = N'sectionadmin@biit.edu.pk')
BEGIN
    INSERT INTO Users (FullName, Email, PasswordHash, RoleID, Rank, AridNo)
    VALUES (N'Second Administrator', N'sectionadmin@biit.edu.pk', @PwdSa, @AdminRole, N'Administrator', NULL);
END
ELSE IF EXISTS (SELECT 1 FROM Users WHERE Email = N'sectionadmin@biit.edu.pk') AND @AdminRole IS NOT NULL
BEGIN
    UPDATE Users
    SET PasswordHash = @PwdSa,
        RoleID = @AdminRole,
        FullName = N'Second Administrator',
        Rank = N'Administrator'
    WHERE Email = N'sectionadmin@biit.edu.pk';
END

PRINT 'Seeding PEOs / PLOs / program mappings...';

DELETE FROM PEOPLEMappings WHERE PEOID IN (SELECT PEOID FROM PEOs WHERE ProgramID = @ProgramId AND PEOCode LIKE N'DEMO-%');
DELETE FROM ProgramCoursePLOMappings WHERE PLOID IN (SELECT PLOID FROM PLOs WHERE ProgramID = @ProgramId AND PLOCode LIKE N'DEMO-%');
IF OBJECT_ID(N'dbo.PLOAttainmentResults', N'U') IS NOT NULL
    DELETE FROM PLOAttainmentResults WHERE PLOID IN (SELECT PLOID FROM PLOs WHERE ProgramID = @ProgramId AND PLOCode LIKE N'DEMO-%');
DELETE FROM PLOCLOMappings WHERE PLOID IN (SELECT PLOID FROM PLOs WHERE ProgramID = @ProgramId AND PLOCode LIKE N'DEMO-%');
DELETE FROM PEOs WHERE ProgramID = @ProgramId AND PEOCode LIKE N'DEMO-%';
DELETE FROM PLOs WHERE ProgramID = @ProgramId AND PLOCode LIKE N'DEMO-%';

INSERT INTO PEOs (ProgramID, PEOCode, PEODescription) VALUES
(@ProgramId, N'DEMO-PEO1', N'Demo: Apply computing knowledge to solve real-world problems.'),
(@ProgramId, N'DEMO-PEO2', N'Demo: Communicate and collaborate effectively in teams.'),
(@ProgramId, N'DEMO-PEO3', N'Demo: Pursue continuous learning and professional ethics.');

INSERT INTO PLOs (ProgramID, PLOCode, PLODescription) VALUES
(@ProgramId, N'DEMO-PLO1', N'Demo: Analyze a complex computing problem and apply principles of computing.'),
(@ProgramId, N'DEMO-PLO2', N'Demo: Design, implement and evaluate a computing-based solution.'),
(@ProgramId, N'DEMO-PLO3', N'Demo: Communicate effectively in a variety of professional contexts.'),
(@ProgramId, N'DEMO-PLO4', N'Demo: Recognize professional responsibilities and make informed judgments.');

DECLARE @Peo1 INT = (SELECT PEOID FROM PEOs WHERE ProgramID = @ProgramId AND PEOCode = N'DEMO-PEO1');
DECLARE @Peo2 INT = (SELECT PEOID FROM PEOs WHERE ProgramID = @ProgramId AND PEOCode = N'DEMO-PEO2');
DECLARE @Peo3 INT = (SELECT PEOID FROM PEOs WHERE ProgramID = @ProgramId AND PEOCode = N'DEMO-PEO3');
DECLARE @Plo1 INT = (SELECT PLOID FROM PLOs WHERE ProgramID = @ProgramId AND PLOCode = N'DEMO-PLO1');
DECLARE @Plo2 INT = (SELECT PLOID FROM PLOs WHERE ProgramID = @ProgramId AND PLOCode = N'DEMO-PLO2');
DECLARE @Plo3 INT = (SELECT PLOID FROM PLOs WHERE ProgramID = @ProgramId AND PLOCode = N'DEMO-PLO3');
DECLARE @Plo4 INT = (SELECT PLOID FROM PLOs WHERE ProgramID = @ProgramId AND PLOCode = N'DEMO-PLO4');

INSERT INTO PEOPLEMappings (PEOID, PLOID, IsMapped, MappedByUserID) VALUES
(@Peo1, @Plo1, 1, @HodId),
(@Peo1, @Plo2, 1, @HodId),
(@Peo2, @Plo3, 1, @HodId),
(@Peo3, @Plo4, 1, @HodId);

-- Link demo PLOs to several program courses (including CAI-261)
INSERT INTO ProgramCoursePLOMappings (ProgramCourseID, PLOID, Percentage, MappedByUserID)
SELECT pc.ProgramCourseID, v.PLOID, v.Pct, @HodId
FROM AcademicProgramCourses pc
CROSS APPLY (VALUES
    (@Plo1, CAST(40 AS DECIMAL(5,2))),
    (@Plo2, CAST(40 AS DECIMAL(5,2))),
    (@Plo3, CAST(10 AS DECIMAL(5,2))),
    (@Plo4, CAST(10 AS DECIMAL(5,2)))
) v(PLOID, Pct)
WHERE pc.ProgramID = @ProgramId
  AND pc.CourseID IN (@CourseId,
      (SELECT TOP 1 CourseID FROM Courses WHERE CourseCode = N'CAI-262'),
      (SELECT TOP 1 CourseID FROM Courses WHERE CourseCode = N'CSC-211'))
  AND NOT EXISTS (
      SELECT 1 FROM ProgramCoursePLOMappings x
      WHERE x.ProgramCourseID = pc.ProgramCourseID AND x.PLOID = v.PLOID
  );

PRINT 'Seeding students for Section A/B...';
IF OBJECT_ID(N'dbo.StudentOfferedCourses', N'U') IS NOT NULL
BEGIN
    DELETE FROM StudentOfferedCourses WHERE StudentID IN (
        SELECT StudentID FROM Students WHERE SectionID IN (@SectionA, ISNULL(@SectionB, -1)) AND AridNo LIKE N'DEMO-%');
END
DELETE FROM StudentQuestionMarks WHERE StudentID IN (
    SELECT StudentID FROM Students WHERE SectionID IN (@SectionA, ISNULL(@SectionB, -1)) AND AridNo LIKE N'DEMO-%');
DELETE FROM StudentAssessmentMarks WHERE StudentID IN (
    SELECT StudentID FROM Students WHERE SectionID IN (@SectionA, ISNULL(@SectionB, -1)) AND AridNo LIKE N'DEMO-%');
DELETE FROM Students WHERE SectionID IN (@SectionA, ISNULL(@SectionB, -1)) AND AridNo LIKE N'DEMO-%';

INSERT INTO Students (SectionID, FullName, AridNo, Email) VALUES
(@SectionA, N'Ali Demo', N'DEMO-21-ARID-0001', N'ali.demo@student.biit.edu.pk'),
(@SectionA, N'Sara Demo', N'DEMO-21-ARID-0002', N'sara.demo@student.biit.edu.pk'),
(@SectionA, N'Hassan Demo', N'DEMO-21-ARID-0003', N'hassan.demo@student.biit.edu.pk'),
(@SectionA, N'Fatima Demo', N'DEMO-21-ARID-0004', N'fatima.demo@student.biit.edu.pk'),
(@SectionA, N'Usman Demo', N'DEMO-21-ARID-0005', N'usman.demo@student.biit.edu.pk');

IF @SectionB IS NOT NULL
BEGIN
    INSERT INTO Students (SectionID, FullName, AridNo, Email) VALUES
    (@SectionB, N'Bilal Demo', N'DEMO-21-ARID-0101', N'bilal.demo@student.biit.edu.pk'),
    (@SectionB, N'Ayesha Demo', N'DEMO-21-ARID-0102', N'ayesha.demo@student.biit.edu.pk'),
    (@SectionB, N'Omar Demo', N'DEMO-21-ARID-0103', N'omar.demo@student.biit.edu.pk');
END

-- Enroll demo students into their section's offered courses (required for mark sheets).
IF OBJECT_ID(N'dbo.StudentOfferedCourses', N'U') IS NOT NULL
BEGIN
    INSERT INTO StudentOfferedCourses (StudentID, OfferedCourseID)
    SELECT s.StudentID, @OfferedA
    FROM Students s
    WHERE s.SectionID = @SectionA AND s.AridNo LIKE N'DEMO-%'
      AND NOT EXISTS (
          SELECT 1 FROM StudentOfferedCourses soc
          WHERE soc.StudentID = s.StudentID AND soc.OfferedCourseID = @OfferedA);

    IF @SectionB IS NOT NULL AND @OfferedB IS NOT NULL
    BEGIN
        INSERT INTO StudentOfferedCourses (StudentID, OfferedCourseID)
        SELECT s.StudentID, @OfferedB
        FROM Students s
        WHERE s.SectionID = @SectionB AND s.AridNo LIKE N'DEMO-%'
          AND NOT EXISTS (
              SELECT 1 FROM StudentOfferedCourses soc
              WHERE soc.StudentID = s.StudentID AND soc.OfferedCourseID = @OfferedB);
    END
END

PRINT 'Granting Section A teacher all 6 modern permissions for CAI-261...';
DECLARE @PermClos INT = (SELECT PermissionTypeID FROM TeacherPermissionTypes WHERE PermissionType = N'Manage CLOs');
DECLARE @PermWeekly INT = (SELECT PermissionTypeID FROM TeacherPermissionTypes WHERE PermissionType = N'Upload Weekly Content');
DECLARE @PermMid INT = (SELECT PermissionTypeID FROM TeacherPermissionTypes WHERE PermissionType = N'Upload Mid Exam Questions');
DECLARE @PermFinal INT = (SELECT PermissionTypeID FROM TeacherPermissionTypes WHERE PermissionType = N'Upload Final Exam Questions');
DECLARE @PermCloPlo INT = (SELECT PermissionTypeID FROM TeacherPermissionTypes WHERE PermissionType = N'Manage CLO-PLO Mapping');
DECLARE @PermCloTask INT = (SELECT PermissionTypeID FROM TeacherPermissionTypes WHERE PermissionType = N'Manage CLO-Task Mapping');

DELETE FROM TeacherPermissions
WHERE ProgramCourseID = @ProgramCourseId
  AND PermissionTypeID IN (@PermClos, @PermWeekly, @PermMid, @PermFinal, @PermCloPlo, @PermCloTask);

INSERT INTO TeacherPermissions (TeacherUserID, ProgramCourseID, PermissionTypeID, HODUserID, IsAllowed)
VALUES
(@TeacherA, @ProgramCourseId, @PermClos, @HodId, 1),
(@TeacherA, @ProgramCourseId, @PermWeekly, @HodId, 1),
(@TeacherA, @ProgramCourseId, @PermMid, @HodId, 1),
(@TeacherA, @ProgramCourseId, @PermFinal, @HodId, 1),
(@TeacherA, @ProgramCourseId, @PermCloPlo, @HodId, 1),
(@TeacherA, @ProgramCourseId, @PermCloTask, @HodId, 1);

PRINT 'Clearing prior CAI-261 session academic content for reseed...';

-- Attainment
IF OBJECT_ID(N'dbo.PLOAttainmentResults', N'U') IS NOT NULL
    DELETE FROM PLOAttainmentResults WHERE OfferedCourseID IN (@OfferedA, ISNULL(@OfferedB, -1));
DELETE FROM CLOAttainmentResults WHERE OfferedCourseID IN (@OfferedA, ISNULL(@OfferedB, -1));
DELETE FROM CLOAttainmentResults WHERE CLOID IN (
    SELECT CLOID FROM CLOs WHERE ProgramCourseID = @ProgramCourseId AND SessionID = @SessionId);

-- Marks / assessments for these offerings
DELETE FROM StudentQuestionMarks WHERE QuestionID IN (
    SELECT q.QuestionID FROM AssessmentQuestions q
    JOIN Assessments a ON a.AssessmentID = q.AssessmentID
    WHERE a.OfferedCourseID IN (@OfferedA, ISNULL(@OfferedB, -1)));
DELETE FROM AssessmentQuestionCLOMappings WHERE QuestionID IN (
    SELECT q.QuestionID FROM AssessmentQuestions q
    JOIN Assessments a ON a.AssessmentID = q.AssessmentID
    WHERE a.OfferedCourseID IN (@OfferedA, ISNULL(@OfferedB, -1)));
IF OBJECT_ID(N'dbo.StudentAssessmentMarks', N'U') IS NOT NULL
    DELETE FROM StudentAssessmentMarks WHERE AssessmentID IN (
        SELECT AssessmentID FROM Assessments WHERE OfferedCourseID IN (@OfferedA, ISNULL(@OfferedB, -1)));
IF OBJECT_ID(N'dbo.GraderTasks', N'U') IS NOT NULL
    DELETE FROM GraderTasks WHERE AssessmentID IN (
        SELECT AssessmentID FROM Assessments WHERE OfferedCourseID IN (@OfferedA, ISNULL(@OfferedB, -1)));
IF OBJECT_ID(N'dbo.AssessmentFiles', N'U') IS NOT NULL
    DELETE FROM AssessmentFiles WHERE AssessmentID IN (
        SELECT AssessmentID FROM Assessments WHERE OfferedCourseID IN (@OfferedA, ISNULL(@OfferedB, -1)));
DELETE FROM AssessmentQuestions WHERE AssessmentID IN (
    SELECT AssessmentID FROM Assessments WHERE OfferedCourseID IN (@OfferedA, ISNULL(@OfferedB, -1)));
DELETE FROM Assessments WHERE OfferedCourseID IN (@OfferedA, ISNULL(@OfferedB, -1));

DELETE FROM OfferedCourseTopicProgress WHERE OfferedCourseID IN (@OfferedA, ISNULL(@OfferedB, -1));
DELETE FROM CourseTopicCLOMappings WHERE TopicID IN (
    SELECT TopicID FROM CourseTopics WHERE ProgramCourseID = @ProgramCourseId AND SessionID = @SessionId);
DELETE FROM CourseTopics WHERE ProgramCourseID = @ProgramCourseId AND SessionID = @SessionId;

DELETE FROM CourseAssessmentTypeCLOMappings WHERE ProgramCourseID = @ProgramCourseId AND SessionID = @SessionId;
DELETE FROM PLOCLOMappings WHERE ProgramCourseID = @ProgramCourseId AND SessionID = @SessionId;
-- Any leftover question→CLO links for these CLOs (other offerings)
DELETE FROM AssessmentQuestionCLOMappings WHERE CLOID IN (
    SELECT CLOID FROM CLOs WHERE ProgramCourseID = @ProgramCourseId AND SessionID = @SessionId);
DELETE FROM CLOs WHERE ProgramCourseID = @ProgramCourseId AND SessionID = @SessionId;
IF OBJECT_ID(N'dbo.FolderSubmissionIssues', N'U') IS NOT NULL
    DELETE FROM FolderSubmissionIssues WHERE SubmissionID IN (
        SELECT SubmissionID FROM FolderSubmissions
        WHERE ContentSubmissionID IN (
            SELECT SubmissionID FROM ProgramCourseContentSubmissions
            WHERE ProgramCourseID = @ProgramCourseId AND SessionID = @SessionId));
IF OBJECT_ID(N'dbo.FolderSubmissions', N'U') IS NOT NULL
    DELETE FROM FolderSubmissions WHERE ContentSubmissionID IN (
        SELECT SubmissionID FROM ProgramCourseContentSubmissions
        WHERE ProgramCourseID = @ProgramCourseId AND SessionID = @SessionId);
DELETE FROM ProgramCourseContentSubmissions WHERE ProgramCourseID = @ProgramCourseId AND SessionID = @SessionId;

PRINT 'Seeding approved CLOs + submissions...';
DECLARE @CtClos INT = (SELECT ContentTypeID FROM ContentSubmissionTypes WHERE ContentTypeCode = N'CLOs');
DECLARE @CtCloPlo INT = (SELECT ContentTypeID FROM ContentSubmissionTypes WHERE ContentTypeCode = N'CloPloMapping');
DECLARE @CtCloTask INT = (SELECT ContentTypeID FROM ContentSubmissionTypes WHERE ContentTypeCode = N'CloTaskMapping');
DECLARE @CtWeekly INT = (SELECT ContentTypeID FROM ContentSubmissionTypes WHERE ContentTypeCode = N'WeeklyContent');

INSERT INTO ProgramCourseContentSubmissions
    (ProgramCourseID, SessionID, ContentTypeID, SubmittedByUserID, SubmittedAt, SubmissionStatus,
     ReviewedByUserID, ReviewedAt, HodFeedback, VersionNo, IsCurrentApproved)
VALUES
(@ProgramCourseId, @SessionId, @CtClos, @TeacherA, GETUTCDATE(), N'Approved', @HodId, GETUTCDATE(), N'Demo approved', 1, 1);

DECLARE @SubClos INT = SCOPE_IDENTITY();

INSERT INTO CLOs (ProgramCourseID, CLOCode, CLODescription, SessionID, ContentStatus, SubmissionID) VALUES
(@ProgramCourseId, N'CLO1', N'Demo: Explain core AI programming concepts and Python foundations.', @SessionId, N'Approved', @SubClos),
(@ProgramCourseId, N'CLO2', N'Demo: Implement algorithms for data processing and simple ML workflows.', @SessionId, N'Approved', @SubClos),
(@ProgramCourseId, N'CLO3', N'Demo: Evaluate AI program outputs and document results professionally.', @SessionId, N'Approved', @SubClos);

DECLARE @Clo1 INT = (SELECT CLOID FROM CLOs WHERE ProgramCourseID = @ProgramCourseId AND SessionID = @SessionId AND CLOCode = N'CLO1');
DECLARE @Clo2 INT = (SELECT CLOID FROM CLOs WHERE ProgramCourseID = @ProgramCourseId AND SessionID = @SessionId AND CLOCode = N'CLO2');
DECLARE @Clo3 INT = (SELECT CLOID FROM CLOs WHERE ProgramCourseID = @ProgramCourseId AND SessionID = @SessionId AND CLOCode = N'CLO3');

PRINT 'Seeding approved CLO-PLO mapping...';
INSERT INTO ProgramCourseContentSubmissions
    (ProgramCourseID, SessionID, ContentTypeID, SubmittedByUserID, SubmittedAt, SubmissionStatus,
     ReviewedByUserID, ReviewedAt, HodFeedback, VersionNo, IsCurrentApproved)
VALUES
(@ProgramCourseId, @SessionId, @CtCloPlo, @TeacherA, GETUTCDATE(), N'Approved', @HodId, GETUTCDATE(), N'Demo approved', 1, 1);
DECLARE @SubCloPlo INT = SCOPE_IDENTITY();

INSERT INTO PLOCLOMappings (ProgramCourseID, SessionID, PLOID, CLOID, Percentage, UpdatedByUserID, ContentStatus, SubmissionID) VALUES
(@ProgramCourseId, @SessionId, @Plo1, @Clo1, 40, @TeacherA, N'Approved', @SubCloPlo),
(@ProgramCourseId, @SessionId, @Plo1, @Clo2, 40, @TeacherA, N'Approved', @SubCloPlo),
(@ProgramCourseId, @SessionId, @Plo1, @Clo3, 20, @TeacherA, N'Approved', @SubCloPlo),
(@ProgramCourseId, @SessionId, @Plo2, @Clo2, 60, @TeacherA, N'Approved', @SubCloPlo),
(@ProgramCourseId, @SessionId, @Plo2, @Clo3, 40, @TeacherA, N'Approved', @SubCloPlo),
(@ProgramCourseId, @SessionId, @Plo3, @Clo3, 100, @TeacherA, N'Approved', @SubCloPlo),
(@ProgramCourseId, @SessionId, @Plo4, @Clo1, 50, @TeacherA, N'Approved', @SubCloPlo),
(@ProgramCourseId, @SessionId, @Plo4, @Clo3, 50, @TeacherA, N'Approved', @SubCloPlo);

PRINT 'Seeding approved CLO-Task mapping (100% per CLO)...';
INSERT INTO ProgramCourseContentSubmissions
    (ProgramCourseID, SessionID, ContentTypeID, SubmittedByUserID, SubmittedAt, SubmissionStatus,
     ReviewedByUserID, ReviewedAt, HodFeedback, VersionNo, IsCurrentApproved)
VALUES
(@ProgramCourseId, @SessionId, @CtCloTask, @TeacherA, GETUTCDATE(), N'Approved', @HodId, GETUTCDATE(), N'Demo approved', 1, 1);
DECLARE @SubCloTask INT = SCOPE_IDENTITY();

DECLARE @TypeAssign INT = (SELECT AssessmentTypeID FROM AssessmentTypes WHERE AssessmentTypeName = N'Assignment');
DECLARE @TypeQuiz INT = (SELECT AssessmentTypeID FROM AssessmentTypes WHERE AssessmentTypeName = N'Quiz');
DECLARE @TypeMid INT = (SELECT AssessmentTypeID FROM AssessmentTypes WHERE AssessmentTypeName = N'Mid');
DECLARE @TypeFinal INT = (SELECT AssessmentTypeID FROM AssessmentTypes WHERE AssessmentTypeName = N'Final');

-- CLO1: Quiz 20, Assignment 30, Mid 25, Final 25
-- CLO2: Quiz 15, Assignment 35, Mid 25, Final 25
-- CLO3: Quiz 10, Assignment 20, Mid 30, Final 40
INSERT INTO CourseAssessmentTypeCLOMappings
    (ProgramCourseID, SessionID, AssessmentTypeID, CLOID, Percentage, UpdatedByUserID, ContentStatus, SubmissionID)
VALUES
(@ProgramCourseId, @SessionId, @TypeQuiz, @Clo1, 20, @TeacherA, N'Approved', @SubCloTask),
(@ProgramCourseId, @SessionId, @TypeAssign, @Clo1, 30, @TeacherA, N'Approved', @SubCloTask),
(@ProgramCourseId, @SessionId, @TypeMid, @Clo1, 25, @TeacherA, N'Approved', @SubCloTask),
(@ProgramCourseId, @SessionId, @TypeFinal, @Clo1, 25, @TeacherA, N'Approved', @SubCloTask),
(@ProgramCourseId, @SessionId, @TypeQuiz, @Clo2, 15, @TeacherA, N'Approved', @SubCloTask),
(@ProgramCourseId, @SessionId, @TypeAssign, @Clo2, 35, @TeacherA, N'Approved', @SubCloTask),
(@ProgramCourseId, @SessionId, @TypeMid, @Clo2, 25, @TeacherA, N'Approved', @SubCloTask),
(@ProgramCourseId, @SessionId, @TypeFinal, @Clo2, 25, @TeacherA, N'Approved', @SubCloTask),
(@ProgramCourseId, @SessionId, @TypeQuiz, @Clo3, 10, @TeacherA, N'Approved', @SubCloTask),
(@ProgramCourseId, @SessionId, @TypeAssign, @Clo3, 20, @TeacherA, N'Approved', @SubCloTask),
(@ProgramCourseId, @SessionId, @TypeMid, @Clo3, 30, @TeacherA, N'Approved', @SubCloTask),
(@ProgramCourseId, @SessionId, @TypeFinal, @Clo3, 40, @TeacherA, N'Approved', @SubCloTask);

PRINT 'Seeding weekly topics + progress...';
INSERT INTO ProgramCourseContentSubmissions
    (ProgramCourseID, SessionID, ContentTypeID, SubmittedByUserID, SubmittedAt, SubmissionStatus,
     ReviewedByUserID, ReviewedAt, HodFeedback, VersionNo, IsCurrentApproved)
VALUES
(@ProgramCourseId, @SessionId, @CtWeekly, @TeacherA, GETUTCDATE(), N'Approved', @HodId, GETUTCDATE(), N'Demo approved', 1, 1);
DECLARE @SubWeekly INT = SCOPE_IDENTITY();

INSERT INTO CourseTopics (ProgramCourseID, ParentTopicID, WeekNo, TopicTitle, TopicDescription, DisplayOrder, SessionID, ContentStatus, SubmissionID)
VALUES
(@ProgramCourseId, NULL, 1, N'Python & AI setup', N'Demo week 1', 1, @SessionId, N'Approved', @SubWeekly);
DECLARE @Topic1 INT = SCOPE_IDENTITY();
INSERT INTO CourseTopics (ProgramCourseID, ParentTopicID, WeekNo, TopicTitle, TopicDescription, DisplayOrder, SessionID, ContentStatus, SubmissionID)
VALUES
(@ProgramCourseId, NULL, 2, N'Data structures for AI', N'Demo week 2', 2, @SessionId, N'Approved', @SubWeekly);
DECLARE @Topic2 INT = SCOPE_IDENTITY();
INSERT INTO CourseTopics (ProgramCourseID, ParentTopicID, WeekNo, TopicTitle, TopicDescription, DisplayOrder, SessionID, ContentStatus, SubmissionID)
VALUES
(@ProgramCourseId, NULL, 3, N'Intro ML pipeline', N'Demo week 3', 3, @SessionId, N'Approved', @SubWeekly);
DECLARE @Topic3 INT = SCOPE_IDENTITY();

INSERT INTO CourseTopicCLOMappings (TopicID, CLOID) VALUES
(@Topic1, @Clo1), (@Topic2, @Clo1), (@Topic2, @Clo2), (@Topic3, @Clo2), (@Topic3, @Clo3);

INSERT INTO OfferedCourseTopicProgress (OfferedCourseID, TopicID, IsCompleted, CompletedAt, CompletedByUserID)
VALUES
(@OfferedA, @Topic1, 1, GETUTCDATE(), @TeacherA),
(@OfferedA, @Topic2, 1, GETUTCDATE(), @TeacherA),
(@OfferedA, @Topic3, 0, NULL, NULL);

PRINT 'Seeding assessments, questions, and student marks (Section A)...';
INSERT INTO Assessments (OfferedCourseID, AssessmentTypeID, AssessmentNo, Title, DueDate, TotalMarks, CreatedByUserID, CreatedAt)
VALUES (@OfferedA, @TypeQuiz, 1, N'Demo Quiz 1', DATEADD(DAY, -20, CAST(GETDATE() AS DATE)), NULL, @TeacherA, GETUTCDATE());
DECLARE @QuizId INT = SCOPE_IDENTITY();

INSERT INTO Assessments (OfferedCourseID, AssessmentTypeID, AssessmentNo, Title, DueDate, TotalMarks, CreatedByUserID, CreatedAt)
VALUES (@OfferedA, @TypeAssign, 1, N'Demo Assignment 1', DATEADD(DAY, -10, CAST(GETDATE() AS DATE)), NULL, @TeacherA, GETUTCDATE());
DECLARE @AssignId INT = SCOPE_IDENTITY();

INSERT INTO Assessments (OfferedCourseID, AssessmentTypeID, AssessmentNo, Title, DueDate, TotalMarks, CreatedByUserID, CreatedAt)
VALUES (@OfferedA, @TypeMid, 1, N'Demo Mid Exam', DATEADD(DAY, -5, CAST(GETDATE() AS DATE)), NULL, @TeacherA, GETUTCDATE());
DECLARE @MidId INT = SCOPE_IDENTITY();

INSERT INTO Assessments (OfferedCourseID, AssessmentTypeID, AssessmentNo, Title, DueDate, TotalMarks, CreatedByUserID, CreatedAt)
VALUES (@OfferedA, @TypeFinal, 1, N'Demo Final Exam', DATEADD(DAY, 10, CAST(GETDATE() AS DATE)), NULL, @TeacherA, GETUTCDATE());
DECLARE @FinalId INT = SCOPE_IDENTITY();

-- Marks omitted: coverage is via AssessmentQuestionCLOMappings.Percentage (of type share).
INSERT INTO AssessmentQuestions (AssessmentID, QuestionNo, QuestionText, Marks) VALUES
(@QuizId, N'Q1', N'Demo quiz: What is a Python list comprehension?', NULL),
(@QuizId, N'Q2', N'Demo quiz: Name one AI library in Python.', NULL);
DECLARE @Qq1 INT = (SELECT QuestionID FROM AssessmentQuestions WHERE AssessmentID = @QuizId AND QuestionNo = N'Q1');
DECLARE @Qq2 INT = (SELECT QuestionID FROM AssessmentQuestions WHERE AssessmentID = @QuizId AND QuestionNo = N'Q2');

INSERT INTO AssessmentQuestions (AssessmentID, QuestionNo, QuestionText, Marks) VALUES
(@AssignId, N'Q1', N'Demo assignment: Implement a data cleaning function.', NULL),
(@AssignId, N'Q2', N'Demo assignment: Document evaluation metrics used.', NULL);
DECLARE @Qa1 INT = (SELECT QuestionID FROM AssessmentQuestions WHERE AssessmentID = @AssignId AND QuestionNo = N'Q1');
DECLARE @Qa2 INT = (SELECT QuestionID FROM AssessmentQuestions WHERE AssessmentID = @AssignId AND QuestionNo = N'Q2');

INSERT INTO AssessmentQuestions (AssessmentID, QuestionNo, QuestionText, Marks) VALUES
(@MidId, N'Q1', N'Demo mid: Explain supervised vs unsupervised learning.', NULL),
(@MidId, N'Q2', N'Demo mid: Write a short ML pipeline outline.', NULL);
DECLARE @Qm1 INT = (SELECT QuestionID FROM AssessmentQuestions WHERE AssessmentID = @MidId AND QuestionNo = N'Q1');
DECLARE @Qm2 INT = (SELECT QuestionID FROM AssessmentQuestions WHERE AssessmentID = @MidId AND QuestionNo = N'Q2');

INSERT INTO AssessmentQuestions (AssessmentID, QuestionNo, QuestionText, Marks) VALUES
(@FinalId, N'Q1', N'Demo final: Design an AI solution for a campus problem.', NULL),
(@FinalId, N'Q2', N'Demo final: Discuss ethics and limitations of your solution.', NULL);
DECLARE @Qf1 INT = (SELECT QuestionID FROM AssessmentQuestions WHERE AssessmentID = @FinalId AND QuestionNo = N'Q1');
DECLARE @Qf2 INT = (SELECT QuestionID FROM AssessmentQuestions WHERE AssessmentID = @FinalId AND QuestionNo = N'Q2');

-- Coverage % = share of that assessment-type bucket for the CLO (sums ≤ 100 per CLO per type).
INSERT INTO AssessmentQuestionCLOMappings (QuestionID, CLOID, Percentage) VALUES
(@Qq1, @Clo1, 50), (@Qq2, @Clo1, 50),
(@Qa1, @Clo2, 60), (@Qa2, @Clo3, 100),
(@Qm1, @Clo1, 50), (@Qm1, @Clo2, 40), (@Qm2, @Clo2, 60),
(@Qf1, @Clo2, 50), (@Qf2, @Clo3, 50);

-- Student scores as 0–100% of each question (marks removed from questions).
DECLARE @S1 INT = (SELECT StudentID FROM Students WHERE AridNo = N'DEMO-21-ARID-0001');
DECLARE @S2 INT = (SELECT StudentID FROM Students WHERE AridNo = N'DEMO-21-ARID-0002');
DECLARE @S3 INT = (SELECT StudentID FROM Students WHERE AridNo = N'DEMO-21-ARID-0003');
DECLARE @S4 INT = (SELECT StudentID FROM Students WHERE AridNo = N'DEMO-21-ARID-0004');
DECLARE @S5 INT = (SELECT StudentID FROM Students WHERE AridNo = N'DEMO-21-ARID-0005');

;WITH Marks(StudentID, QuestionID, ObtainedMarks) AS (
    SELECT @S1, @Qq1, 100 UNION ALL SELECT @S1, @Qq2, 80 UNION ALL SELECT @S1, @Qa1, 90 UNION ALL SELECT @S1, @Qa2, 80
    UNION ALL SELECT @S1, @Qm1, 88 UNION ALL SELECT @S1, @Qm2, 80 UNION ALL SELECT @S1, @Qf1, 84 UNION ALL SELECT @S1, @Qf2, 80
    UNION ALL SELECT @S2, @Qq1, 80 UNION ALL SELECT @S2, @Qq2, 100 UNION ALL SELECT @S2, @Qa1, 80 UNION ALL SELECT @S2, @Qa2, 90
    UNION ALL SELECT @S2, @Qm1, 80 UNION ALL SELECT @S2, @Qm2, 72 UNION ALL SELECT @S2, @Qf1, 76 UNION ALL SELECT @S2, @Qf2, 72
    UNION ALL SELECT @S3, @Qq1, 60 UNION ALL SELECT @S3, @Qq2, 60 UNION ALL SELECT @S3, @Qa1, 70 UNION ALL SELECT @S3, @Qa2, 60
    UNION ALL SELECT @S3, @Qm1, 60 UNION ALL SELECT @S3, @Qm2, 56 UNION ALL SELECT @S3, @Qf1, 60 UNION ALL SELECT @S3, @Qf2, 56
    UNION ALL SELECT @S4, @Qq1, 100 UNION ALL SELECT @S4, @Qq2, 100 UNION ALL SELECT @S4, @Qa1, 100 UNION ALL SELECT @S4, @Qa2, 90
    UNION ALL SELECT @S4, @Qm1, 96 UNION ALL SELECT @S4, @Qm2, 92 UNION ALL SELECT @S4, @Qf1, 90 UNION ALL SELECT @S4, @Qf2, 88
    UNION ALL SELECT @S5, @Qq1, 40 UNION ALL SELECT @S5, @Qq2, 60 UNION ALL SELECT @S5, @Qa1, 50 UNION ALL SELECT @S5, @Qa2, 50
    UNION ALL SELECT @S5, @Qm1, 48 UNION ALL SELECT @S5, @Qm2, 44 UNION ALL SELECT @S5, @Qf1, 50 UNION ALL SELECT @S5, @Qf2, 44
)
INSERT INTO StudentQuestionMarks (QuestionID, StudentID, ObtainedMarks)
SELECT QuestionID, StudentID, ObtainedMarks FROM Marks;

PRINT '=========================================================';
PRINT 'Demo seed complete.';
PRINT 'Logins:';
PRINT '  Admin         admin@biit.edu.pk / Admin@BIIT#2026';
PRINT '  Admin (2nd)   sectionadmin@biit.edu.pk / SectionAdmin@123';
PRINT '  HOD           munir@biit.edu.pk / Hod@123';
PRINT '  Teacher A     aftab@biit.edu.pk / Teacher@123   (CAI-261 BAI-4A, all permissions)';
PRINT '  Teacher B     shahid_jamil@biit.edu.pk / Teacher@123';
PRINT 'Test subject: CAI-261 Programming for AI — CLOs/mappings approved, marks ready.';
PRINT 'Teacher A → Results → Calculate CLO-PLO Attainment';
PRINT 'HOD → Current Session → CAI-261 → section → Attainment charts';
PRINT '=========================================================';
GO
