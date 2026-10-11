/*
  SeedBSCS3TestProgram.sql
  Fully completed test program: Computer Science 2 (BSCS3)

  What you can test after seeding:
  - HOD → Programs → Computer Science 2: catalog, PEOs/PLOs, mappings, session, offerings
  - HOD → Review: Submitted course folders (all 12 offerings)
  - Teacher qasim (1A) / saeed (1B) / mustafa (1C):
      Results/Attainment (CLO + PLO), Course Result (non-zero totals), Folders

  Re-runnable: removes prior BSCS3 tree first.
*/
USE FolderManagementDB;
GO
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

DECLARE @HodId INT = 2;   -- munir@biit.edu.pk
DECLARE @T1 INT = 4;      -- qasim — BSCS3-1A
DECLARE @T2 INT = 6;      -- saeed — BSCS3-1B
DECLARE @T3 INT = 7;      -- mustafa — BSCS3-1C

DECLARE @TypeAssign INT = (SELECT AssessmentTypeID FROM AssessmentTypes WHERE AssessmentTypeName = N'Assignment');
DECLARE @TypeQuiz INT = (SELECT AssessmentTypeID FROM AssessmentTypes WHERE AssessmentTypeName = N'Quiz');
DECLARE @TypeMid INT = (SELECT AssessmentTypeID FROM AssessmentTypes WHERE AssessmentTypeName = N'Mid');
DECLARE @TypeFinal INT = (SELECT AssessmentTypeID FROM AssessmentTypes WHERE AssessmentTypeName = N'Final');

DECLARE @PermClos INT = (SELECT PermissionTypeID FROM TeacherPermissionTypes WHERE PermissionType = N'Manage CLOs');
DECLARE @PermWeekly INT = (SELECT PermissionTypeID FROM TeacherPermissionTypes WHERE PermissionType = N'Upload Weekly Content');
DECLARE @PermMid INT = (SELECT PermissionTypeID FROM TeacherPermissionTypes WHERE PermissionType = N'Upload Mid Exam Questions');
DECLARE @PermFinal INT = (SELECT PermissionTypeID FROM TeacherPermissionTypes WHERE PermissionType = N'Upload Final Exam Questions');
DECLARE @PermCloPlo INT = (SELECT PermissionTypeID FROM TeacherPermissionTypes WHERE PermissionType = N'Manage CLO-PLO Mapping');
DECLARE @PermCloTask INT = (SELECT PermissionTypeID FROM TeacherPermissionTypes WHERE PermissionType = N'Manage CLO-Task Mapping');

DECLARE @CtClos INT = (SELECT ContentTypeID FROM ContentSubmissionTypes WHERE ContentTypeCode = N'CLOs');
DECLARE @CtWeekly INT = (SELECT ContentTypeID FROM ContentSubmissionTypes WHERE ContentTypeCode = N'WeeklyContent');
DECLARE @CtCloPlo INT = (SELECT ContentTypeID FROM ContentSubmissionTypes WHERE ContentTypeCode = N'CloPloMapping');
DECLARE @CtCloTask INT = (SELECT ContentTypeID FROM ContentSubmissionTypes WHERE ContentTypeCode = N'CloTaskMapping');

