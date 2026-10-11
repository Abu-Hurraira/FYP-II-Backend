IF OBJECT_ID(N'dbo.HodUploadAllocations', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.HodUploadAllocations (
        OfferedCourseID INT NOT NULL,
        Clos BIT NOT NULL CONSTRAINT DF_HodUploadAllocations_Clos DEFAULT (0),
        CloPloMapping BIT NOT NULL CONSTRAINT DF_HodUploadAllocations_CloPlo DEFAULT (0),
        CloTaskMapping BIT NOT NULL CONSTRAINT DF_HodUploadAllocations_CloTask DEFAULT (0),
        Mid BIT NOT NULL CONSTRAINT DF_HodUploadAllocations_Mid DEFAULT (0),
        Final BIT NOT NULL CONSTRAINT DF_HodUploadAllocations_Final DEFAULT (0),
        FolderTemplate BIT NOT NULL CONSTRAINT DF_HodUploadAllocations_Folder DEFAULT (0),
        UpdatedByHodUserID INT NULL,
        UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_HodUploadAllocations_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_HodUploadAllocations PRIMARY KEY (OfferedCourseID),
        CONSTRAINT FK_HodUploadAllocations_OfferedCourses
            FOREIGN KEY (OfferedCourseID) REFERENCES dbo.OfferedCourses (OfferedCourseID)
            ON DELETE CASCADE
    );
END
GO
