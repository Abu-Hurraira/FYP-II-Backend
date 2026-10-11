USE FolderManagementDB;
GO

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

/* =========================================================
   SeedFullFeatureTestData.sql
   End-to-end test data for assessments, folders, and HOD review.

   CAI-261 BAI-4A (OC1 / aftab):
     - 8 Quiz + 4 Assignment + Mid + Final templates with questions
     - Solutions/samples/question PDFs registered in master folder
     - Folder status Submitted → HOD folder review queue

   CAI-261 BAI-4B (OC8 / shahid_jamil):
     - Partial Generated folder (teacher upload / incomplete progress)

   CAI-262 BAI-4A (PC115 / shafqat):
     - CLOs + mappings + weekly Submitted → HOD content review queue

   Re-runnable for demo-tagged rows / OC1+OC8 folders.
   ========================================================= */

DECLARE @OcA INT = 1;          -- CAI-261 BAI-4A
DECLARE @OcB INT = 8;          -- CAI-261 BAI-4B
DECLARE @OcMl INT = 2;         -- CAI-262 BAI-4A
DECLARE @TeacherA INT = 11;    -- aftab
DECLARE @TeacherB INT = 46;    -- shahid_jamil
DECLARE @TeacherMl INT = 44;   -- shafqat
DECLARE @HodId INT = 2;        -- munir
DECLARE @Pc261 INT = 114;
DECLARE @Pc262 INT = 115;
DECLARE @SessionId INT = 1;

DECLARE @TypeAssign INT = (SELECT AssessmentTypeID FROM AssessmentTypes WHERE AssessmentTypeName = N'Assignment');
DECLARE @TypeQuiz INT = (SELECT AssessmentTypeID FROM AssessmentTypes WHERE AssessmentTypeName = N'Quiz');
DECLARE @TypeMid INT = (SELECT AssessmentTypeID FROM AssessmentTypes WHERE AssessmentTypeName = N'Mid');
DECLARE @TypeFinal INT = (SELECT AssessmentTypeID FROM AssessmentTypes WHERE AssessmentTypeName = N'Final');

DECLARE @Clo1 INT = (SELECT TOP 1 CLOID FROM CLOs WHERE ProgramCourseID = @Pc261 AND SessionID = @SessionId AND CLOCode = N'CLO1');
DECLARE @Clo2 INT = (SELECT TOP 1 CLOID FROM CLOs WHERE ProgramCourseID = @Pc261 AND SessionID = @SessionId AND CLOCode = N'CLO2');
DECLARE @Clo3 INT = (SELECT TOP 1 CLOID FROM CLOs WHERE ProgramCourseID = @Pc261 AND SessionID = @SessionId AND CLOCode = N'CLO3');
DECLARE @Clo4 INT = (SELECT TOP 1 CLOID FROM CLOs WHERE ProgramCourseID = @Pc261 AND SessionID = @SessionId AND CLOCode = N'CLO4');
DECLARE @Clo5 INT = (SELECT TOP 1 CLOID FROM CLOs WHERE ProgramCourseID = @Pc261 AND SessionID = @SessionId AND CLOCode = N'CLO5');
DECLARE @Clo6 INT = (SELECT TOP 1 CLOID FROM CLOs WHERE ProgramCourseID = @Pc261 AND SessionID = @SessionId AND CLOCode = N'CLO6');

IF @Clo1 IS NULL OR @Clo2 IS NULL OR @Clo3 IS NULL
BEGIN
    RAISERROR(N'CAI-261 approved CLOs missing. Run SeedFullDemoData.sql first.', 16, 1);
    RETURN;
END

PRINT 'Ensuring required assessment templates for OC1...';

DECLARE @n INT;

-- Quizzes 1..8
SET @n = 1;
WHILE @n <= 8
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM Assessments
        WHERE OfferedCourseID = @OcA AND AssessmentTypeID = @TypeQuiz AND AssessmentNo = @n)
    BEGIN
        INSERT INTO Assessments (OfferedCourseID, AssessmentTypeID, AssessmentNo, Title, DueDate, TotalMarks, CreatedByUserID, CreatedAt)
        VALUES (@OcA, @TypeQuiz, @n, CONCAT(N'Quiz ', @n), DATEADD(DAY, -30 + @n, CAST(GETDATE() AS DATE)), 10, @TeacherA, GETUTCDATE());
    END
    ELSE
    BEGIN
        UPDATE Assessments
        SET Title = CONCAT(N'Quiz ', @n),
            TotalMarks = ISNULL(TotalMarks, 10)
        WHERE OfferedCourseID = @OcA AND AssessmentTypeID = @TypeQuiz AND AssessmentNo = @n
          AND Title LIKE N'Demo%';
    END
    SET @n += 1;
END