/* ---------- Wipe previous BSCS3 (expects at most one; run WipeBSCS3.sql first if dirty) ---------- */
DECLARE @OldProgramId INT = (SELECT TOP 1 ProgramID FROM AcademicPrograms WHERE ProgramCode = N'BSCS3' ORDER BY ProgramID);
IF @OldProgramId IS NOT NULL
BEGIN
    PRINT CONCAT('Removing previous BSCS3 ProgramID=', @OldProgramId);

    DECLARE @OldSessions TABLE (SessionID INT);
    INSERT INTO @OldSessions SELECT SessionID FROM AcademicSessions WHERE ProgramID = @OldProgramId;

    DECLARE @OldSections TABLE (SectionID INT);
    INSERT INTO @OldSections SELECT SectionID FROM Sections WHERE SessionID IN (SELECT SessionID FROM @OldSessions);

    DECLARE @OldOCs TABLE (OfferedCourseID INT PRIMARY KEY);
    INSERT INTO @OldOCs SELECT OfferedCourseID FROM OfferedCourses WHERE SectionID IN (SELECT SectionID FROM @OldSections);

    DECLARE @OldPCs TABLE (ProgramCourseID INT PRIMARY KEY);
    INSERT INTO @OldPCs SELECT ProgramCourseID FROM AcademicProgramCourses WHERE ProgramID = @OldProgramId;

    DECLARE @OldFolders TABLE (CourseFolderID INT PRIMARY KEY);
    INSERT INTO @OldFolders SELECT CourseFolderID FROM CourseFolders WHERE OfferedCourseID IN (SELECT OfferedCourseID FROM @OldOCs);

    DECLARE @OldA TABLE (AssessmentID INT PRIMARY KEY);
    INSERT INTO @OldA SELECT AssessmentID FROM Assessments WHERE OfferedCourseID IN (SELECT OfferedCourseID FROM @OldOCs);

    DECLARE @OldQ TABLE (QuestionID INT PRIMARY KEY);
    INSERT INTO @OldQ SELECT QuestionID FROM AssessmentQuestions WHERE AssessmentID IN (SELECT AssessmentID FROM @OldA);

    DELETE FROM StudentQuestionMarks WHERE QuestionID IN (SELECT QuestionID FROM @OldQ);
    DELETE FROM AssessmentQuestionCLOMappings WHERE QuestionID IN (SELECT QuestionID FROM @OldQ);
    DELETE FROM AssessmentQuestions WHERE QuestionID IN (SELECT QuestionID FROM @OldQ);
    DELETE FROM StudentAssessmentMarks WHERE AssessmentID IN (SELECT AssessmentID FROM @OldA);
    DELETE FROM StudentCourseResultSelections WHERE OfferedCourseID IN (SELECT OfferedCourseID FROM @OldOCs);
    DELETE FROM Assessments WHERE AssessmentID IN (SELECT AssessmentID FROM @OldA);
    DELETE FROM AttendanceRecords WHERE OfferedCourseID IN (SELECT OfferedCourseID FROM @OldOCs);

    DELETE FROM CLOAttainmentResults WHERE OfferedCourseID IN (SELECT OfferedCourseID FROM @OldOCs);
    DELETE FROM PLOAttainmentResults WHERE OfferedCourseID IN (SELECT OfferedCourseID FROM @OldOCs);

    IF OBJECT_ID(N'dbo.OfferedCourseHomeworkMarkSchemes', N'U') IS NOT NULL
        DELETE FROM OfferedCourseHomeworkMarkSchemes WHERE OfferedCourseID IN (SELECT OfferedCourseID FROM @OldOCs);
    IF OBJECT_ID(N'dbo.OfferedCourseLabMarkSchemes', N'U') IS NOT NULL
        DELETE FROM OfferedCourseLabMarkSchemes WHERE OfferedCourseID IN (SELECT OfferedCourseID FROM @OldOCs);
    IF OBJECT_ID(N'dbo.StudentCourseResults', N'U') IS NOT NULL
        DELETE FROM StudentCourseResults WHERE OfferedCourseID IN (SELECT OfferedCourseID FROM @OldOCs);
    IF OBJECT_ID(N'dbo.OfferedCourseResultSchemes', N'U') IS NOT NULL
        DELETE FROM OfferedCourseResultSchemes WHERE OfferedCourseID IN (SELECT OfferedCourseID FROM @OldOCs);

    DELETE FROM CourseFolderFiles WHERE DirectoryID IN (
        SELECT DirectoryID FROM CourseFolderDirectories WHERE CourseFolderID IN (SELECT CourseFolderID FROM @OldFolders));
    DELETE FROM CourseFolderDirectories WHERE CourseFolderID IN (SELECT CourseFolderID FROM @OldFolders);
    DELETE FROM FolderSubmissions WHERE CourseFolderID IN (SELECT CourseFolderID FROM @OldFolders);
    DELETE FROM CourseFolders WHERE CourseFolderID IN (SELECT CourseFolderID FROM @OldFolders);

    DELETE FROM StudentOfferedCourses WHERE OfferedCourseID IN (SELECT OfferedCourseID FROM @OldOCs);
    DELETE FROM OfferedCourses WHERE OfferedCourseID IN (SELECT OfferedCourseID FROM @OldOCs);
    DELETE FROM Students WHERE SectionID IN (SELECT SectionID FROM @OldSections);
    DELETE FROM Sections WHERE SectionID IN (SELECT SectionID FROM @OldSections);

    DELETE FROM PLOCLOMappings WHERE ProgramCourseID IN (SELECT ProgramCourseID FROM @OldPCs);
    DELETE FROM CourseAssessmentTypeCLOMappings WHERE ProgramCourseID IN (SELECT ProgramCourseID FROM @OldPCs);
    DELETE FROM CourseTopics WHERE ProgramCourseID IN (SELECT ProgramCourseID FROM @OldPCs);
    DELETE FROM CLOs WHERE ProgramCourseID IN (SELECT ProgramCourseID FROM @OldPCs);
    DELETE FROM ProgramCourseContentSubmissions WHERE ProgramCourseID IN (SELECT ProgramCourseID FROM @OldPCs);
    DELETE FROM TeacherPermissions WHERE ProgramCourseID IN (SELECT ProgramCourseID FROM @OldPCs);
    DELETE FROM ProgramCoursePLOMappings WHERE ProgramCourseID IN (SELECT ProgramCourseID FROM @OldPCs);
    DELETE FROM AcademicProgramCourses WHERE ProgramID = @OldProgramId;

    DELETE FROM PEOPLEMappings WHERE PEOID IN (SELECT PEOID FROM PEOs WHERE ProgramID = @OldProgramId);
    DELETE FROM PLOs WHERE ProgramID = @OldProgramId;
    DELETE FROM PEOs WHERE ProgramID = @OldProgramId;
    DELETE FROM AcademicSessions WHERE ProgramID = @OldProgramId;
    DELETE FROM AcademicPrograms WHERE ProgramID = @OldProgramId;
END

PRINT 'Creating fully completed BSCS3 test program...';

INSERT INTO AcademicPrograms (ProgramCode, ProgramName)
VALUES (N'BSCS3', N'Computer Science 2');
DECLARE @ProgramId INT = SCOPE_IDENTITY();

DECLARE @C1 INT, @C2 INT, @C3 INT, @C4 INT;
IF NOT EXISTS (SELECT 1 FROM Courses WHERE CourseCode = N'TST-301')
    INSERT INTO Courses (CourseCode, CourseTitle, ClassCreditHours, LabCreditHours)
    VALUES (N'TST-301', N'Programming Fundamentals', 3, 0);
SET @C1 = (SELECT CourseID FROM Courses WHERE CourseCode = N'TST-301');

IF NOT EXISTS (SELECT 1 FROM Courses WHERE CourseCode = N'TST-302')
    INSERT INTO Courses (CourseCode, CourseTitle, ClassCreditHours, LabCreditHours)
    VALUES (N'TST-302', N'Object Oriented Programming', 3, 1);
SET @C2 = (SELECT CourseID FROM Courses WHERE CourseCode = N'TST-302');

IF NOT EXISTS (SELECT 1 FROM Courses WHERE CourseCode = N'TST-303')
    INSERT INTO Courses (CourseCode, CourseTitle, ClassCreditHours, LabCreditHours)
    VALUES (N'TST-303', N'Data Structures', 3, 1);
SET @C3 = (SELECT CourseID FROM Courses WHERE CourseCode = N'TST-303');

IF NOT EXISTS (SELECT 1 FROM Courses WHERE CourseCode = N'TST-304')
    INSERT INTO Courses (CourseCode, CourseTitle, ClassCreditHours, LabCreditHours)
    VALUES (N'TST-304', N'Database Systems', 3, 0);
SET @C4 = (SELECT CourseID FROM Courses WHERE CourseCode = N'TST-304');

INSERT INTO AcademicProgramCourses (ProgramID, CourseID)
VALUES (@ProgramId, @C1), (@ProgramId, @C2), (@ProgramId, @C3), (@ProgramId, @C4);

