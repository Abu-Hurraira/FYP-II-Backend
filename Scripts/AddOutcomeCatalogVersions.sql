/*
  PEO/PLO outcome catalog versioning
  - Snapshot on Excel upload
  - Immutable version items for Load-from-Database apply
*/
USE FolderManagementDB;
GO

IF OBJECT_ID(N'dbo.OutcomeCatalogVersionItems', N'U') IS NULL
   AND OBJECT_ID(N'dbo.OutcomeCatalogVersions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.OutcomeCatalogVersions
    (
        OutcomeCatalogVersionID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_OutcomeCatalogVersions PRIMARY KEY,
        OutcomeType NVARCHAR(10) NOT NULL,
        SourceProgramID INT NOT NULL,
        VersionLabel NVARCHAR(200) NOT NULL,
        SourceFileName NVARCHAR(260) NULL,
        ItemCount INT NOT NULL CONSTRAINT DF_OutcomeCatalogVersions_ItemCount DEFAULT (0),
        ContentFingerprint NVARCHAR(64) NOT NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_OutcomeCatalogVersions_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CreatedByUserID INT NULL,
        CONSTRAINT CK_OutcomeCatalogVersions_Type CHECK (OutcomeType IN (N'PEO', N'PLO')),
        CONSTRAINT FK_OutcomeCatalogVersions_Program
            FOREIGN KEY (SourceProgramID) REFERENCES dbo.AcademicPrograms(ProgramID),
        CONSTRAINT FK_OutcomeCatalogVersions_User
            FOREIGN KEY (CreatedByUserID) REFERENCES dbo.Users(UserID)
    );

    CREATE INDEX IX_OutcomeCatalogVersions_Type_Source_CreatedAt
        ON dbo.OutcomeCatalogVersions (OutcomeType, SourceProgramID, CreatedAt DESC);

    CREATE INDEX IX_OutcomeCatalogVersions_Fingerprint
        ON dbo.OutcomeCatalogVersions (OutcomeType, SourceProgramID, ContentFingerprint);
END
GO

IF OBJECT_ID(N'dbo.OutcomeCatalogVersionItems', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.OutcomeCatalogVersionItems
    (
        OutcomeCatalogVersionItemID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_OutcomeCatalogVersionItems PRIMARY KEY,
        OutcomeCatalogVersionID INT NOT NULL,
        ItemCode NVARCHAR(20) NOT NULL,
        ItemDescription NVARCHAR(1000) NOT NULL,
        DisplayOrder INT NOT NULL CONSTRAINT DF_OutcomeCatalogVersionItems_DisplayOrder DEFAULT (0),
        CONSTRAINT FK_OutcomeCatalogVersionItems_Version
            FOREIGN KEY (OutcomeCatalogVersionID) REFERENCES dbo.OutcomeCatalogVersions(OutcomeCatalogVersionID)
            ON DELETE CASCADE
    );

    CREATE INDEX IX_OutcomeCatalogVersionItems_Version
        ON dbo.OutcomeCatalogVersionItems (OutcomeCatalogVersionID, DisplayOrder);
END
GO

PRINT 'Outcome catalog version tables ready.';
GO