-- Assignments 1..4
SET @n = 1;
WHILE @n <= 4
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM Assessments
        WHERE OfferedCourseID = @OcA AND AssessmentTypeID = @TypeAssign AND AssessmentNo = @n)
    BEGIN
        INSERT INTO Assessments (OfferedCourseID, AssessmentTypeID, AssessmentNo, Title, DueDate, TotalMarks, CreatedByUserID, CreatedAt)
        VALUES (@OcA, @TypeAssign, @n, CONCAT(N'Assignment ', @n), DATEADD(DAY, -20 + @n, CAST(GETDATE() AS DATE)), 20, @TeacherA, GETUTCDATE());
    END
    ELSE
    BEGIN
        UPDATE Assessments
        SET Title = CONCAT(N'Assignment ', @n),
            TotalMarks = ISNULL(TotalMarks, 20)
        WHERE OfferedCourseID = @OcA AND AssessmentTypeID = @TypeAssign AND AssessmentNo = @n
          AND Title LIKE N'Demo%';
    END
    SET @n += 1;
END

IF NOT EXISTS (SELECT 1 FROM Assessments WHERE OfferedCourseID = @OcA AND AssessmentTypeID = @TypeMid AND AssessmentNo = 1)
BEGIN
    INSERT INTO Assessments (OfferedCourseID, AssessmentTypeID, AssessmentNo, Title, DueDate, TotalMarks, CreatedByUserID, CreatedAt)
    VALUES (@OcA, @TypeMid, 1, N'Mid Term Exam', DATEADD(DAY, -5, CAST(GETDATE() AS DATE)), 50, @TeacherA, GETUTCDATE());
END
ELSE
    UPDATE Assessments SET Title = N'Mid Term Exam', TotalMarks = ISNULL(TotalMarks, 50)
    WHERE OfferedCourseID = @OcA AND AssessmentTypeID = @TypeMid AND AssessmentNo = 1 AND Title LIKE N'Demo%';

IF NOT EXISTS (SELECT 1 FROM Assessments WHERE OfferedCourseID = @OcA AND AssessmentTypeID = @TypeFinal AND AssessmentNo = 1)
BEGIN
    INSERT INTO Assessments (OfferedCourseID, AssessmentTypeID, AssessmentNo, Title, DueDate, TotalMarks, CreatedByUserID, CreatedAt)
    VALUES (@OcA, @TypeFinal, 1, N'Final Term Exam', DATEADD(DAY, 14, CAST(GETDATE() AS DATE)), 100, @TeacherA, GETUTCDATE());
END
ELSE
    UPDATE Assessments SET Title = N'Final Term Exam', TotalMarks = ISNULL(TotalMarks, 100)
    WHERE OfferedCourseID = @OcA AND AssessmentTypeID = @TypeFinal AND AssessmentNo = 1 AND Title LIKE N'Demo%';

PRINT 'Adding questions to empty assessments...';

DECLARE @Aid INT, @AType INT, @ANo INT, @Q1 INT, @Q2 INT;

DECLARE asm_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT AssessmentID, AssessmentTypeID, AssessmentNo
    FROM Assessments
    WHERE OfferedCourseID = @OcA
      AND AssessmentTypeID IN (@TypeQuiz, @TypeAssign, @TypeMid, @TypeFinal)
      AND NOT EXISTS (SELECT 1 FROM AssessmentQuestions q WHERE q.AssessmentID = Assessments.AssessmentID);