DECLARE @Pc1 INT = (SELECT ProgramCourseID FROM AcademicProgramCourses WHERE ProgramID=@ProgramId AND CourseID=@C1);
DECLARE @Pc2 INT = (SELECT ProgramCourseID FROM AcademicProgramCourses WHERE ProgramID=@ProgramId AND CourseID=@C2);
DECLARE @Pc3 INT = (SELECT ProgramCourseID FROM AcademicProgramCourses WHERE ProgramID=@ProgramId AND CourseID=@C3);
DECLARE @Pc4 INT = (SELECT ProgramCourseID FROM AcademicProgramCourses WHERE ProgramID=@ProgramId AND CourseID=@C4);

INSERT INTO PEOs (ProgramID, PEOCode, PEODescription) VALUES
(@ProgramId, N'PEO1', N'Test PEO1: competent computing professionals.'),
(@ProgramId, N'PEO2', N'Test PEO2: ethical and lifelong learners.');
DECLARE @Peo1 INT = (SELECT PEOID FROM PEOs WHERE ProgramID=@ProgramId AND PEOCode=N'PEO1');
DECLARE @Peo2 INT = (SELECT PEOID FROM PEOs WHERE ProgramID=@ProgramId AND PEOCode=N'PEO2');

INSERT INTO PLOs (ProgramID, PLOCode, PLODescription) VALUES
(@ProgramId, N'PLO1', N'Test PLO1: apply computing knowledge.'),
(@ProgramId, N'PLO2', N'Test PLO2: analyze and solve problems.'),
(@ProgramId, N'PLO3', N'Test PLO3: communicate and work in teams.'),
(@ProgramId, N'PLO4', N'Test PLO4: professional ethics.');
DECLARE @Plo1 INT = (SELECT PLOID FROM PLOs WHERE ProgramID=@ProgramId AND PLOCode=N'PLO1');
DECLARE @Plo2 INT = (SELECT PLOID FROM PLOs WHERE ProgramID=@ProgramId AND PLOCode=N'PLO2');
DECLARE @Plo3 INT = (SELECT PLOID FROM PLOs WHERE ProgramID=@ProgramId AND PLOCode=N'PLO3');
DECLARE @Plo4 INT = (SELECT PLOID FROM PLOs WHERE ProgramID=@ProgramId AND PLOCode=N'PLO4');

INSERT INTO PEOPLEMappings (PEOID, PLOID, MappedByUserID, IsMapped) VALUES
(@Peo1, @Plo1, @HodId, 1), (@Peo1, @Plo2, @HodId, 1),
(@Peo2, @Plo3, @HodId, 1), (@Peo2, @Plo4, @HodId, 1);

INSERT INTO ProgramCoursePLOMappings (ProgramCourseID, PLOID, Percentage, MappedByUserID) VALUES
(@Pc1, @Plo1, 40, @HodId), (@Pc1, @Plo2, 30, @HodId), (@Pc1, @Plo3, 20, @HodId), (@Pc1, @Plo4, 10, @HodId),
(@Pc2, @Plo1, 25, @HodId), (@Pc2, @Plo2, 35, @HodId), (@Pc2, @Plo3, 25, @HodId), (@Pc2, @Plo4, 15, @HodId),
(@Pc3, @Plo1, 20, @HodId), (@Pc3, @Plo2, 40, @HodId), (@Pc3, @Plo3, 25, @HodId), (@Pc3, @Plo4, 15, @HodId),
(@Pc4, @Plo1, 15, @HodId), (@Pc4, @Plo2, 25, @HodId), (@Pc4, @Plo3, 30, @HodId), (@Pc4, @Plo4, 30, @HodId);

-- Active session (StartDate in the past so it shows as current)
INSERT INTO AcademicSessions (ProgramID, SessionTitle, AcademicYear, StartDate)
VALUES (@ProgramId, N'Fall 2026', N'2026-2027', CAST(N'2026-02-01' AS DATE));
DECLARE @SessionId INT = SCOPE_IDENTITY();

INSERT INTO Sections (SessionID, SectionName, Semester) VALUES
(@SessionId, N'BSCS3-1A', 1),
(@SessionId, N'BSCS3-1B', 1),
(@SessionId, N'BSCS3-1C', 1);
DECLARE @SecA INT = (SELECT SectionID FROM Sections WHERE SessionID=@SessionId AND SectionName=N'BSCS3-1A');
DECLARE @SecB INT = (SELECT SectionID FROM Sections WHERE SessionID=@SessionId AND SectionName=N'BSCS3-1B');
DECLARE @SecC INT = (SELECT SectionID FROM Sections WHERE SessionID=@SessionId AND SectionName=N'BSCS3-1C');

INSERT INTO Students (SectionID, FullName, AridNo, Email) VALUES
(@SecA, N'Ali Test', N'BSCS3-F26-001', N'ali.bscs3@test.biit.edu.pk'),
(@SecA, N'Basit Test', N'BSCS3-F26-002', N'basit.bscs3@test.biit.edu.pk'),
(@SecA, N'Carla Test', N'BSCS3-F26-003', N'carla.bscs3@test.biit.edu.pk'),
(@SecA, N'Daniyal Test', N'BSCS3-F26-004', N'daniyal.bscs3@test.biit.edu.pk'),
(@SecA, N'Eman Test', N'BSCS3-F26-005', N'eman.bscs3@test.biit.edu.pk'),
(@SecB, N'Farah Test', N'BSCS3-F26-006', N'farah.bscs3@test.biit.edu.pk'),
(@SecB, N'Gul Test', N'BSCS3-F26-007', N'gul.bscs3@test.biit.edu.pk'),
(@SecB, N'Hina Test', N'BSCS3-F26-008', N'hina.bscs3@test.biit.edu.pk'),
(@SecB, N'Imran Test', N'BSCS3-F26-009', N'imran.bscs3@test.biit.edu.pk'),
(@SecB, N'Javeria Test', N'BSCS3-F26-010', N'javeria.bscs3@test.biit.edu.pk'),
(@SecC, N'Kamran Test', N'BSCS3-F26-011', N'kamran.bscs3@test.biit.edu.pk'),
(@SecC, N'Laraib Test', N'BSCS3-F26-012', N'laraib.bscs3@test.biit.edu.pk'),
(@SecC, N'Mahnoor Test', N'BSCS3-F26-013', N'mahnoor.bscs3@test.biit.edu.pk'),
(@SecC, N'Naveed Test', N'BSCS3-F26-014', N'naveed.bscs3@test.biit.edu.pk'),
(@SecC, N'Omar Test', N'BSCS3-F26-015', N'omar.bscs3@test.biit.edu.pk');

