-- Nested question parts (self-referencing), same pattern as CourseTopics.
IF COL_LENGTH('dbo.AssessmentQuestions', 'ParentQuestionID') IS NULL
BEGIN
    ALTER TABLE dbo.AssessmentQuestions
    ADD ParentQuestionID INT NULL;

    ALTER TABLE dbo.AssessmentQuestions
    ADD CONSTRAINT FK_AssessmentQuestions_Parent
        FOREIGN KEY (ParentQuestionID)
        REFERENCES dbo.AssessmentQuestions(QuestionID);
END
GO

IF COL_LENGTH('dbo.AssessmentQuestions', 'DisplayOrder') IS NULL
BEGIN
    ALTER TABLE dbo.AssessmentQuestions
    ADD DisplayOrder INT NULL;
END
GO