OPEN asm_cursor;
FETCH NEXT FROM asm_cursor INTO @Aid, @AType, @ANo;
WHILE @@FETCH_STATUS = 0
BEGIN
    -- Marks omitted; CLO coverage % drives mapping (of type share).
    INSERT INTO AssessmentQuestions (AssessmentID, QuestionNo, QuestionText, Marks)
    VALUES
    (@Aid, N'Q1', CONCAT(N'Demo Q1 for assessment ', @ANo, N' — explain a core concept.'), NULL),
    (@Aid, N'Q2', CONCAT(N'Demo Q2 for assessment ', @ANo, N' — apply the concept to a short problem.'), NULL);

    SELECT @Q1 = QuestionID FROM AssessmentQuestions WHERE AssessmentID = @Aid AND QuestionNo = N'Q1';
    SELECT @Q2 = QuestionID FROM AssessmentQuestions WHERE AssessmentID = @Aid AND QuestionNo = N'Q2';

    -- Coverage % of each type bucket; keep per-CLO totals ≤ 100 across all assessments of that type.
    -- Demo already fills Quiz1/Assign1 with Clo1–Clo3; later slots prefer Clo4–Clo6 when present.
    IF @AType = @TypeQuiz
    BEGIN
        IF @ANo = 1 OR @Clo4 IS NULL
            INSERT INTO AssessmentQuestionCLOMappings (QuestionID, CLOID, Percentage)
            VALUES (@Q1, @Clo1, 50), (@Q2, @Clo1, 50);
        ELSE
            -- Quizzes 2–8: Clo4 (quiz-only) ~14% each + Clo2 remaining quiz share
            INSERT INTO AssessmentQuestionCLOMappings (QuestionID, CLOID, Percentage)
            VALUES (@Q1, @Clo4, 14), (@Q2, @Clo2, 12.5);
    END
    ELSE IF @AType = @TypeAssign
    BEGIN
        IF @ANo = 1 OR @Clo5 IS NULL
            INSERT INTO AssessmentQuestionCLOMappings (QuestionID, CLOID, Percentage)
            VALUES (@Q1, @Clo2, 60), (@Q2, @Clo3, 100);
        ELSE
            -- Assignments 2–4: Clo5 (assignment-only) + leftover Clo2 assign share
            INSERT INTO AssessmentQuestionCLOMappings (QuestionID, CLOID, Percentage)
            VALUES (@Q1, @Clo5, 33), (@Q2, @Clo2, 13);
    END
    ELSE IF @AType = @TypeMid
    BEGIN
        IF @Clo6 IS NOT NULL
            INSERT INTO AssessmentQuestionCLOMappings (QuestionID, CLOID, Percentage)
            VALUES (@Q1, @Clo1, 50), (@Q1, @Clo2, 40), (@Q2, @Clo2, 60), (@Q2, @Clo6, 100);
        ELSE
            INSERT INTO AssessmentQuestionCLOMappings (QuestionID, CLOID, Percentage)
            VALUES (@Q1, @Clo1, 50), (@Q1, @Clo2, 40), (@Q2, @Clo2, 60);
    END
    ELSE IF @AType = @TypeFinal
    BEGIN
        IF @Clo6 IS NOT NULL
            INSERT INTO AssessmentQuestionCLOMappings (QuestionID, CLOID, Percentage)
            VALUES (@Q1, @Clo2, 50), (@Q1, @Clo6, 50), (@Q2, @Clo3, 50), (@Q2, @Clo6, 50);
        ELSE
            INSERT INTO AssessmentQuestionCLOMappings (QuestionID, CLOID, Percentage)
            VALUES (@Q1, @Clo2, 50), (@Q2, @Clo3, 50);
    END

    FETCH NEXT FROM asm_cursor INTO @Aid, @AType, @ANo;
END
CLOSE asm_cursor;
DEALLOCATE asm_cursor;

PRINT 'Creating / resetting CourseFolders for OC1 and OC8...';

-- Clear prior file rows for these offerings (keep disk files from PS script)
DELETE FROM AssessmentFiles WHERE AssessmentID IN (SELECT AssessmentID FROM Assessments WHERE OfferedCourseID IN (@OcA, @OcB));

DELETE FROM FolderSubmissionIssues WHERE SubmissionID IN (
    SELECT fs.SubmissionID FROM FolderSubmissions fs
    JOIN CourseFolders cf ON cf.CourseFolderID = fs.CourseFolderID
    WHERE cf.OfferedCourseID IN (@OcA, @OcB));
DELETE FROM FolderSubmissions WHERE CourseFolderID IN (
    SELECT CourseFolderID FROM CourseFolders WHERE OfferedCourseID IN (@OcA, @OcB));
DELETE FROM CourseFolderFiles WHERE DirectoryID IN (
    SELECT d.DirectoryID FROM CourseFolderDirectories d
    JOIN CourseFolders cf ON cf.CourseFolderID = d.CourseFolderID
    WHERE cf.OfferedCourseID IN (@OcA, @OcB));
DELETE FROM CourseFolderDirectories WHERE CourseFolderID IN (
    SELECT CourseFolderID FROM CourseFolders WHERE OfferedCourseID IN (@OcA, @OcB));
DELETE FROM CourseFolders WHERE OfferedCourseID IN (@OcA, @OcB);

DECLARE @RootA NVARCHAR(400) = N'src/Folders/AftabProgrammingforAIBAI4A/MasterFolder';
DECLARE @RootB NVARCHAR(400) = N'src/Folders/ShahidJamilProgrammingforAIBAI4B/MasterFolder';

INSERT INTO CourseFolders (OfferedCourseID, RootFolderName, RootFolderPath, CreatedByUserID, FolderStatus)
VALUES (@OcA, N'AftabProgrammingforAIBAI4A', @RootA, @TeacherA, N'Submitted');
DECLARE @FolderA INT = SCOPE_IDENTITY();

INSERT INTO CourseFolders (OfferedCourseID, RootFolderName, RootFolderPath, CreatedByUserID, FolderStatus)
VALUES (@OcB, N'ShahidJamilProgrammingforAIBAI4B', @RootB, @TeacherB, N'Generated');
DECLARE @FolderB INT = SCOPE_IDENTITY();

