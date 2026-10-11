/*
  Course catalog versioning
  - Snapshot on Excel upload (not row-level triggers)
  - Immutable version items for Load-from-Database apply
*/
USE FolderManagementDB;
GO

IF OBJECT_ID(N'dbo.CourseCatalogVersionItems', N'U') IS NULL
   AND OBJECT_ID(N'dbo.CourseCatalogVersions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CourseCatalogVersions
    (
        CatalogVersionID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CourseCatalogVersions PRIMARY KEY,
        SourceProgramID INT NOT NULL,
        VersionLabel NVARCHAR(200) NOT NULL,
        SourceFileName NVARCHAR(260) NULL,
        CourseCount INT NOT NULL CONSTRAINT DF_CourseCatalogVersions_CourseCount DEFAULT (0),
        ContentFingerprint NVARCHAR(64) NOT NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_CourseCatalogVersions_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CreatedByUserID INT NULL,
        CONSTRAINT FK_CourseCatalogVersions_Program
            FOREIGN KEY (SourceProgramID) REFERENCES dbo.AcademicPrograms(ProgramID),
        CONSTRAINT FK_CourseCatalogVersions_User
            FOREIGN KEY (CreatedByUserID) REFERENCES dbo.Users(UserID)
    );

    CREATE INDEX IX_CourseCatalogVersions_SourceProgram_CreatedAt
        ON dbo.CourseCatalogVersions (SourceProgramID, CreatedAt DESC);

    CREATE INDEX IX_CourseCatalogVersions_Fingerprint
        ON dbo.CourseCatalogVersions (SourceProgramID, ContentFingerprint);
END
GO

IF OBJECT_ID(N'dbo.CourseCatalogVersionItems', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CourseCatalogVersionItems
    (
        CatalogVersionItemID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CourseCatalogVersionItems PRIMARY KEY,
        CatalogVersionID INT NOT NULL,
        CourseCode NVARCHAR(50) NOT NULL,
        CourseTitle NVARCHAR(200) NOT NULL,
        ClassCreditHours INT NULL,
        LabCreditHours INT NULL,
        DisplayOrder INT NOT NULL CONSTRAINT DF_CourseCatalogVersionItems_DisplayOrder DEFAULT (0),
        CONSTRAINT FK_CourseCatalogVersionItems_Version
            FOREIGN KEY (CatalogVersionID) REFERENCES dbo.CourseCatalogVersions(CatalogVersionID)
            ON DELETE CASCADE
    );

    CREATE INDEX IX_CourseCatalogVersionItems_Version
        ON dbo.CourseCatalogVersionItems (CatalogVersionID, DisplayOrder);
END
GO

PRINT 'Course catalog version tables ready.';
GO