/* ---- Approved CLOs / weekly / CLO-PLO / CLO-Task for ALL 4 courses ---- */
DECLARE @Pc INT, @CourseTitle NVARCHAR(200), @HasLab BIT, @TeacherForContent INT = @T1;
DECLARE @Clo1 INT, @Clo2 INT, @Clo3 INT, @Clo4 INT, @SubClos INT;

DECLARE pc_cur CURSOR LOCAL FAST_FORWARD FOR
    SELECT pc.ProgramCourseID, c.CourseTitle,
           CASE WHEN ISNULL(c.LabCreditHours,0) > 0 THEN 1 ELSE 0 END
    FROM AcademicProgramCourses pc
    JOIN Courses c ON c.CourseID = pc.CourseID
    WHERE pc.ProgramID = @ProgramId
    ORDER BY c.CourseCode;

OPEN pc_cur;
FETCH NEXT FROM pc_cur INTO @Pc, @CourseTitle, @HasLab;
WHILE @@FETCH_STATUS = 0
BEGIN
    INSERT INTO CLOs (ProgramCourseID, SessionID, CLOCode, CLODescription, ContentStatus) VALUES
    (@Pc, @SessionId, N'CLO1', CONCAT(N'[', @CourseTitle, N'] Explain core concepts.'), N'Approved'),
    (@Pc, @SessionId, N'CLO2', CONCAT(N'[', @CourseTitle, N'] Apply techniques to problems.'), N'Approved'),
    (@Pc, @SessionId, N'CLO3', CONCAT(N'[', @CourseTitle, N'] Analyze and debug solutions.'), N'Approved'),
    (@Pc, @SessionId, N'CLO4', CONCAT(N'[', @CourseTitle, N'] Design modular solutions.'), N'Approved');

    SET @Clo1 = (SELECT CLOID FROM CLOs WHERE ProgramCourseID=@Pc AND SessionID=@SessionId AND CLOCode=N'CLO1');
    SET @Clo2 = (SELECT CLOID FROM CLOs WHERE ProgramCourseID=@Pc AND SessionID=@SessionId AND CLOCode=N'CLO2');
    SET @Clo3 = (SELECT CLOID FROM CLOs WHERE ProgramCourseID=@Pc AND SessionID=@SessionId AND CLOCode=N'CLO3');
    SET @Clo4 = (SELECT CLOID FROM CLOs WHERE ProgramCourseID=@Pc AND SessionID=@SessionId AND CLOCode=N'CLO4');

    INSERT INTO ProgramCourseContentSubmissions (ProgramCourseID, SessionID, ContentTypeID, SubmittedByUserID, SubmissionStatus, SubmittedAt, ReviewedByUserID, ReviewedAt, IsCurrentApproved)
    VALUES (@Pc, @SessionId, @CtClos, @TeacherForContent, N'Approved', DATEADD(DAY,-14,GETUTCDATE()), @HodId, DATEADD(DAY,-13,GETUTCDATE()), 1);
    SET @SubClos = SCOPE_IDENTITY();
    UPDATE CLOs SET SubmissionID=@SubClos WHERE ProgramCourseID=@Pc AND SessionID=@SessionId;

    INSERT INTO CourseTopics (ProgramCourseID, SessionID, WeekNo, TopicTitle, TopicDescription, DisplayOrder, ContentStatus, ParentTopicID) VALUES
    (@Pc, @SessionId, 1, N'Week 1 Intro', N'TST', 1, N'Approved', NULL),
    (@Pc, @SessionId, 2, N'Week 2 Core', N'TST', 2, N'Approved', NULL),
    (@Pc, @SessionId, 3, N'Week 3 Practice', N'TST', 3, N'Approved', NULL),
    (@Pc, @SessionId, 4, N'Week 4 Advanced', N'TST', 4, N'Approved', NULL);

    INSERT INTO ProgramCourseContentSubmissions (ProgramCourseID, SessionID, ContentTypeID, SubmittedByUserID, SubmissionStatus, SubmittedAt, ReviewedByUserID, ReviewedAt, IsCurrentApproved)
    VALUES (@Pc, @SessionId, @CtWeekly, @TeacherForContent, N'Approved', DATEADD(DAY,-12,GETUTCDATE()), @HodId, DATEADD(DAY,-11,GETUTCDATE()), 1);

    INSERT INTO PLOCLOMappings (ProgramCourseID, SessionID, CLOID, PLOID, Percentage, UpdatedByUserID, ContentStatus) VALUES
    (@Pc, @SessionId, @Clo1, @Plo1, 40, @TeacherForContent, N'Approved'),
    (@Pc, @SessionId, @Clo2, @Plo1, 30, @TeacherForContent, N'Approved'),
    (@Pc, @SessionId, @Clo3, @Plo1, 20, @TeacherForContent, N'Approved'),
    (@Pc, @SessionId, @Clo4, @Plo1, 10, @TeacherForContent, N'Approved'),
    (@Pc, @SessionId, @Clo1, @Plo2, 20, @TeacherForContent, N'Approved'),
    (@Pc, @SessionId, @Clo2, @Plo2, 40, @TeacherForContent, N'Approved'),
    (@Pc, @SessionId, @Clo3, @Plo2, 25, @TeacherForContent, N'Approved'),
    (@Pc, @SessionId, @Clo4, @Plo2, 15, @TeacherForContent, N'Approved'),
    (@Pc, @SessionId, @Clo2, @Plo3, 30, @TeacherForContent, N'Approved'),
    (@Pc, @SessionId, @Clo3, @Plo3, 40, @TeacherForContent, N'Approved'),
    (@Pc, @SessionId, @Clo4, @Plo3, 30, @TeacherForContent, N'Approved'),
    (@Pc, @SessionId, @Clo3, @Plo4, 50, @TeacherForContent, N'Approved'),
    (@Pc, @SessionId, @Clo4, @Plo4, 50, @TeacherForContent, N'Approved');

    INSERT INTO ProgramCourseContentSubmissions (ProgramCourseID, SessionID, ContentTypeID, SubmittedByUserID, SubmissionStatus, SubmittedAt, ReviewedByUserID, ReviewedAt, IsCurrentApproved)
    VALUES (@Pc, @SessionId, @CtCloPlo, @TeacherForContent, N'Approved', DATEADD(DAY,-10,GETUTCDATE()), @HodId, DATEADD(DAY,-9,GETUTCDATE()), 1);

    INSERT INTO CourseAssessmentTypeCLOMappings (ProgramCourseID, SessionID, AssessmentTypeID, CLOID, Percentage, UpdatedByUserID, ContentStatus) VALUES
    (@Pc, @SessionId, @TypeQuiz, @Clo1, 30, @TeacherForContent, N'Approved'),
    (@Pc, @SessionId, @TypeAssign, @Clo1, 30, @TeacherForContent, N'Approved'),
    (@Pc, @SessionId, @TypeMid, @Clo1, 20, @TeacherForContent, N'Approved'),
    (@Pc, @SessionId, @TypeFinal, @Clo1, 20, @TeacherForContent, N'Approved'),
    (@Pc, @SessionId, @TypeQuiz, @Clo2, 25, @TeacherForContent, N'Approved'),
    (@Pc, @SessionId, @TypeAssign, @Clo2, 25, @TeacherForContent, N'Approved'),
    (@Pc, @SessionId, @TypeMid, @Clo2, 25, @TeacherForContent, N'Approved'),
    (@Pc, @SessionId, @TypeFinal, @Clo2, 25, @TeacherForContent, N'Approved'),
    (@Pc, @SessionId, @TypeQuiz, @Clo3, 20, @TeacherForContent, N'Approved'),
    (@Pc, @SessionId, @TypeAssign, @Clo3, 20, @TeacherForContent, N'Approved'),
    (@Pc, @SessionId, @TypeMid, @Clo3, 30, @TeacherForContent, N'Approved'),
    (@Pc, @SessionId, @TypeFinal, @Clo3, 30, @TeacherForContent, N'Approved'),
    (@Pc, @SessionId, @TypeQuiz, @Clo4, 15, @TeacherForContent, N'Approved'),
    (@Pc, @SessionId, @TypeAssign, @Clo4, 25, @TeacherForContent, N'Approved'),
    (@Pc, @SessionId, @TypeMid, @Clo4, 30, @TeacherForContent, N'Approved'),
    (@Pc, @SessionId, @TypeFinal, @Clo4, 30, @TeacherForContent, N'Approved');

    INSERT INTO ProgramCourseContentSubmissions (ProgramCourseID, SessionID, ContentTypeID, SubmittedByUserID, SubmissionStatus, SubmittedAt, ReviewedByUserID, ReviewedAt, IsCurrentApproved)
    VALUES (@Pc, @SessionId, @CtCloTask, @TeacherForContent, N'Approved', DATEADD(DAY,-8,GETUTCDATE()), @HodId, DATEADD(DAY,-7,GETUTCDATE()), 1);

    FETCH NEXT FROM pc_cur INTO @Pc, @CourseTitle, @HasLab;
