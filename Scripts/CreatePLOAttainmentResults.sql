USE FolderManagementDB;
GO

IF OBJECT_ID(N'dbo.PLOAttainmentResults', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PLOAttainmentResults
    (
        AttainmentID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PLOAttainmentResults PRIMARY KEY,
        OfferedCourseID INT NOT NULL,
        PLOID INT NOT NULL,
        AttainmentPercentage DECIMAL(5,2) NOT NULL,
        CalculatedAt DATETIME NOT NULL CONSTRAINT DF_PLOAttainmentResults_CalculatedAt DEFAULT (GETDATE()),
        CalculatedByUserID INT NULL,
        CONSTRAINT UQ_PLOAttainmentResults_OfferedCourse_PLO UNIQUE (OfferedCourseID, PLOID),
        CONSTRAINT FK_PLOAttainmentResults_OfferedCourse
            FOREIGN KEY (OfferedCourseID) REFERENCES dbo.OfferedCourses (OfferedCourseID),
        CONSTRAINT FK_PLOAttainmentResults_PLO
            FOREIGN KEY (PLOID) REFERENCES dbo.PLOs (PLOID),
        CONSTRAINT FK_PLOAttainmentResults_User
            FOREIGN KEY (CalculatedByUserID) REFERENCES dbo.Users (UserID)
    );
END
GO