-- Content submission row for folder (optional link)
DECLARE @CtFolder INT = (SELECT ContentTypeID FROM ContentSubmissionTypes WHERE ContentTypeCode = N'CourseFolder');
DELETE FROM ProgramCourseContentSubmissions
WHERE ProgramCourseID = @Pc261 AND SessionID = @SessionId AND ContentTypeID = @CtFolder
  AND SubmittedByUserID = @TeacherA AND SubmissionStatus IN (N'Submitted', N'Approved', N'Rejected');

INSERT INTO ProgramCourseContentSubmissions
    (ProgramCourseID, SessionID, ContentTypeID, SubmittedByUserID, SubmittedAt, SubmissionStatus,
     VersionNo, IsCurrentApproved)
VALUES (@Pc261, @SessionId, @CtFolder, @TeacherA, GETUTCDATE(), N'Submitted', 1, 0);
DECLARE @FolderContentSub INT = SCOPE_IDENTITY();

INSERT INTO FolderSubmissions
    (CourseFolderID, SubmittedByUserID, SubmissionStatus, SubmittedAt, Remarks, VersionNo, ContentSubmissionID)
VALUES
    (@FolderA, @TeacherA, N'Submitted', GETUTCDATE(), N'Demo complete folder for HOD review', 1, @FolderContentSub);

PRINT 'Inserting template directories + files for OC1...';

DECLARE @Templates TABLE (
    TemplateCode NVARCHAR(5) PRIMARY KEY,
    FolderItemName NVARCHAR(200),
    TemplateItemID INT
);
INSERT INTO @Templates (TemplateCode, FolderItemName, TemplateItemID)
SELECT TemplateCode, FolderItemName, TemplateItemID FROM CourseFolderTemplateItems;

DECLARE @code NVARCHAR(5), @itemName NVARCHAR(200), @tid INT, @dirId INT, @dirPath NVARCHAR(500), @dirName NVARCHAR(260);

DECLARE tcur CURSOR LOCAL FAST_FORWARD FOR SELECT TemplateCode, FolderItemName, TemplateItemID FROM @Templates;
OPEN tcur;
FETCH NEXT FROM tcur INTO @code, @itemName, @tid;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @dirName = CONCAT(@code, N'-', @itemName);
    SET @dirPath = CONCAT(@RootA, N'/', @dirName);
    INSERT INTO CourseFolderDirectories (CourseFolderID, ParentDirectoryID, TemplateItemID, DirectoryName, DirectoryPath)
    VALUES (@FolderA, NULL, @tid, @dirName, @dirPath);
    FETCH NEXT FROM tcur INTO @code, @itemName, @tid;
END
CLOSE tcur;
DEALLOCATE tcur;

-- Helper: insert file into directory by template code
DECLARE @Files TABLE (
    TemplateCode NVARCHAR(5),
    FileName NVARCHAR(260),
    FileCategory NVARCHAR(100),
    Purpose NVARCHAR(50) NULL,
    AssessmentNo INT NULL,
    AssessmentTypeID INT NULL
);

INSERT INTO @Files (TemplateCode, FileName, FileCategory) VALUES
(N'a', N'CourseObjective_01.pdf', N'Course Objective'),
(N'b', N'CLO_Task_Mapping_01.pdf', N'CLO-Task Mapping'),
(N'c', N'CourseContent_01.pdf', N'Course Content'),
(N'd', N'WeeklyPlan_01.pdf', N'Weekly Plan'),
(N'e', N'AttendanceRecord_01.pdf', N'Attendance Record'),
(N'f', N'ReferenceMaterial_01.pdf', N'Reference Material'),
(N'j', N'MarksDistribution_01.pdf', N'Marks Distribution and Grading'),
(N'k', N'Result_01.pdf', N'Result'),
(N'l', N'OutcomesAssessment_01.pdf', N'Outcomes Assessment'),
(N'm', N'TechnologyInvolved_01.pdf', N'Technology Involved'),
(N'n', N'DesignTechniques_01.pdf', N'Design Techniques Practiced'),
(N'o', N'CompleteAnalysis_01.pdf', N'Complete Analysis of Course');

-- g questions
INSERT INTO @Files (TemplateCode, FileName, FileCategory, Purpose, AssessmentNo, AssessmentTypeID)
SELECT N'g', CONCAT(N'Quiz', n, N'_Questions.pdf'), N'Quiz Question', N'Question Paper', n, @TypeQuiz FROM (VALUES (1),(2),(3),(4),(5),(6),(7),(8)) v(n);
INSERT INTO @Files (TemplateCode, FileName, FileCategory, Purpose, AssessmentNo, AssessmentTypeID)
SELECT N'g', CONCAT(N'Assignment', n, N'_Questions.pdf'), N'Assignment Question', N'Question Paper', n, @TypeAssign FROM (VALUES (1),(2),(3),(4)) v(n);
INSERT INTO @Files (TemplateCode, FileName, FileCategory, Purpose, AssessmentNo, AssessmentTypeID) VALUES
(N'g', N'MidExam1_Questions.pdf', N'Mid Exam Question', N'Question Paper', 1, @TypeMid),
(N'g', N'FinalExam1_Questions.pdf', N'Final Exam Question', N'Question Paper', 1, @TypeFinal);