END
CLOSE pc_cur;
DEALLOCATE pc_cur;

/* ---- Offerings: 4 courses x 3 sections, fully filled ---- */
DECLARE @Oc TABLE (
    OfferedCourseID INT,
    SectionID INT,
    CourseID INT,
    TeacherUserID INT,
    ProgramCourseID INT,
    CourseTitle NVARCHAR(200),
    SectionName NVARCHAR(50),
    HasLab BIT,
    ClassCH INT,
    LabCH INT
);

DECLARE @Sec INT, @Course INT, @Teacher INT, @NewOc INT, @SecName NVARCHAR(50), @CTitle NVARCHAR(200), @ClassCH INT, @LabCH INT;

DECLARE offer_cur CURSOR LOCAL FAST_FORWARD FOR
    SELECT s.SectionID, s.SectionName, c.CourseID, c.CourseTitle, c.ClassCreditHours, c.LabCreditHours,
           t.TeacherUserID, pc.ProgramCourseID
    FROM (VALUES (@SecA,@T1), (@SecB,@T2), (@SecC,@T3)) t(SectionID, TeacherUserID)
    CROSS JOIN (VALUES (@C1,@Pc1), (@C2,@Pc2), (@C3,@Pc3), (@C4,@Pc4)) x(CourseID, ProgramCourseID)
    JOIN Sections s ON s.SectionID = t.SectionID
    JOIN Courses c ON c.CourseID = x.CourseID
    JOIN AcademicProgramCourses pc ON pc.ProgramCourseID = x.ProgramCourseID;

OPEN offer_cur;
FETCH NEXT FROM offer_cur INTO @Sec, @SecName, @Course, @CTitle, @ClassCH, @LabCH, @Teacher, @Pc;
WHILE @@FETCH_STATUS = 0
BEGIN
    INSERT INTO OfferedCourses (SectionID, CourseID, TeacherUserID)
    VALUES (@Sec, @Course, @Teacher);
    SET @NewOc = SCOPE_IDENTITY();

    INSERT INTO @Oc (OfferedCourseID, SectionID, CourseID, TeacherUserID, ProgramCourseID, CourseTitle, SectionName, HasLab, ClassCH, LabCH)
    VALUES (@NewOc, @Sec, @Course, @Teacher, @Pc, @CTitle, @SecName,
            CASE WHEN ISNULL(@LabCH,0)>0 THEN 1 ELSE 0 END, @ClassCH, @LabCH);

    INSERT INTO StudentOfferedCourses (StudentID, OfferedCourseID)
    SELECT StudentID, @NewOc FROM Students WHERE SectionID = @Sec;

    IF NOT EXISTS (SELECT 1 FROM TeacherPermissions WHERE TeacherUserID=@Teacher AND ProgramCourseID=@Pc AND PermissionTypeID=@PermClos)
        INSERT INTO TeacherPermissions (TeacherUserID, ProgramCourseID, PermissionTypeID, HODUserID, IsAllowed)
        VALUES
        (@Teacher, @Pc, @PermClos, @HodId, 1),
        (@Teacher, @Pc, @PermWeekly, @HodId, 1),
        (@Teacher, @Pc, @PermMid, @HodId, 1),
        (@Teacher, @Pc, @PermFinal, @HodId, 1),
        (@Teacher, @Pc, @PermCloPlo, @HodId, 1),
        (@Teacher, @Pc, @PermCloTask, @HodId, 1);

    FETCH NEXT FROM offer_cur INTO @Sec, @SecName, @Course, @CTitle, @ClassCH, @LabCH, @Teacher, @Pc;
