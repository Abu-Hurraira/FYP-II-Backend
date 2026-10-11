-- Extra CLO-Task matrix tasks per program-course + session (beyond Assignment/Quiz/Mid/Final).

IF OBJECT_ID(N'dbo.CourseSessionTaskTypes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CourseSessionTaskTypes (
        CourseSessionTaskTypeID INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_CourseSessionTaskTypes PRIMARY KEY,
        ProgramCourseID INT NOT NULL,
        SessionID INT NOT NULL,
        AssessmentTypeID INT NOT NULL,
        DisplayOrder INT NULL,
        CreatedAt DATETIME NOT NULL
            CONSTRAINT DF_CourseSessionTaskTypes_CreatedAt DEFAULT (GETUTCDATE()),
        CONSTRAINT UQ_CourseSessionTaskTypes
            UNIQUE (ProgramCourseID, SessionID, AssessmentTypeID),
        CONSTRAINT FK_CourseSessionTaskTypes_ProgramCourse
            FOREIGN KEY (ProgramCourseID) REFERENCES dbo.AcademicProgramCourses(ProgramCourseID),
        CONSTRAINT FK_CourseSessionTaskTypes_Session
            FOREIGN KEY (SessionID) REFERENCES dbo.AcademicSessions(SessionID),
        CONSTRAINT FK_CourseSessionTaskTypes_AssessmentType
            FOREIGN KEY (AssessmentTypeID) REFERENCES dbo.AssessmentTypes(AssessmentTypeID)
    );
END
GO