-- h solutions
INSERT INTO @Files (TemplateCode, FileName, FileCategory, Purpose, AssessmentNo, AssessmentTypeID)
SELECT N'h', CONCAT(N'Quiz', n, N'_Solution.pdf'), N'Quiz Solution', N'Solution', n, @TypeQuiz FROM (VALUES (1),(2),(3),(4),(5),(6),(7),(8)) v(n);
INSERT INTO @Files (TemplateCode, FileName, FileCategory, Purpose, AssessmentNo, AssessmentTypeID)
SELECT N'h', CONCAT(N'Assignment', n, N'_Solution.pdf'), N'Assignment Solution', N'Solution', n, @TypeAssign FROM (VALUES (1),(2),(3),(4)) v(n);
INSERT INTO @Files (TemplateCode, FileName, FileCategory, Purpose, AssessmentNo, AssessmentTypeID) VALUES
(N'h', N'MidExam1_Solution.pdf', N'Mid Exam Solution', N'Solution', 1, @TypeMid),
(N'h', N'FinalExam1_Solution.pdf', N'Final Exam Solution', N'Solution', 1, @TypeFinal);

-- i samples (link Best/Avg/Worst to assessment #1 of each type)
INSERT INTO @Files (TemplateCode, FileName, FileCategory, Purpose, AssessmentNo, AssessmentTypeID) VALUES
(N'i', N'Assignment_Best.pdf', N'Assignment Best Sample', N'Best Sample', 1, @TypeAssign),
(N'i', N'Assignment_Average.pdf', N'Assignment Average Sample', N'Average Sample', 1, @TypeAssign),
(N'i', N'Assignment_Worst.pdf', N'Assignment Worst Sample', N'Worst Sample', 1, @TypeAssign),
(N'i', N'Quiz_Best.pdf', N'Quiz Best Sample', N'Best Sample', 1, @TypeQuiz),
(N'i', N'Quiz_Average.pdf', N'Quiz Average Sample', N'Average Sample', 1, @TypeQuiz),
(N'i', N'Quiz_Worst.pdf', N'Quiz Worst Sample', N'Worst Sample', 1, @TypeQuiz),
(N'i', N'Mid_Best.pdf', N'Mid Exam Best Sample', N'Best Sample', 1, @TypeMid),
(N'i', N'Mid_Average.pdf', N'Mid Exam Average Sample', N'Average Sample', 1, @TypeMid),
(N'i', N'Mid_Worst.pdf', N'Mid Exam Worst Sample', N'Worst Sample', 1, @TypeMid),
(N'i', N'Final_Best.pdf', N'Final Exam Best Sample', N'Best Sample', 1, @TypeFinal),
(N'i', N'Final_Average.pdf', N'Final Exam Average Sample', N'Average Sample', 1, @TypeFinal),
(N'i', N'Final_Worst.pdf', N'Final Exam Worst Sample', N'Worst Sample', 1, @TypeFinal);

DECLARE @fn NVARCHAR(260), @cat NVARCHAR(100), @purpose NVARCHAR(50), @fileAsmNo INT, @fileAsmType INT, @fileId INT, @asmId INT;

DECLARE fcur CURSOR LOCAL FAST_FORWARD FOR
    SELECT TemplateCode, FileName, FileCategory, Purpose, AssessmentNo, AssessmentTypeID FROM @Files;
OPEN fcur;
FETCH NEXT FROM fcur INTO @code, @fn, @cat, @purpose, @fileAsmNo, @fileAsmType;
WHILE @@FETCH_STATUS = 0
BEGIN
    SELECT @dirId = d.DirectoryID, @dirPath = d.DirectoryPath
    FROM CourseFolderDirectories d
    JOIN @Templates t ON t.TemplateItemID = d.TemplateItemID
    WHERE d.CourseFolderID = @FolderA AND t.TemplateCode = @code;

    INSERT INTO CourseFolderFiles (DirectoryID, UploadedByUserID, FileName, FilePath, FileCategory, Remarks)
    VALUES (
        @dirId, @TeacherA, @fn,
        CONCAT(@dirPath, N'/', @fn),
        @cat,
        CASE WHEN @purpose IS NULL THEN N'Demo seed'
             ELSE CONCAT(N'AssessmentNo=', @fileAsmNo, N';Purpose=', @purpose, N';Seed=1') END
    );
    SET @fileId = SCOPE_IDENTITY();

    IF @purpose IS NOT NULL AND @fileAsmNo IS NOT NULL AND @fileAsmType IS NOT NULL
    BEGIN
        SELECT @asmId = AssessmentID
        FROM Assessments
        WHERE OfferedCourseID = @OcA AND AssessmentTypeID = @fileAsmType AND AssessmentNo = @fileAsmNo;

        IF @asmId IS NOT NULL
        BEGIN
            INSERT INTO AssessmentFiles (AssessmentID, FileID, FilePurpose)
            VALUES (@asmId, @fileId, @purpose);
        END
    END

    FETCH NEXT FROM fcur INTO @code, @fn, @cat, @purpose, @fileAsmNo, @fileAsmType;
END
CLOSE fcur;
DEALLOCATE fcur;

PRINT 'Partial folder files for OC8...';
DECLARE @TidE INT = (SELECT TemplateItemID FROM CourseFolderTemplateItems WHERE TemplateCode = N'e');
DECLARE @TidF INT = (SELECT TemplateItemID FROM CourseFolderTemplateItems WHERE TemplateCode = N'f');
DECLARE @DirBe NVARCHAR(500) = CONCAT(@RootB, N'/e-Attendance Record');
DECLARE @DirBf NVARCHAR(500) = CONCAT(@RootB, N'/f-Reference Material');

INSERT INTO CourseFolderDirectories (CourseFolderID, ParentDirectoryID, TemplateItemID, DirectoryName, DirectoryPath)
VALUES
(@FolderB, NULL, @TidE, N'e-Attendance Record', @DirBe),
(@FolderB, NULL, @TidF, N'f-Reference Material', @DirBf);

DECLARE @DirBeId INT = (SELECT DirectoryID FROM CourseFolderDirectories WHERE CourseFolderID = @FolderB AND TemplateItemID = @TidE);
DECLARE @DirBfId INT = (SELECT DirectoryID FROM CourseFolderDirectories WHERE CourseFolderID = @FolderB AND TemplateItemID = @TidF);

INSERT INTO CourseFolderFiles (DirectoryID, UploadedByUserID, FileName, FilePath, FileCategory, Remarks) VALUES
(@DirBeId, @TeacherB, N'AttendanceRecord_partial.pdf', CONCAT(@DirBe, N'/AttendanceRecord_partial.pdf'), N'Attendance Record', N'Partial seed'),
(@DirBfId, @TeacherB, N'ReferenceMaterial_partial.pdf', CONCAT(@DirBf, N'/ReferenceMaterial_partial.pdf'), N'Reference Material', N'Partial seed');

PRINT 'Seeding CAI-262 pending content for HOD review...';

-- Clear prior DEMO pending for PC 115
DELETE FROM CourseAssessmentTypeCLOMappings WHERE ProgramCourseID = @Pc262 AND SessionID = @SessionId;
DELETE FROM PLOCLOMappings WHERE ProgramCourseID = @Pc262 AND SessionID = @SessionId;
DELETE FROM CourseTopicCLOMappings WHERE TopicID IN (
    SELECT TopicID FROM CourseTopics WHERE ProgramCourseID = @Pc262 AND SessionID = @SessionId);
DELETE FROM CourseTopics WHERE ProgramCourseID = @Pc262 AND SessionID = @SessionId;
DELETE FROM CLOs WHERE ProgramCourseID = @Pc262 AND SessionID = @SessionId;
DELETE FROM FolderSubmissions WHERE ContentSubmissionID IN (
    SELECT SubmissionID FROM ProgramCourseContentSubmissions WHERE ProgramCourseID = @Pc262 AND SessionID = @SessionId);
DELETE FROM ProgramCourseContentSubmissions WHERE ProgramCourseID = @Pc262 AND SessionID = @SessionId;

DECLARE @CtClos INT = (SELECT ContentTypeID FROM ContentSubmissionTypes WHERE ContentTypeCode = N'CLOs');
DECLARE @CtCloPlo INT = (SELECT ContentTypeID FROM ContentSubmissionTypes WHERE ContentTypeCode = N'CloPloMapping');
DECLARE @CtCloTask INT = (SELECT ContentTypeID FROM ContentSubmissionTypes WHERE ContentTypeCode = N'CloTaskMapping');
DECLARE @CtWeekly INT = (SELECT ContentTypeID FROM ContentSubmissionTypes WHERE ContentTypeCode = N'WeeklyContent');

-- Link PLOs if DEMO PLOs exist
DECLARE @Plo1 INT = (SELECT TOP 1 PLOID FROM PLOs WHERE PLOCode = N'DEMO-PLO1');
DECLARE @Plo2 INT = (SELECT TOP 1 PLOID FROM PLOs WHERE PLOCode = N'DEMO-PLO2');

INSERT INTO ProgramCourseContentSubmissions
    (ProgramCourseID, SessionID, ContentTypeID, SubmittedByUserID, SubmittedAt, SubmissionStatus, VersionNo, IsCurrentApproved)
VALUES (@Pc262, @SessionId, @CtClos, @TeacherMl, DATEADD(MINUTE, -40, GETUTCDATE()), N'Submitted', 1, 0);
DECLARE @SubClos262 INT = SCOPE_IDENTITY();

INSERT INTO CLOs (ProgramCourseID, CLOCode, CLODescription, SessionID, ContentStatus, SubmissionID) VALUES
(@Pc262, N'CLO1', N'ML Demo: Explain supervised learning foundations.', @SessionId, N'PendingReview', @SubClos262),
(@Pc262, N'CLO2', N'ML Demo: Train and evaluate a simple predictive model.', @SessionId, N'PendingReview', @SubClos262),
(@Pc262, N'CLO3', N'ML Demo: Communicate model limitations ethically.', @SessionId, N'PendingReview', @SubClos262);

DECLARE @MlClo1 INT = (SELECT CLOID FROM CLOs WHERE ProgramCourseID = @Pc262 AND SessionID = @SessionId AND CLOCode = N'CLO1');
DECLARE @MlClo2 INT = (SELECT CLOID FROM CLOs WHERE ProgramCourseID = @Pc262 AND SessionID = @SessionId AND CLOCode = N'CLO2');
DECLARE @MlClo3 INT = (SELECT CLOID FROM CLOs WHERE ProgramCourseID = @Pc262 AND SessionID = @SessionId AND CLOCode = N'CLO3');

INSERT INTO ProgramCourseContentSubmissions
    (ProgramCourseID, SessionID, ContentTypeID, SubmittedByUserID, SubmittedAt, SubmissionStatus, VersionNo, IsCurrentApproved)
VALUES (@Pc262, @SessionId, @CtCloPlo, @TeacherMl, DATEADD(MINUTE, -30, GETUTCDATE()), N'Submitted', 1, 0);
DECLARE @SubCloPlo262 INT = SCOPE_IDENTITY();

IF @Plo1 IS NOT NULL AND @Plo2 IS NOT NULL
BEGIN
    INSERT INTO PLOCLOMappings (ProgramCourseID, SessionID, PLOID, CLOID, Percentage, UpdatedByUserID, ContentStatus, SubmissionID) VALUES
    (@Pc262, @SessionId, @Plo1, @MlClo1, 50, @TeacherMl, N'PendingReview', @SubCloPlo262),
    (@Pc262, @SessionId, @Plo1, @MlClo2, 50, @TeacherMl, N'PendingReview', @SubCloPlo262),
    (@Pc262, @SessionId, @Plo2, @MlClo2, 60, @TeacherMl, N'PendingReview', @SubCloPlo262),
    (@Pc262, @SessionId, @Plo2, @MlClo3, 40, @TeacherMl, N'PendingReview', @SubCloPlo262);
END

INSERT INTO ProgramCourseContentSubmissions
    (ProgramCourseID, SessionID, ContentTypeID, SubmittedByUserID, SubmittedAt, SubmissionStatus, VersionNo, IsCurrentApproved)
VALUES (@Pc262, @SessionId, @CtCloTask, @TeacherMl, DATEADD(MINUTE, -20, GETUTCDATE()), N'Submitted', 1, 0);
DECLARE @SubCloTask262 INT = SCOPE_IDENTITY();

INSERT INTO CourseAssessmentTypeCLOMappings
    (ProgramCourseID, SessionID, AssessmentTypeID, CLOID, Percentage, UpdatedByUserID, ContentStatus, SubmissionID)
VALUES
(@Pc262, @SessionId, @TypeQuiz, @MlClo1, 25, @TeacherMl, N'PendingReview', @SubCloTask262),
(@Pc262, @SessionId, @TypeAssign, @MlClo1, 25, @TeacherMl, N'PendingReview', @SubCloTask262),
(@Pc262, @SessionId, @TypeMid, @MlClo1, 25, @TeacherMl, N'PendingReview', @SubCloTask262),
(@Pc262, @SessionId, @TypeFinal, @MlClo1, 25, @TeacherMl, N'PendingReview', @SubCloTask262),
(@Pc262, @SessionId, @TypeQuiz, @MlClo2, 20, @TeacherMl, N'PendingReview', @SubCloTask262),
(@Pc262, @SessionId, @TypeAssign, @MlClo2, 30, @TeacherMl, N'PendingReview', @SubCloTask262),
(@Pc262, @SessionId, @TypeMid, @MlClo2, 25, @TeacherMl, N'PendingReview', @SubCloTask262),
(@Pc262, @SessionId, @TypeFinal, @MlClo2, 25, @TeacherMl, N'PendingReview', @SubCloTask262),
(@Pc262, @SessionId, @TypeQuiz, @MlClo3, 10, @TeacherMl, N'PendingReview', @SubCloTask262),
(@Pc262, @SessionId, @TypeAssign, @MlClo3, 20, @TeacherMl, N'PendingReview', @SubCloTask262),
(@Pc262, @SessionId, @TypeMid, @MlClo3, 30, @TeacherMl, N'PendingReview', @SubCloTask262),
(@Pc262, @SessionId, @TypeFinal, @MlClo3, 40, @TeacherMl, N'PendingReview', @SubCloTask262);

INSERT INTO ProgramCourseContentSubmissions
    (ProgramCourseID, SessionID, ContentTypeID, SubmittedByUserID, SubmittedAt, SubmissionStatus, VersionNo, IsCurrentApproved)
VALUES (@Pc262, @SessionId, @CtWeekly, @TeacherMl, DATEADD(MINUTE, -10, GETUTCDATE()), N'Submitted', 1, 0);
DECLARE @SubWeekly262 INT = SCOPE_IDENTITY();

INSERT INTO CourseTopics (ProgramCourseID, ParentTopicID, WeekNo, TopicTitle, TopicDescription, DisplayOrder, SessionID, ContentStatus, SubmissionID)
VALUES
(@Pc262, NULL, 1, N'ML intro & data prep', N'Pending review week 1', 1, @SessionId, N'PendingReview', @SubWeekly262),
(@Pc262, NULL, 2, N'Supervised models', N'Pending review week 2', 2, @SessionId, N'PendingReview', @SubWeekly262);

DECLARE @TopicMl1 INT = (SELECT TOP 1 TopicID FROM CourseTopics WHERE ProgramCourseID = @Pc262 AND WeekNo = 1 AND SessionID = @SessionId);
DECLARE @TopicMl2 INT = (SELECT TOP 1 TopicID FROM CourseTopics WHERE ProgramCourseID = @Pc262 AND WeekNo = 2 AND SessionID = @SessionId);
INSERT INTO CourseTopicCLOMappings (TopicID, CLOID) VALUES (@TopicMl1, @MlClo1), (@TopicMl2, @MlClo2);

-- Grant teacher ML permissions for CAI-262 so after approve they can continue
DECLARE @PermClos INT = (SELECT PermissionTypeID FROM TeacherPermissionTypes WHERE PermissionType = N'Manage CLOs');
DECLARE @PermWeekly INT = (SELECT PermissionTypeID FROM TeacherPermissionTypes WHERE PermissionType = N'Upload Weekly Content');
DECLARE @PermCloPlo INT = (SELECT PermissionTypeID FROM TeacherPermissionTypes WHERE PermissionType = N'Manage CLO-PLO Mapping');
DECLARE @PermCloTask INT = (SELECT PermissionTypeID FROM TeacherPermissionTypes WHERE PermissionType = N'Manage CLO-Task Mapping');
DECLARE @PermMid INT = (SELECT PermissionTypeID FROM TeacherPermissionTypes WHERE PermissionType = N'Upload Mid Exam Questions');
DECLARE @PermFinal INT = (SELECT PermissionTypeID FROM TeacherPermissionTypes WHERE PermissionType = N'Upload Final Exam Questions');

DELETE FROM TeacherPermissions
WHERE ProgramCourseID = @Pc262 AND TeacherUserID = @TeacherMl
  AND PermissionTypeID IN (@PermClos, @PermWeekly, @PermCloPlo, @PermCloTask, @PermMid, @PermFinal);

INSERT INTO TeacherPermissions (TeacherUserID, ProgramCourseID, PermissionTypeID, HODUserID, IsAllowed)
VALUES
(@TeacherMl, @Pc262, @PermClos, @HodId, 1),
(@TeacherMl, @Pc262, @PermWeekly, @HodId, 1),
(@TeacherMl, @Pc262, @PermCloPlo, @HodId, 1),
(@TeacherMl, @Pc262, @PermCloTask, @HodId, 1),
(@TeacherMl, @Pc262, @PermMid, @HodId, 1),
(@TeacherMl, @Pc262, @PermFinal, @HodId, 1);

-- Ensure shafqat password is Teacher@123 for easy login
UPDATE Users
SET PasswordHash = N'AQAAAAIAAYagAAAAEDC7jL0eN8aLHwdmyWstxLB/1PzmsOVOUnhYpEXoj7DlBYKzxBnMN+v+HzFvqzTR1A=='
WHERE UserID = @TeacherMl;

PRINT '=========================================================';
PRINT 'Full feature seed complete.';
PRINT 'HOD review: munir@biit.edu.pk / Hod@123';
PRINT '  - Content queue: CAI-262 CLOs / mappings / weekly (Submitted)';
PRINT '  - Folder queue: CAI-261 BAI-4A master folder (Submitted)';
PRINT 'Teacher assessments: aftab@biit.edu.pk / Teacher@123 → CAI-261 BAI-4A';
PRINT '  - 8 quizzes, 4 assignments, mid, final + solutions/samples';
PRINT 'Partial folder: shahid_jamil@biit.edu.pk / Teacher@123 → CAI-261 BAI-4B';
PRINT 'ML teacher (after HOD approve): shafqat@biit.edu.pk / Teacher@123';
PRINT '=========================================================';
GO