END
CLOSE offer_cur;
DEALLOCATE offer_cur;

/* ---- Per offering: assessments, question+solid marks, attendance, schemes, submitted folder ---- */
DECLARE @OcId INT, @Aid INT, @AType INT, @ANo INT, @Qid INT, @CloPick INT, @n INT, @i INT;
DECLARE @TotalQ DECIMAL(10,2), @Score DECIMAL(10,2);
DECLARE @HwTotal DECIMAL(10,2), @MidTotal DECIMAL(10,2), @LabTotal DECIMAL(10,2), @FinalTotal DECIMAL(10,2), @GrandTotal DECIMAL(10,2);
DECLARE @RootName NVARCHAR(200), @RootPath NVARCHAR(400), @FolderId INT;
DECLARE @Tid INT, @TCode NVARCHAR(10), @TName NVARCHAR(200), @DirId INT, @DirName NVARCHAR(300), @DirPath NVARCHAR(500);
DECLARE @DayN INT;
DECLARE @Cat NVARCHAR(100);

DECLARE oc_cur CURSOR LOCAL FAST_FORWARD FOR
    SELECT OfferedCourseID, SectionID, TeacherUserID, ProgramCourseID, CourseTitle, SectionName, HasLab, ClassCH, LabCH
    FROM @Oc
    ORDER BY OfferedCourseID;
