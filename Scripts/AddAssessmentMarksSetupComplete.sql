-- Marks entry gate: teacher must complete assessment setup (questions + total marks)
-- before the section mark sheet is available.
IF COL_LENGTH('dbo.Assessments', 'IsSetupComplete') IS NULL
BEGIN
    ALTER TABLE dbo.Assessments
    ADD IsSetupComplete BIT NOT NULL
        CONSTRAINT DF_Assessments_IsSetupComplete DEFAULT (0);
END
GO

IF COL_LENGTH('dbo.Assessments', 'SetupCompletedAt') IS NULL
BEGIN
    ALTER TABLE dbo.Assessments
    ADD SetupCompletedAt DATETIME NULL;
END
GO

-- Ensure mark tables exist (scaffolded in most DBs; create if missing).
IF OBJECT_ID(N'dbo.StudentAssessmentMarks', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.StudentAssessmentMarks
    (
        StudentAssessmentMarkID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_StudentAssessmentMarks PRIMARY KEY,
        AssessmentID INT NOT NULL,
        StudentID INT NOT NULL,
        ObtainedMarks DECIMAL(6,2) NOT NULL,
        CONSTRAINT UQ_StudentAssessmentMarks_Assessment_Student UNIQUE (AssessmentID, StudentID),
        CONSTRAINT FK_StudentAssessmentMarks_Assessment
            FOREIGN KEY (AssessmentID) REFERENCES dbo.Assessments(AssessmentID),
        CONSTRAINT FK_StudentAssessmentMarks_Student
            FOREIGN KEY (StudentID) REFERENCES dbo.Students(StudentID)
    );
END
GO

IF OBJECT_ID(N'dbo.StudentQuestionMarks', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.StudentQuestionMarks
    (
        StudentQuestionMarkID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_StudentQuestionMarks PRIMARY KEY,
        QuestionID INT NOT NULL,
        StudentID INT NOT NULL,
        ObtainedMarks DECIMAL(6,2) NOT NULL,
        CONSTRAINT UQ_StudentQuestionMarks_Question_Student UNIQUE (QuestionID, StudentID),
        CONSTRAINT FK_StudentQuestionMarks_Question
            FOREIGN KEY (QuestionID) REFERENCES dbo.AssessmentQuestions(QuestionID),
        CONSTRAINT FK_StudentQuestionMarks_Student
            FOREIGN KEY (StudentID) REFERENCES dbo.Students(StudentID)
    );
END
GO
