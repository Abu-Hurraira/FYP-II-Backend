SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

BEGIN TRANSACTION;

IF COL_LENGTH('dbo.PEOPLEMappings', 'IsMapped') IS NULL
BEGIN
    ALTER TABLE dbo.PEOPLEMappings
        ADD IsMapped bit NOT NULL
            CONSTRAINT DF_PEOPLEMappings_IsMapped DEFAULT (1);
END;

IF COL_LENGTH('dbo.PEOPLEMappings', 'Percentage') IS NOT NULL
BEGIN
    EXEC(N'
        UPDATE dbo.PEOPLEMappings
        SET IsMapped = CASE WHEN Percentage > 0 THEN 1 ELSE 0 END;
    ');

    EXEC(N'
        DELETE FROM dbo.PEOPLEMappings
        WHERE IsMapped = 0;
    ');

    ALTER TABLE dbo.PEOPLEMappings DROP COLUMN Percentage;
END;

;WITH DuplicateMappings AS
(
    SELECT
        MappingID,
        ROW_NUMBER() OVER (PARTITION BY PEOID, PLOID ORDER BY MappingID) AS DuplicateNumber
    FROM dbo.PEOPLEMappings
)
DELETE FROM DuplicateMappings
WHERE DuplicateNumber > 1;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.PEOPLEMappings')
      AND name = 'UQ_PEOPLEMappings_PEO_PLO'
)
BEGIN
    CREATE UNIQUE INDEX UQ_PEOPLEMappings_PEO_PLO
        ON dbo.PEOPLEMappings (PEOID, PLOID);
END;

COMMIT TRANSACTION;