OPEN oc_cur;
FETCH NEXT FROM oc_cur INTO @OcId, @Sec, @Teacher, @Pc, @CTitle, @SecName, @HasLab, @ClassCH, @LabCH;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @Clo1 = (SELECT CLOID FROM CLOs WHERE ProgramCourseID=@Pc AND SessionID=@SessionId AND CLOCode=N'CLO1');
    SET @Clo2 = (SELECT CLOID FROM CLOs WHERE ProgramCourseID=@Pc AND SessionID=@SessionId AND CLOCode=N'CLO2');
    SET @Clo3 = (SELECT CLOID FROM CLOs WHERE ProgramCourseID=@Pc AND SessionID=@SessionId AND CLOCode=N'CLO3');
    SET @Clo4 = (SELECT CLOID FROM CLOs WHERE ProgramCourseID=@Pc AND SessionID=@SessionId AND CLOCode=N'CLO4');

    -- Assessments
    SET @n = 1;
    WHILE @n <= 8
    BEGIN
        INSERT INTO Assessments (OfferedCourseID, AssessmentTypeID, AssessmentNo, Title, DueDate, TotalMarks, CreatedByUserID, CreatedAt, IsSetupComplete, SetupCompletedAt)
        VALUES (@OcId, @TypeQuiz, @n, CONCAT(N'Quiz ', @n), DATEADD(DAY, -40+@n, CAST(GETDATE() AS DATE)), 10, @Teacher, GETUTCDATE(), 1, GETUTCDATE());
        SET @n += 1;
    END
    SET @n = 1;
    WHILE @n <= 4
    BEGIN
        INSERT INTO Assessments (OfferedCourseID, AssessmentTypeID, AssessmentNo, Title, DueDate, TotalMarks, CreatedByUserID, CreatedAt, IsSetupComplete, SetupCompletedAt)
        VALUES (@OcId, @TypeAssign, @n, CONCAT(N'Assignment ', @n), DATEADD(DAY, -20+@n, CAST(GETDATE() AS DATE)), 20, @Teacher, GETUTCDATE(), 1, GETUTCDATE());
        SET @n += 1;
    END
    INSERT INTO Assessments (OfferedCourseID, AssessmentTypeID, AssessmentNo, Title, DueDate, TotalMarks, CreatedByUserID, CreatedAt, IsSetupComplete, SetupCompletedAt)
    VALUES
    (@OcId, @TypeMid, 1, N'Mid Term Exam', DATEADD(DAY, -5, CAST(GETDATE() AS DATE)), 50, @Teacher, GETUTCDATE(), 1, GETUTCDATE()),
    (@OcId, @TypeFinal, 1, N'Final Term Exam', DATEADD(DAY, 5, CAST(GETDATE() AS DATE)), 100, @Teacher, GETUTCDATE(), 1, GETUTCDATE());

    IF @HasLab = 1
    BEGIN
        INSERT INTO Assessments (OfferedCourseID, AssessmentTypeID, AssessmentNo, Title, DueDate, TotalMarks, CreatedByUserID, CreatedAt, IsSetupComplete, SetupCompletedAt)
        VALUES (@OcId, @TypeFinal, 2, N'Lab Final Exam', DATEADD(DAY, 6, CAST(GETDATE() AS DATE)), 50, @Teacher, GETUTCDATE(), 1, GETUTCDATE());
    END

    -- Questions + CLO map + question marks + solid marks
    DECLARE acur CURSOR LOCAL FAST_FORWARD FOR
        SELECT AssessmentID, AssessmentTypeID, AssessmentNo, TotalMarks
        FROM Assessments WHERE OfferedCourseID=@OcId;
    OPEN acur;
    FETCH NEXT FROM acur INTO @Aid, @AType, @ANo, @TotalQ;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @CloPick = CASE
            WHEN @ANo % 4 = 1 THEN @Clo1
            WHEN @ANo % 4 = 2 THEN @Clo2
            WHEN @ANo % 4 = 3 THEN @Clo3
            ELSE @Clo4 END;

        INSERT INTO AssessmentQuestions (AssessmentID, QuestionNo, QuestionText, Marks)
        VALUES (@Aid, N'Q1', CONCAT(N'TST Q for assessment ', @ANo), @TotalQ);
        SET @Qid = SCOPE_IDENTITY();
        INSERT INTO AssessmentQuestionCLOMappings (QuestionID, CLOID, Percentage)
        VALUES (@Qid, @CloPick, 100);

        INSERT INTO StudentQuestionMarks (QuestionID, StudentID, ObtainedMarks)
        SELECT @Qid, s.StudentID,
            ROUND(@TotalQ * (0.55 + 0.08 * ROW_NUMBER() OVER (ORDER BY s.StudentID)), 2)
        FROM Students s WHERE s.SectionID = @Sec;

        -- Cap at TotalMarks (row 5 can exceed)
        UPDATE sqm
        SET ObtainedMarks = CASE WHEN sqm.ObtainedMarks > @TotalQ THEN @TotalQ ELSE sqm.ObtainedMarks END
        FROM StudentQuestionMarks sqm
        WHERE sqm.QuestionID = @Qid;

        INSERT INTO StudentAssessmentMarks (AssessmentID, StudentID, ObtainedMarks)
        SELECT @Aid, sqm.StudentID, sqm.ObtainedMarks
        FROM StudentQuestionMarks sqm
        WHERE sqm.QuestionID = @Qid;

        FETCH NEXT FROM acur INTO @Aid, @AType, @ANo, @TotalQ;
    END
    CLOSE acur;
    DEALLOCATE acur;

    -- Attendance (~80% present pattern)
    SET @DayN = 1;
    WHILE @DayN <= 10
    BEGIN
        INSERT INTO AttendanceRecords (OfferedCourseID, StudentID, AttendanceDate, Status)
        SELECT @OcId, s.StudentID, DATEADD(DAY, -20 + @DayN, CAST(GETDATE() AS DATE)),
            CASE WHEN ((s.StudentID + @DayN) % 5) = 0 THEN N'Absent' ELSE N'Present' END
        FROM Students s WHERE s.SectionID = @Sec;
        SET @DayN += 1;
    END

    -- Mark schemes
    IF @HasLab = 1 AND (@ClassCH + @LabCH) >= 4
    BEGIN
        SET @HwTotal = 12; SET @MidTotal = 18; SET @LabTotal = 20; SET @FinalTotal = 30; SET @GrandTotal = 80;
    END
    ELSE IF @HasLab = 1
    BEGIN
        SET @HwTotal = 8; SET @MidTotal = 12; SET @LabTotal = 20; SET @FinalTotal = 20; SET @GrandTotal = 60;
    END
    ELSE
    BEGIN
        SET @HwTotal = 12; SET @MidTotal = 18; SET @LabTotal = 0; SET @FinalTotal = 30; SET @GrandTotal = 60;
    END

    INSERT INTO OfferedCourseHomeworkMarkSchemes (OfferedCourseID, AttendanceMarks, QuizMarks, AssignmentMarks, UpdatedByUserID, UpdatedAt)
    VALUES (@OcId,
        CASE WHEN @HwTotal = 12 THEN 2 ELSE 1 END,
        CASE WHEN @HwTotal = 12 THEN 5 ELSE 3 END,
        CASE WHEN @HwTotal = 12 THEN 5 ELSE 4 END,
        @Teacher, GETUTCDATE());

    IF @HasLab = 1
    BEGIN
        INSERT INTO OfferedCourseLabMarkSchemes
            (OfferedCourseID, QuizMarks, AssignmentMarks, LabTaskMarks, ProjectMarks, AttendanceMarks, ExamMarks, UpdatedByUserID, UpdatedAt)
        VALUES (@OcId, 3, 3, 2, 2, 2, 8, @Teacher, GETUTCDATE());
    END

    INSERT INTO OfferedCourseResultSchemes
        (OfferedCourseID, TotalMarks, QuizMarks, HomeworkMarks, MidMarks, LabMarks, FinalTheoryMarks, FinalLabMarks,
         AssignmentBestOf, QuizBestOf, AttendanceHomeworkMarks, UpdatedAt, UpdatedByUserID)
    VALUES
        (@OcId, @GrandTotal, 0, @HwTotal, @MidTotal, @LabTotal, @FinalTotal, 0, 3, 6,
         CASE WHEN @HwTotal = 12 THEN 2 ELSE 1 END, GETUTCDATE(), @Teacher);

    -- Submitted folder with template dirs + files
    SET @RootName = CONCAT(N'OC-', @OcId, N'-', REPLACE(@CTitle, N'/', N'-'), N'-', @SecName);
    SET @RootPath = CONCAT(N'course-folders/', @RootName);

    INSERT INTO CourseFolders (OfferedCourseID, RootFolderName, RootFolderPath, CreatedByUserID, FolderStatus, CreatedAt)
    VALUES (@OcId, @RootName, @RootPath, @Teacher, N'Submitted', GETUTCDATE());
    SET @FolderId = SCOPE_IDENTITY();

    INSERT INTO FolderSubmissions (CourseFolderID, SubmittedByUserID, SubmissionStatus, SubmittedAt, VersionNo)
    VALUES (@FolderId, @Teacher, N'Submitted', GETUTCDATE(), 1);

    DECLARE tcur CURSOR LOCAL FAST_FORWARD FOR
        SELECT TemplateItemID, TemplateCode, FolderItemName FROM CourseFolderTemplateItems ORDER BY SortOrder;
    OPEN tcur;
    FETCH NEXT FROM tcur INTO @Tid, @TCode, @TName;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @DirName = CONCAT(@TCode, N'-', @TName);
        SET @DirPath = CONCAT(@RootPath, N'/', @DirName);
        INSERT INTO CourseFolderDirectories (CourseFolderID, TemplateItemID, DirectoryName, DirectoryPath)
        VALUES (@FolderId, @Tid, @DirName, @DirPath);
        SET @DirId = SCOPE_IDENTITY();

        IF @TCode IN (N'a', N'b', N'c', N'd', N'e', N'f', N'j', N'k', N'l', N'm', N'n', N'o')
        BEGIN
            SET @Cat =
                CASE @TCode
                    WHEN N'a' THEN N'Course Objective'
                    WHEN N'b' THEN N'CLO-Task Mapping'
                    WHEN N'c' THEN N'Course Content'
                    WHEN N'd' THEN N'Weekly Plan'
                    WHEN N'e' THEN N'Attendance Record'
                    WHEN N'f' THEN N'Reference Material'
                    WHEN N'j' THEN N'Marks Distribution and Grading'
                    WHEN N'k' THEN N'Result'
                    WHEN N'l' THEN N'Outcomes Assessment'
                    WHEN N'm' THEN N'Technology Involved'
                    WHEN N'n' THEN N'Design Techniques Practiced'
                    WHEN N'o' THEN N'Complete Analysis of Course'
                END;
            INSERT INTO CourseFolderFiles (DirectoryID, UploadedByUserID, FileName, FilePath, FileCategory, Remarks, UploadedAt)
            VALUES (@DirId, @Teacher, CONCAT(@TCode, N'-complete.pdf'), CONCAT(@DirPath, N'/', @TCode, N'-complete.pdf'),
                    @Cat, N'Full test seed', GETUTCDATE());
        END
        IF @TCode = N'g'
        BEGIN
            SET @i = 1;
            WHILE @i <= 4 BEGIN
                INSERT INTO CourseFolderFiles (DirectoryID, UploadedByUserID, FileName, FilePath, FileCategory, Remarks, UploadedAt)
                VALUES (@DirId, @Teacher, CONCAT(N'AssignQ', @i, N'.pdf'), CONCAT(@DirPath, N'/AssignQ', @i, N'.pdf'), N'Assignment Question', N'TST', GETUTCDATE());
                SET @i += 1;
            END
            SET @i = 1;
            WHILE @i <= 8 BEGIN
                INSERT INTO CourseFolderFiles (DirectoryID, UploadedByUserID, FileName, FilePath, FileCategory, Remarks, UploadedAt)
                VALUES (@DirId, @Teacher, CONCAT(N'QuizQ', @i, N'.pdf'), CONCAT(@DirPath, N'/QuizQ', @i, N'.pdf'), N'Quiz Question', N'TST', GETUTCDATE());
                SET @i += 1;
            END
            INSERT INTO CourseFolderFiles (DirectoryID, UploadedByUserID, FileName, FilePath, FileCategory, Remarks, UploadedAt)
            VALUES
            (@DirId, @Teacher, N'MidQ.pdf', CONCAT(@DirPath, N'/MidQ.pdf'), N'Mid Exam Question', N'TST', GETUTCDATE()),
            (@DirId, @Teacher, N'FinalQ.pdf', CONCAT(@DirPath, N'/FinalQ.pdf'), N'Final Exam Question', N'TST', GETUTCDATE());
            IF @HasLab = 1
                INSERT INTO CourseFolderFiles (DirectoryID, UploadedByUserID, FileName, FilePath, FileCategory, Remarks, UploadedAt)
                VALUES (@DirId, @Teacher, N'LabFinalQ.pdf', CONCAT(@DirPath, N'/LabFinalQ.pdf'), N'Final Exam Question', N'TST', GETUTCDATE());
        END
        IF @TCode = N'h'
        BEGIN
            SET @i = 1;
            WHILE @i <= 4 BEGIN
                INSERT INTO CourseFolderFiles (DirectoryID, UploadedByUserID, FileName, FilePath, FileCategory, Remarks, UploadedAt)
                VALUES (@DirId, @Teacher, CONCAT(N'AssignSol', @i, N'.pdf'), CONCAT(@DirPath, N'/AssignSol', @i, N'.pdf'), N'Assignment Solution', N'TST', GETUTCDATE());
                SET @i += 1;
            END
            SET @i = 1;
            WHILE @i <= 8 BEGIN
                INSERT INTO CourseFolderFiles (DirectoryID, UploadedByUserID, FileName, FilePath, FileCategory, Remarks, UploadedAt)
                VALUES (@DirId, @Teacher, CONCAT(N'QuizSol', @i, N'.pdf'), CONCAT(@DirPath, N'/QuizSol', @i, N'.pdf'), N'Quiz Solution', N'TST', GETUTCDATE());
                SET @i += 1;
            END
            INSERT INTO CourseFolderFiles (DirectoryID, UploadedByUserID, FileName, FilePath, FileCategory, Remarks, UploadedAt)
            VALUES
            (@DirId, @Teacher, N'MidSol.pdf', CONCAT(@DirPath, N'/MidSol.pdf'), N'Mid Exam Solution', N'TST', GETUTCDATE()),
            (@DirId, @Teacher, N'FinalSol.pdf', CONCAT(@DirPath, N'/FinalSol.pdf'), N'Final Exam Solution', N'TST', GETUTCDATE());
        END
        IF @TCode = N'i'
        BEGIN
            INSERT INTO CourseFolderFiles (DirectoryID, UploadedByUserID, FileName, FilePath, FileCategory, Remarks, UploadedAt)
            VALUES
            (@DirId, @Teacher, N'A1-Best.pdf', CONCAT(@DirPath, N'/A1-Best.pdf'), N'Assignment Best Sample', N'TST', GETUTCDATE()),
            (@DirId, @Teacher, N'A1-Avg.pdf', CONCAT(@DirPath, N'/A1-Avg.pdf'), N'Assignment Average Sample', N'TST', GETUTCDATE()),
            (@DirId, @Teacher, N'A1-Worst.pdf', CONCAT(@DirPath, N'/A1-Worst.pdf'), N'Assignment Worst Sample', N'TST', GETUTCDATE());
        END

        FETCH NEXT FROM tcur INTO @Tid, @TCode, @TName;
    END
    CLOSE tcur;
    DEALLOCATE tcur;

    FETCH NEXT FROM oc_cur INTO @OcId, @Sec, @Teacher, @Pc, @CTitle, @SecName, @HasLab, @ClassCH, @LabCH;
END
CLOSE oc_cur;
DEALLOCATE oc_cur;

PRINT '===== BSCS3 FULL TEST PROGRAM READY =====';
PRINT CONCAT('ProgramID=', @ProgramId, ' SessionID=', @SessionId);
PRINT 'All 4 courses Approved (CLOs/weekly/CLO-PLO/CLO-Task).';
PRINT 'All 12 offerings: assessments, solid marks, attendance, schemes, Submitted folders.';
PRINT 'Run Docs/complete_bscs3_calculate.py next to write PDFs + calculate CLO/PLO + Course Results.';
PRINT 'HOD: munir@biit.edu.pk | Teachers: qasim(1A) saeed(1B) mustafa(1C) / Teacher@123';
GO
