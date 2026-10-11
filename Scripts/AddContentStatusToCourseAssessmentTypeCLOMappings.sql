IF COL_LENGTH('CourseAssessmentTypeCLOMappings', 'ContentStatus') IS NULL
BEGIN
    ALTER TABLE CourseAssessmentTypeCLOMappings
    ADD ContentStatus NVARCHAR(30) NOT NULL
        CONSTRAINT DF_CourseAssessmentTypeCLOMappings_ContentStatus DEFAULT ('Draft');
END;
GO

IF COL_LENGTH('CourseAssessmentTypeCLOMappings', 'SubmissionID') IS NULL
BEGIN
    ALTER TABLE CourseAssessmentTypeCLOMappings
    ADD SubmissionID INT NULL;
END;
GO

UPDATE CourseAssessmentTypeCLOMappings
SET ContentStatus = 'Draft'
WHERE ContentStatus IS NULL OR LTRIM(RTRIM(ContentStatus)) = '';
GO

IF NOT EXISTS (
    SELECT 1 FROM ContentSubmissionTypes WHERE ContentTypeCode = 'CloTaskMapping'
)
BEGIN
    INSERT INTO ContentSubmissionTypes (ContentTypeCode, ContentTypeName, SortOrder)
    VALUES ('CloTaskMapping', 'CLO-Task Mapping', 4);
END;
GO
