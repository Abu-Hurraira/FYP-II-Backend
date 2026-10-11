/*
  FolderManagementDB — full schema + data script
  Generated for team share.

  How to recreate:
  1. CREATE DATABASE FolderManagementDB;
  2. Open this file in SSMS against that database and Execute (F5).
  3. Point appsettings.json DefaultConnection at your server.
  Note: uploaded files on disk are not included.
*/
GO
USE [FolderManagementDB]
GO

/****** Object:  Table [dbo].[AcademicProgramCourses]    Script Date: 8/25/2026 3:34:38 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AcademicProgramCourses](
	[ProgramCourseID] [int] IDENTITY(1,1) NOT NULL,
	[ProgramID] [int] NOT NULL,
	[CourseID] [int] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[ProgramCourseID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Users]    Script Date: 8/25/2026 3:34:38 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Users](
	[UserID] [int] IDENTITY(1,1) NOT NULL,
	[FullName] [nvarchar](150) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Email] [nvarchar](150) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[PasswordHash] [nvarchar](500) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[RoleID] [int] NOT NULL,
	[Rank] [nvarchar](100) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[AridNo] [nvarchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
PRIMARY KEY CLUSTERED 
(
	[UserID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AcademicPrograms]    Script Date: 8/25/2026 3:34:38 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AcademicPrograms](
	[ProgramID] [int] IDENTITY(1,1) NOT NULL,
	[ProgramName] [nvarchar](150) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[ProgramCode] [nvarchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
PRIMARY KEY CLUSTERED 
(
	[ProgramID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Courses]    Script Date: 8/25/2026 3:34:38 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Courses](
	[CourseID] [int] IDENTITY(1,1) NOT NULL,
	[CourseTitle] [nvarchar](200) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[CourseCode] [nvarchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[ClassCreditHours] [int] NULL,
	[LabCreditHours] [int] NULL,
PRIMARY KEY CLUSTERED 
(
	[CourseID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AcademicSessions]    Script Date: 8/25/2026 3:34:38 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AcademicSessions](
	[SessionID] [int] IDENTITY(1,1) NOT NULL,
	[ProgramID] [int] NOT NULL,
	[SessionTitle] [nvarchar](100) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[AcademicYear] [nvarchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[StartDate] [date] NULL,
PRIMARY KEY CLUSTERED 
(
	[SessionID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ContentSubmissionTypes]    Script Date: 8/25/2026 3:34:38 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ContentSubmissionTypes](
	[ContentTypeID] [int] IDENTITY(1,1) NOT NULL,
	[ContentTypeCode] [nvarchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[ContentTypeName] [nvarchar](100) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[SortOrder] [int] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[ContentTypeID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ProgramCourseContentSubmissions]    Script Date: 8/25/2026 3:34:38 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ProgramCourseContentSubmissions](
	[SubmissionID] [int] IDENTITY(1,1) NOT NULL,
	[ProgramCourseID] [int] NOT NULL,
	[SessionID] [int] NOT NULL,
	[ContentTypeID] [int] NOT NULL,
	[SubmittedByUserID] [int] NOT NULL,
	[SubmittedAt] [datetime] NOT NULL,
	[SubmissionStatus] [nvarchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[ReviewedByUserID] [int] NULL,
	[ReviewedAt] [datetime] NULL,
	[HodFeedback] [nvarchar](1000) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[VersionNo] [int] NOT NULL,
	[IsCurrentApproved] [bit] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[SubmissionID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  View [dbo].[vw_HodContentReviewQueue]    Script Date: 8/25/2026 3:34:38 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE VIEW [dbo].[vw_HodContentReviewQueue]
AS
SELECT
    pcs.SubmissionID,
    pcs.ProgramCourseID,
    pcs.SessionID,
    ses.SessionTitle,
    ses.AcademicYear,
    ap.ProgramName,
    c.CourseTitle,
    c.CourseCode,
    ct.ContentTypeCode,
    ct.ContentTypeName,
    u.FullName AS SubmittedByName,
    pcs.SubmittedAt,
    pcs.SubmissionStatus,
    pcs.VersionNo,
    pcs.HodFeedback
FROM ProgramCourseContentSubmissions pcs
INNER JOIN ContentSubmissionTypes ct ON ct.ContentTypeID = pcs.ContentTypeID
INNER JOIN AcademicProgramCourses apc ON apc.ProgramCourseID = pcs.ProgramCourseID
INNER JOIN AcademicPrograms ap ON ap.ProgramID = apc.ProgramID
INNER JOIN Courses c ON c.CourseID = apc.CourseID
INNER JOIN AcademicSessions ses ON ses.SessionID = pcs.SessionID
INNER JOIN Users u ON u.UserID = pcs.SubmittedByUserID
WHERE pcs.SubmissionStatus = 'Submitted';

GO
/****** Object:  Table [dbo].[TeacherPermissionTypes]    Script Date: 8/25/2026 3:34:38 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[TeacherPermissionTypes](
	[PermissionTypeID] [int] IDENTITY(1,1) NOT NULL,
	[PermissionType] [nvarchar](100) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[PermissionTypeID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Roles]    Script Date: 8/25/2026 3:34:38 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Roles](
	[RoleID] [int] IDENTITY(1,1) NOT NULL,
	[RoleName] [nvarchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[RoleID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[PEOPLEMappings]    Script Date: 8/25/2026 3:34:38 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[PEOPLEMappings](
	[MappingID] [int] IDENTITY(1,1) NOT NULL,
	[PEOID] [int] NOT NULL,
	[PLOID] [int] NOT NULL,
	[MappedByUserID] [int] NULL,
	[IsMapped] [bit] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[MappingID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AssessmentTypes]    Script Date: 8/25/2026 3:34:38 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AssessmentTypes](
	[AssessmentTypeID] [int] IDENTITY(1,1) NOT NULL,
	[AssessmentTypeName] [nvarchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[AssessmentTypeID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[CourseFolderTemplateItems]    Script Date: 8/25/2026 3:34:38 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[CourseFolderTemplateItems](
	[TemplateItemID] [int] IDENTITY(1,1) NOT NULL,
	[TemplateCode] [nvarchar](5) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[FolderItemName] [nvarchar](200) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[SortOrder] [int] NOT NULL,
	[IsRequired] [bit] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[TemplateItemID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Book]    Script Date: 8/25/2026 3:34:38 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Book](
	[ISBN] [varchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Bname] [varchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[price] [int] NULL,
	[Category] [varchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
PRIMARY KEY CLUSTERED 
(
	[ISBN] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[BookImage]    Script Date: 8/25/2026 3:34:38 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[BookImage](
	[Bimageid] [int] IDENTITY(1,1) NOT NULL,
	[ISBN] [varchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
PRIMARY KEY CLUSTERED 
(
	[Bimageid] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[PEOs]    Script Date: 8/25/2026 3:34:38 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[PEOs](
	[PEOID] [int] IDENTITY(1,1) NOT NULL,
	[ProgramID] [int] NOT NULL,
	[PEOCode] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[PEODescription] [nvarchar](1000) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[PEOID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[PLOs]    Script Date: 8/25/2026 3:34:38 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[PLOs](
	[PLOID] [int] IDENTITY(1,1) NOT NULL,
	[ProgramID] [int] NOT NULL,
	[PLOCode] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[PLODescription] [nvarchar](1000) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[PLOID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Notifications]    Script Date: 8/25/2026 3:34:38 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Notifications](
	[NotificationID] [int] IDENTITY(1,1) NOT NULL,
	[UserID] [int] NOT NULL,
	[NotificationTitle] [nvarchar](200) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[NotificationMessage] [nvarchar](1000) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[NotificationType] [nvarchar](100) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[RelatedID] [int] NULL,
	[IsRead] [bit] NOT NULL,
	[CreatedAt] [datetime] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[NotificationID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[GraderAllocations]    Script Date: 8/25/2026 3:34:38 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[GraderAllocations](
	[GraderAllocationID] [int] IDENTITY(1,1) NOT NULL,
	[TeacherUserID] [int] NOT NULL,
	[GraderUserID] [int] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[GraderAllocationID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ProgramCoursePLOMappings]    Script Date: 8/25/2026 3:34:38 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ProgramCoursePLOMappings](
	[MappingID] [int] IDENTITY(1,1) NOT NULL,
	[ProgramCourseID] [int] NOT NULL,
	[PLOID] [int] NOT NULL,
	[Percentage] [decimal](5, 2) NULL,
	[MappedByUserID] [int] NULL,
PRIMARY KEY CLUSTERED 
(
	[MappingID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[TeacherPermissions]    Script Date: 8/25/2026 3:34:38 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[TeacherPermissions](
	[PermissionID] [int] IDENTITY(1,1) NOT NULL,
	[TeacherUserID] [int] NOT NULL,
	[ProgramCourseID] [int] NOT NULL,
	[PermissionTypeID] [int] NOT NULL,
	[HODUserID] [int] NULL,
	[IsAllowed] [bit] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[PermissionID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Sections]    Script Date: 8/25/2026 3:34:38 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Sections](
	[SectionID] [int] IDENTITY(1,1) NOT NULL,
	[SessionID] [int] NOT NULL,
	[SectionName] [nvarchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Semester] [int] NULL,
	[StudentListFilePath] [nvarchar](500) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[AttendanceSheetPath] [nvarchar](500) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
PRIMARY KEY CLUSTERED 
(
	[SectionID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Students]    Script Date: 8/25/2026 3:34:38 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Students](
	[StudentID] [int] IDENTITY(1,1) NOT NULL,
	[SectionID] [int] NOT NULL,
	[FullName] [nvarchar](150) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[AridNo] [nvarchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Email] [nvarchar](150) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
PRIMARY KEY CLUSTERED 
(
	[StudentID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[CourseTopics]    Script Date: 8/25/2026 3:34:38 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[CourseTopics](
	[TopicID] [int] IDENTITY(1,1) NOT NULL,
	[ProgramCourseID] [int] NOT NULL,
	[ParentTopicID] [int] NULL,
	[WeekNo] [int] NULL,
	[TopicTitle] [nvarchar](300) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[TopicDescription] [nvarchar](1000) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[DisplayOrder] [int] NULL,
	[SessionID] [int] NOT NULL,
	[ContentStatus] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[SubmissionID] [int] NULL,
PRIMARY KEY CLUSTERED 
(
	[TopicID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[CLOs]    Script Date: 8/25/2026 3:34:38 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[CLOs](
	[CLOID] [int] IDENTITY(1,1) NOT NULL,
	[ProgramCourseID] [int] NOT NULL,
	[CLOCode] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[CLODescription] [nvarchar](1000) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[SessionID] [int] NOT NULL,
	[ContentStatus] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[SubmissionID] [int] NULL,
PRIMARY KEY CLUSTERED 
(
	[CLOID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[OfferedCourses]    Script Date: 8/25/2026 3:34:38 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[OfferedCourses](
	[OfferedCourseID] [int] IDENTITY(1,1) NOT NULL,
	[SectionID] [int] NOT NULL,
	[CourseID] [int] NOT NULL,
	[TeacherUserID] [int] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[OfferedCourseID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[OfferedCourseResultSchemes]    Script Date: 8/25/2026 3:34:38 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[OfferedCourseResultSchemes](
	[SchemeID] [int] IDENTITY(1,1) NOT NULL,
	[OfferedCourseID] [int] NOT NULL,
	[TotalMarks] [decimal](6, 2) NOT NULL,
	[QuizMarks] [decimal](6, 2) NOT NULL,
	[HomeworkMarks] [decimal](6, 2) NOT NULL,
	[MidMarks] [decimal](6, 2) NOT NULL,
	[LabMarks] [decimal](6, 2) NOT NULL,
	[FinalTheoryMarks] [decimal](6, 2) NOT NULL,
	[FinalLabMarks] [decimal](6, 2) NOT NULL,
	[AssignmentBestOf] [int] NOT NULL,
	[QuizBestOf] [int] NOT NULL,
	[AttendanceHomeworkMarks] [decimal](6, 2) NOT NULL,
	[UpdatedAt] [datetime] NULL,
	[UpdatedByUserID] [int] NULL,
	[LastGeneratedAt] [datetime] NULL,
	[LastGeneratedByUserID] [int] NULL,
 CONSTRAINT [PK_OfferedCourseResultSchemes] PRIMARY KEY CLUSTERED 
(
	[SchemeID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[OfferedCourseLabMarkSchemes]    Script Date: 8/25/2026 3:34:38 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[OfferedCourseLabMarkSchemes](
	[OfferedCourseID] [int] NOT NULL,
	[QuizMarks] [decimal](6, 2) NOT NULL,
	[AssignmentMarks] [decimal](6, 2) NOT NULL,
	[LabTaskMarks] [decimal](6, 2) NOT NULL,
	[ProjectMarks] [decimal](6, 2) NOT NULL,
	[AttendanceMarks] [decimal](6, 2) NOT NULL,
	[ExamMarks] [decimal](6, 2) NOT NULL,
	[UpdatedAt] [datetime] NULL,
	[UpdatedByUserID] [int] NULL,
 CONSTRAINT [PK_OfferedCourseLabMarkSchemes] PRIMARY KEY CLUSTERED 
(
	[OfferedCourseID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[OfferedCourseHomeworkMarkSchemes]    Script Date: 8/25/2026 3:34:38 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[OfferedCourseHomeworkMarkSchemes](
	[OfferedCourseID] [int] NOT NULL,
	[AttendanceMarks] [decimal](6, 2) NOT NULL,
	[QuizMarks] [decimal](6, 2) NOT NULL,
	[AssignmentMarks] [decimal](6, 2) NOT NULL,
	[UpdatedAt] [datetime] NULL,
	[UpdatedByUserID] [int] NULL,
 CONSTRAINT [PK_OfferedCourseHomeworkMarkSchemes] PRIMARY KEY CLUSTERED 
(
	[OfferedCourseID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[CourseFolders]    Script Date: 8/25/2026 3:34:38 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[CourseFolders](
	[CourseFolderID] [int] IDENTITY(1,1) NOT NULL,
	[OfferedCourseID] [int] NOT NULL,
	[RootFolderName] [nvarchar](250) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[RootFolderPath] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[CreatedByUserID] [int] NOT NULL,
	[FolderStatus] [nvarchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[CreatedAt] [datetime] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[CourseFolderID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AttendanceRecords]    Script Date: 8/25/2026 3:34:38 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AttendanceRecords](
	[AttendanceID] [int] IDENTITY(1,1) NOT NULL,
	[OfferedCourseID] [int] NOT NULL,
	[StudentID] [int] NOT NULL,
	[AttendanceDate] [date] NOT NULL,
	[Status] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[AttendanceID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AssessmentWeightages]    Script Date: 8/25/2026 3:34:38 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AssessmentWeightages](
	[WeightageID] [int] IDENTITY(1,1) NOT NULL,
	[OfferedCourseID] [int] NOT NULL,
	[AssessmentTypeID] [int] NOT NULL,
	[WeightagePercentage] [decimal](5, 2) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[WeightageID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[CourseAssessmentTypeCLOMappings]    Script Date: 8/25/2026 3:34:38 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[CourseAssessmentTypeCLOMappings](
	[MappingID] [int] IDENTITY(1,1) NOT NULL,
	[ProgramCourseID] [int] NOT NULL,
	[AssessmentTypeID] [int] NOT NULL,
	[CLOID] [int] NOT NULL,
	[Percentage] [decimal](5, 2) NULL,
	[UpdatedByUserID] [int] NULL,
	[SessionID] [int] NULL,
	[ContentStatus] [nvarchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[SubmissionID] [int] NULL,
PRIMARY KEY CLUSTERED 
(
	[MappingID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[CLOAttainmentResults]    Script Date: 8/25/2026 3:34:38 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[CLOAttainmentResults](
	[AttainmentID] [int] IDENTITY(1,1) NOT NULL,
	[OfferedCourseID] [int] NOT NULL,
	[CLOID] [int] NOT NULL,
	[AttainmentPercentage] [decimal](5, 2) NOT NULL,
	[CalculatedAt] [datetime] NOT NULL,
	[CalculatedByUserID] [int] NULL,
PRIMARY KEY CLUSTERED 
(
	[AttainmentID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Assessments]    Script Date: 8/25/2026 3:34:38 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Assessments](
	[AssessmentID] [int] IDENTITY(1,1) NOT NULL,
	[OfferedCourseID] [int] NOT NULL,
	[AssessmentTypeID] [int] NOT NULL,
	[AssessmentNo] [int] NULL,
	[Title] [nvarchar](200) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[DueDate] [date] NULL,
	[TotalMarks] [decimal](6, 2) NULL,
	[CreatedByUserID] [int] NOT NULL,
	[CreatedAt] [datetime] NOT NULL,
	[IsSetupComplete] [bit] NOT NULL,
	[SetupCompletedAt] [datetime] NULL,
PRIMARY KEY CLUSTERED 
(
	[AssessmentID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[PLOCLOMappings]    Script Date: 8/25/2026 3:34:38 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[PLOCLOMappings](
	[MappingID] [int] IDENTITY(1,1) NOT NULL,
	[ProgramCourseID] [int] NOT NULL,
	[PLOID] [int] NOT NULL,
	[CLOID] [int] NOT NULL,
	[Percentage] [decimal](5, 2) NULL,
	[UpdatedByUserID] [int] NULL,
	[SessionID] [int] NOT NULL,
	[ContentStatus] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[SubmissionID] [int] NULL,
PRIMARY KEY CLUSTERED 
(
	[MappingID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[PLOAttainmentResults]    Script Date: 8/25/2026 3:34:38 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[PLOAttainmentResults](
	[AttainmentID] [int] IDENTITY(1,1) NOT NULL,
	[OfferedCourseID] [int] NOT NULL,
	[PLOID] [int] NOT NULL,
	[AttainmentPercentage] [decimal](5, 2) NOT NULL,
	[CalculatedAt] [datetime] NOT NULL,
	[CalculatedByUserID] [int] NULL,
 CONSTRAINT [PK_PLOAttainmentResults] PRIMARY KEY CLUSTERED 
(
	[AttainmentID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[OfferedCourseTopicProgress]    Script Date: 8/25/2026 3:34:38 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[OfferedCourseTopicProgress](
	[ProgressID] [int] IDENTITY(1,1) NOT NULL,
	[OfferedCourseID] [int] NOT NULL,
	[TopicID] [int] NOT NULL,
	[IsCompleted] [bit] NOT NULL,
	[CompletedAt] [datetime] NULL,
	[CompletedByUserID] [int] NULL,
PRIMARY KEY CLUSTERED 
(
	[ProgressID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[CourseTopicCLOMappings]    Script Date: 8/25/2026 3:34:38 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[CourseTopicCLOMappings](
	[TopicCLOMappingID] [int] IDENTITY(1,1) NOT NULL,
	[TopicID] [int] NOT NULL,
	[CLOID] [int] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[TopicCLOMappingID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[StudentCourseResults]    Script Date: 8/25/2026 3:34:38 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[StudentCourseResults](
	[ResultID] [int] IDENTITY(1,1) NOT NULL,
	[OfferedCourseID] [int] NOT NULL,
	[StudentID] [int] NOT NULL,
	[QuizMarks] [decimal](6, 2) NOT NULL,
	[HomeworkMarks] [decimal](6, 2) NOT NULL,
	[MidMarks] [decimal](6, 2) NOT NULL,
	[LabMarks] [decimal](6, 2) NOT NULL,
	[FinalTheoryMarks] [decimal](6, 2) NOT NULL,
	[FinalLabMarks] [decimal](6, 2) NOT NULL,
	[TotalObtained] [decimal](6, 2) NOT NULL,
	[TotalMax] [decimal](6, 2) NOT NULL,
	[Percentage] [decimal](5, 2) NOT NULL,
	[GeneratedAt] [datetime] NOT NULL,
	[GeneratedByUserID] [int] NOT NULL,
 CONSTRAINT [PK_StudentCourseResults] PRIMARY KEY CLUSTERED 
(
	[ResultID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[StudentOfferedCourses]    Script Date: 8/25/2026 3:34:38 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[StudentOfferedCourses](
	[StudentOfferedCourseID] [int] IDENTITY(1,1) NOT NULL,
	[StudentID] [int] NOT NULL,
	[OfferedCourseID] [int] NOT NULL,
 CONSTRAINT [PK_StudentOfferedCourses] PRIMARY KEY CLUSTERED 
(
	[StudentOfferedCourseID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[StudentCourseResultSelections]    Script Date: 8/25/2026 3:34:38 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[StudentCourseResultSelections](
	[SelectionID] [int] IDENTITY(1,1) NOT NULL,
	[OfferedCourseID] [int] NOT NULL,
	[StudentID] [int] NOT NULL,
	[AssessmentID] [int] NOT NULL,
	[Category] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
 CONSTRAINT [PK_StudentCourseResultSelections] PRIMARY KEY CLUSTERED 
(
	[SelectionID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[StudentAssessmentMarks]    Script Date: 8/25/2026 3:34:38 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[StudentAssessmentMarks](
	[StudentAssessmentMarkID] [int] IDENTITY(1,1) NOT NULL,
	[AssessmentID] [int] NOT NULL,
	[StudentID] [int] NOT NULL,
	[ObtainedMarks] [decimal](6, 2) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[StudentAssessmentMarkID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[FolderSubmissions]    Script Date: 8/25/2026 3:34:38 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[FolderSubmissions](
	[SubmissionID] [int] IDENTITY(1,1) NOT NULL,
	[CourseFolderID] [int] NOT NULL,
	[SubmittedByUserID] [int] NOT NULL,
	[SubmissionStatus] [nvarchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[SubmittedAt] [datetime] NOT NULL,
	[Remarks] [nvarchar](500) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[ReviewedByUserID] [int] NULL,
	[ReviewedAt] [datetime] NULL,
	[HodFeedback] [nvarchar](1000) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[VersionNo] [int] NOT NULL,
	[ContentSubmissionID] [int] NULL,
PRIMARY KEY CLUSTERED 
(
	[SubmissionID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AssessmentQuestions]    Script Date: 8/25/2026 3:34:38 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AssessmentQuestions](
	[QuestionID] [int] IDENTITY(1,1) NOT NULL,
	[AssessmentID] [int] NOT NULL,
	[QuestionNo] [nvarchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[QuestionText] [nvarchar](1000) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Marks] [decimal](6, 2) NULL,
	[ParentQuestionID] [int] NULL,
	[DisplayOrder] [int] NULL,
PRIMARY KEY CLUSTERED 
(
	[QuestionID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[CourseFolderDirectories]    Script Date: 8/25/2026 3:34:38 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[CourseFolderDirectories](
	[DirectoryID] [int] IDENTITY(1,1) NOT NULL,
	[CourseFolderID] [int] NOT NULL,
	[ParentDirectoryID] [int] NULL,
	[TemplateItemID] [int] NULL,
	[DirectoryName] [nvarchar](250) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[DirectoryPath] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[DirectoryID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[CourseFolderFiles]    Script Date: 8/25/2026 3:34:38 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[CourseFolderFiles](
	[FileID] [int] IDENTITY(1,1) NOT NULL,
	[DirectoryID] [int] NOT NULL,
	[UploadedByUserID] [int] NOT NULL,
	[FileName] [nvarchar](250) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[FilePath] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[FileCategory] [nvarchar](100) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Remarks] [nvarchar](500) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[UploadedAt] [datetime] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[FileID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AssessmentQuestionCLOMappings]    Script Date: 8/25/2026 3:34:38 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AssessmentQuestionCLOMappings](
	[QuestionCLOMappingID] [int] IDENTITY(1,1) NOT NULL,
	[QuestionID] [int] NOT NULL,
	[CLOID] [int] NOT NULL,
	[Percentage] [decimal](5, 2) NULL,
PRIMARY KEY CLUSTERED 
(
	[QuestionCLOMappingID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[StudentQuestionMarks]    Script Date: 8/25/2026 3:34:38 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[StudentQuestionMarks](
	[StudentQuestionMarkID] [int] IDENTITY(1,1) NOT NULL,
	[QuestionID] [int] NOT NULL,
	[StudentID] [int] NOT NULL,
	[ObtainedMarks] [decimal](6, 2) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[StudentQuestionMarkID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[FolderSubmissionIssues]    Script Date: 8/25/2026 3:34:38 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[FolderSubmissionIssues](
	[IssueID] [int] IDENTITY(1,1) NOT NULL,
	[SubmissionID] [int] NOT NULL,
	[IssueMessage] [nvarchar](500) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[IsResolved] [bit] NOT NULL,
	[CreatedByUserID] [int] NULL,
PRIMARY KEY CLUSTERED 
(
	[IssueID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[LessonPlans]    Script Date: 8/25/2026 3:34:38 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[LessonPlans](
	[LessonPlanID] [int] IDENTITY(1,1) NOT NULL,
	[OfferedCourseID] [int] NOT NULL,
	[LessonPlanNo] [nvarchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[LessonPlanTitle] [nvarchar](200) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Topics] [nvarchar](1000) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[FileID] [int] NULL,
	[UploadedByUserID] [int] NOT NULL,
	[UploadedAt] [datetime] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[LessonPlanID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[GraderTasks]    Script Date: 8/25/2026 3:34:38 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[GraderTasks](
	[GraderTaskID] [int] IDENTITY(1,1) NOT NULL,
	[AssessmentID] [int] NOT NULL,
	[SourceFileID] [int] NULL,
	[AssignedByUserID] [int] NOT NULL,
	[GraderAllocationID] [int] NOT NULL,
	[Instructions] [nvarchar](1000) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[TaskStatus] [nvarchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[AssignedAt] [datetime] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[GraderTaskID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AssessmentFiles]    Script Date: 8/25/2026 3:34:38 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AssessmentFiles](
	[AssessmentFileID] [int] IDENTITY(1,1) NOT NULL,
	[AssessmentID] [int] NOT NULL,
	[FileID] [int] NOT NULL,
	[FilePurpose] [nvarchar](100) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[AssessmentFileID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
SET IDENTITY_INSERT [dbo].[TeacherPermissionTypes] ON 

INSERT [dbo].[TeacherPermissionTypes] ([PermissionTypeID], [PermissionType]) VALUES (1, N'Edit CLOs')
INSERT [dbo].[TeacherPermissionTypes] ([PermissionTypeID], [PermissionType]) VALUES (6, N'Edit Course Content')
INSERT [dbo].[TeacherPermissionTypes] ([PermissionTypeID], [PermissionType]) VALUES (20, N'Manage CLO-PLO Mapping')
INSERT [dbo].[TeacherPermissionTypes] ([PermissionTypeID], [PermissionType]) VALUES (15, N'Manage CLOs')
INSERT [dbo].[TeacherPermissionTypes] ([PermissionTypeID], [PermissionType]) VALUES (21, N'Manage CLO-Task Mapping')
INSERT [dbo].[TeacherPermissionTypes] ([PermissionTypeID], [PermissionType]) VALUES (4, N'Map CLOs to Course')
INSERT [dbo].[TeacherPermissionTypes] ([PermissionTypeID], [PermissionType]) VALUES (3, N'Map CLOs to PLOs')
INSERT [dbo].[TeacherPermissionTypes] ([PermissionTypeID], [PermissionType]) VALUES (14, N'Submit Course Folder')
INSERT [dbo].[TeacherPermissionTypes] ([PermissionTypeID], [PermissionType]) VALUES (9, N'Upload Assignment Quiz Exam Files')
INSERT [dbo].[TeacherPermissionTypes] ([PermissionTypeID], [PermissionType]) VALUES (8, N'Upload Attendance Record')
INSERT [dbo].[TeacherPermissionTypes] ([PermissionTypeID], [PermissionType]) VALUES (2, N'Upload CLO File')
INSERT [dbo].[TeacherPermissionTypes] ([PermissionTypeID], [PermissionType]) VALUES (5, N'Upload Course Content')
INSERT [dbo].[TeacherPermissionTypes] ([PermissionTypeID], [PermissionType]) VALUES (16, N'Upload Exam Questions')
INSERT [dbo].[TeacherPermissionTypes] ([PermissionTypeID], [PermissionType]) VALUES (19, N'Upload Final Exam Questions')
INSERT [dbo].[TeacherPermissionTypes] ([PermissionTypeID], [PermissionType]) VALUES (18, N'Upload Mid Exam Questions')
INSERT [dbo].[TeacherPermissionTypes] ([PermissionTypeID], [PermissionType]) VALUES (13, N'Upload Outcomes Assessment')
INSERT [dbo].[TeacherPermissionTypes] ([PermissionTypeID], [PermissionType]) VALUES (12, N'Upload Results')
INSERT [dbo].[TeacherPermissionTypes] ([PermissionTypeID], [PermissionType]) VALUES (11, N'Upload Samples')
INSERT [dbo].[TeacherPermissionTypes] ([PermissionTypeID], [PermissionType]) VALUES (10, N'Upload Solutions')
INSERT [dbo].[TeacherPermissionTypes] ([PermissionTypeID], [PermissionType]) VALUES (17, N'Upload Weekly Content')
INSERT [dbo].[TeacherPermissionTypes] ([PermissionTypeID], [PermissionType]) VALUES (7, N'Upload Weekly Plan')
SET IDENTITY_INSERT [dbo].[TeacherPermissionTypes] OFF
GO
SET IDENTITY_INSERT [dbo].[Roles] ON 

INSERT [dbo].[Roles] ([RoleID], [RoleName]) VALUES (1, N'Admin')
INSERT [dbo].[Roles] ([RoleID], [RoleName]) VALUES (5, N'Grader')
INSERT [dbo].[Roles] ([RoleID], [RoleName]) VALUES (4, N'HOD')
INSERT [dbo].[Roles] ([RoleID], [RoleName]) VALUES (2, N'SectionAdmin')
INSERT [dbo].[Roles] ([RoleID], [RoleName]) VALUES (3, N'Teacher')
SET IDENTITY_INSERT [dbo].[Roles] OFF
GO
SET IDENTITY_INSERT [dbo].[AssessmentTypes] ON 

INSERT [dbo].[AssessmentTypes] ([AssessmentTypeID], [AssessmentTypeName]) VALUES (1, N'Assignment')
INSERT [dbo].[AssessmentTypes] ([AssessmentTypeID], [AssessmentTypeName]) VALUES (4, N'Final')
INSERT [dbo].[AssessmentTypes] ([AssessmentTypeID], [AssessmentTypeName]) VALUES (3, N'Mid')
INSERT [dbo].[AssessmentTypes] ([AssessmentTypeID], [AssessmentTypeName]) VALUES (2, N'Quiz')
SET IDENTITY_INSERT [dbo].[AssessmentTypes] OFF
GO
SET IDENTITY_INSERT [dbo].[ContentSubmissionTypes] ON 

INSERT [dbo].[ContentSubmissionTypes] ([ContentTypeID], [ContentTypeCode], [ContentTypeName], [SortOrder]) VALUES (1, N'CLOs', N'Course Learning Outcomes', 1)
INSERT [dbo].[ContentSubmissionTypes] ([ContentTypeID], [ContentTypeCode], [ContentTypeName], [SortOrder]) VALUES (2, N'WeeklyContent', N'Weekly / Course Content', 2)
INSERT [dbo].[ContentSubmissionTypes] ([ContentTypeID], [ContentTypeCode], [ContentTypeName], [SortOrder]) VALUES (3, N'CloPloMapping', N'CLO-PLO Mapping', 3)
INSERT [dbo].[ContentSubmissionTypes] ([ContentTypeID], [ContentTypeCode], [ContentTypeName], [SortOrder]) VALUES (4, N'CourseFolder', N'Course Master Folder', 4)
INSERT [dbo].[ContentSubmissionTypes] ([ContentTypeID], [ContentTypeCode], [ContentTypeName], [SortOrder]) VALUES (5, N'CloTaskMapping', N'CLO-Task Mapping', 4)
SET IDENTITY_INSERT [dbo].[ContentSubmissionTypes] OFF
GO
SET IDENTITY_INSERT [dbo].[CourseFolderTemplateItems] ON 

INSERT [dbo].[CourseFolderTemplateItems] ([TemplateItemID], [TemplateCode], [FolderItemName], [SortOrder], [IsRequired]) VALUES (1, N'a', N'Course Objectives', 1, 1)
INSERT [dbo].[CourseFolderTemplateItems] ([TemplateItemID], [TemplateCode], [FolderItemName], [SortOrder], [IsRequired]) VALUES (2, N'b', N'CLO-Task Mapping', 2, 1)
INSERT [dbo].[CourseFolderTemplateItems] ([TemplateItemID], [TemplateCode], [FolderItemName], [SortOrder], [IsRequired]) VALUES (3, N'c', N'Course Contents', 3, 1)
INSERT [dbo].[CourseFolderTemplateItems] ([TemplateItemID], [TemplateCode], [FolderItemName], [SortOrder], [IsRequired]) VALUES (4, N'd', N'Weekly Plan', 4, 1)
INSERT [dbo].[CourseFolderTemplateItems] ([TemplateItemID], [TemplateCode], [FolderItemName], [SortOrder], [IsRequired]) VALUES (5, N'e', N'Attendance Record', 5, 1)
INSERT [dbo].[CourseFolderTemplateItems] ([TemplateItemID], [TemplateCode], [FolderItemName], [SortOrder], [IsRequired]) VALUES (6, N'f', N'Reference Material', 6, 1)
INSERT [dbo].[CourseFolderTemplateItems] ([TemplateItemID], [TemplateCode], [FolderItemName], [SortOrder], [IsRequired]) VALUES (7, N'g', N'Quiz-Assignments-Exams', 7, 1)
INSERT [dbo].[CourseFolderTemplateItems] ([TemplateItemID], [TemplateCode], [FolderItemName], [SortOrder], [IsRequired]) VALUES (8, N'h', N'Solutions', 8, 1)
INSERT [dbo].[CourseFolderTemplateItems] ([TemplateItemID], [TemplateCode], [FolderItemName], [SortOrder], [IsRequired]) VALUES (9, N'i', N'Samples', 9, 1)
INSERT [dbo].[CourseFolderTemplateItems] ([TemplateItemID], [TemplateCode], [FolderItemName], [SortOrder], [IsRequired]) VALUES (10, N'j', N'Marks Distribution and Grading', 10, 1)
INSERT [dbo].[CourseFolderTemplateItems] ([TemplateItemID], [TemplateCode], [FolderItemName], [SortOrder], [IsRequired]) VALUES (11, N'k', N'Result', 11, 1)
INSERT [dbo].[CourseFolderTemplateItems] ([TemplateItemID], [TemplateCode], [FolderItemName], [SortOrder], [IsRequired]) VALUES (12, N'l', N'Outcomes Assessment', 12, 1)
INSERT [dbo].[CourseFolderTemplateItems] ([TemplateItemID], [TemplateCode], [FolderItemName], [SortOrder], [IsRequired]) VALUES (13, N'm', N'Technology Involved', 13, 1)
INSERT [dbo].[CourseFolderTemplateItems] ([TemplateItemID], [TemplateCode], [FolderItemName], [SortOrder], [IsRequired]) VALUES (14, N'n', N'Design Techniques Practiced', 14, 1)
INSERT [dbo].[CourseFolderTemplateItems] ([TemplateItemID], [TemplateCode], [FolderItemName], [SortOrder], [IsRequired]) VALUES (15, N'o', N'Complete Analysis of Course', 15, 1)
SET IDENTITY_INSERT [dbo].[CourseFolderTemplateItems] OFF
GO
SET IDENTITY_INSERT [dbo].[Users] ON 

INSERT [dbo].[Users] ([UserID], [FullName], [Email], [PasswordHash], [RoleID], [Rank], [AridNo]) VALUES (1, N'System Administrator', N'admin@biit.edu.pk', N'AQAAAAIAAYagAAAAEJhJzKqMq+z6JbpmpItGDAijul3TpfIXMDJ6e2DKTV2heEcI83qtKcu/PV2TnKotGw==', 1, N'Administrator', NULL)
INSERT [dbo].[Users] ([UserID], [FullName], [Email], [PasswordHash], [RoleID], [Rank], [AridNo]) VALUES (2, N'Dr. Munir', N'munir@biit.edu.pk', N'AQAAAAIAAYagAAAAEF6g9CsmJXZM8YxEQZ2A9b19w4nYAnfdWImP3BUaxnz74U0aRRktHJrtz4azm1duUA==', 4, N'Deputy Director & HoD CS', NULL)
INSERT [dbo].[Users] ([UserID], [FullName], [Email], [PasswordHash], [RoleID], [Rank], [AridNo]) VALUES (3, N'Dr. Mohsin', N'mohsin@biit.edu.pk', N'AQAAAAIAAYagAAAAEDoWNd+X2ws7ydbMooTJrldAPX7PkKuTk5u/KKL05XlrS9FSiMyjDxXqvqToUv0qNQ==', 4, N'HoD Math & Assistant Professor', NULL)
INSERT [dbo].[Users] ([UserID], [FullName], [Email], [PasswordHash], [RoleID], [Rank], [AridNo]) VALUES (4, N'Qasim', N'qasim@biit.edu.pk', N'AQAAAAIAAYagAAAAEJV2YHvqgUV/H2+kuLsQV44nMDsXOUDscHleq+P0a0a+Nf8GNzA0pOblCo0O2hzM8g==', 3, N'Assistant Professor', NULL)
INSERT [dbo].[Users] ([UserID], [FullName], [Email], [PasswordHash], [RoleID], [Rank], [AridNo]) VALUES (5, N'Dr. Iram', N'iram@biit.edu.pk', N'AQAAAAIAAYagAAAAEFbGljTO1HL5IcBLRPnbfoPT18Zt8ejFoPBiKqoPO7tqYTCUGc54/10c4o/6YP1sTw==', 4, N'HoD Software Engineering & Assistant Professor', NULL)
INSERT [dbo].[Users] ([UserID], [FullName], [Email], [PasswordHash], [RoleID], [Rank], [AridNo]) VALUES (6, N'Dr. Saeed', N'saeed@biit.edu.pk', N'AQAAAAIAAYagAAAAEEArO3rxsizaqvI94Gu/5RnNOoA+EbyH3c38Z2Ws+qNC2yKo9CeoK+gpxuEB4lEXhQ==', 3, N'Assistant Professor', NULL)
INSERT [dbo].[Users] ([UserID], [FullName], [Email], [PasswordHash], [RoleID], [Rank], [AridNo]) VALUES (7, N'Mustafa', N'mustafa@biit.edu.pk', N'AQAAAAIAAYagAAAAEItJBzEbWeqMGD2PAqUr9v3u3iO6uHrcAavgAILT1xEzRi+KLGGN6ys/lSKh0fvsPg==', 3, N'Assistant Professor', NULL)
INSERT [dbo].[Users] ([UserID], [FullName], [Email], [PasswordHash], [RoleID], [Rank], [AridNo]) VALUES (8, N'Abdul Hameed', N'abdul_hameed@biit.edu.pk', N'AQAAAAIAAYagAAAAEB4agw9o8Q+VsYaR6afm4iY/alyLuiqTOhhSEGOJggZt0SnFGme2/LisQtnDCnYC9Q==', 3, N'Lecturer', NULL)
INSERT [dbo].[Users] ([UserID], [FullName], [Email], [PasswordHash], [RoleID], [Rank], [AridNo]) VALUES (9, N'Adeel', N'adeel@biit.edu.pk', N'AQAAAAIAAYagAAAAEOsK6UIEF8XWnq4HvRAQ6xeMjn5IF3EztgijZjzgN3s0oz9EZeK5N5TB90y0DKKRjA==', 3, N'Lecturer', NULL)
INSERT [dbo].[Users] ([UserID], [FullName], [Email], [PasswordHash], [RoleID], [Rank], [AridNo]) VALUES (10, N'Afrasiab', N'afrasiab@biit.edu.pk', N'AQAAAAIAAYagAAAAELxtNnQZPzA/JJINW0PtfYOp1ccux3usqDyEJWndXxxCP1a4un6GwRcq88fLxSCF8g==', 3, N'Lecturer', NULL)
INSERT [dbo].[Users] ([UserID], [FullName], [Email], [PasswordHash], [RoleID], [Rank], [AridNo]) VALUES (11, N'Aftab', N'aftab@biit.edu.pk', N'AQAAAAIAAYagAAAAEPHu7o4FVt24HLPffw8YV9nNEmfHPSo9HSVcgNu+nswDmRcQrQu6NEVJ+kXOrMdXQw==', 3, N'Lecturer', NULL)
INSERT [dbo].[Users] ([UserID], [FullName], [Email], [PasswordHash], [RoleID], [Rank], [AridNo]) VALUES (12, N'Ahsan', N'ahsan@biit.edu.pk', N'AQAAAAIAAYagAAAAEJfic9eiIcpRX9T/oVR5C0J3GHnLzTgz5nXhmY6d2zwvRYDMhy243jt/tuVlLkTfWg==', 3, N'Lecturer', NULL)
INSERT [dbo].[Users] ([UserID], [FullName], [Email], [PasswordHash], [RoleID], [Rank], [AridNo]) VALUES (13, N'Ali', N'ali@biit.edu.pk', N'AQAAAAIAAYagAAAAEF48XglfRuGcozGTE9/JigCMMqnqZQU05TnWkGtLiCLbjcG62KBT/XM+2gHS0Fi47g==', 3, N'Lecturer', NULL)
INSERT [dbo].[Users] ([UserID], [FullName], [Email], [PasswordHash], [RoleID], [Rank], [AridNo]) VALUES (14, N'Amir', N'amir@biit.edu.pk', N'AQAAAAIAAYagAAAAEL0nWWcqQI9oZG2bRObfmso6+tVejBLjXuSHS8K7ENZqpXcivCqEns+WnZffflFz1g==', 3, N'Lecturer', NULL)
INSERT [dbo].[Users] ([UserID], [FullName], [Email], [PasswordHash], [RoleID], [Rank], [AridNo]) VALUES (15, N'Ayub', N'ayub@biit.edu.pk', N'AQAAAAIAAYagAAAAEHKyru4P4Ehci2Ym3SX/NE/flDJhOcOUnP3/ficNCi4QwAlcnNt3OE1+M/+r0q5+6w==', 3, N'Lecturer', NULL)
INSERT [dbo].[Users] ([UserID], [FullName], [Email], [PasswordHash], [RoleID], [Rank], [AridNo]) VALUES (16, N'Azeem', N'azeem@biit.edu.pk', N'AQAAAAIAAYagAAAAEJRltlhS/9XNuuxGOihSJOXWNNA5QAVphffq/pnQVI9dnQ0+wEnuY1izQClyY1A//A==', 3, N'Lecturer', NULL)
INSERT [dbo].[Users] ([UserID], [FullName], [Email], [PasswordHash], [RoleID], [Rank], [AridNo]) VALUES (17, N'Azhar', N'azhar@biit.edu.pk', N'AQAAAAIAAYagAAAAEA7CwOeEeVOFEHv0MY1efvp1nC4IJgiLRRfnfuVWfncz3F168bV4RJyWj6jv+W5rxw==', 3, N'Lecturer', NULL)
INSERT [dbo].[Users] ([UserID], [FullName], [Email], [PasswordHash], [RoleID], [Rank], [AridNo]) VALUES (18, N'Basharat Madani', N'basharat_madani@biit.edu.pk', N'AQAAAAIAAYagAAAAEKePVG6k7I3/5Yi31+Vwh+bRIqv4dr1yGYZt8ndw5nAwN/8CGFvPM5HBXicwsKUTqg==', 3, N'Lecturer', NULL)
INSERT [dbo].[Users] ([UserID], [FullName], [Email], [PasswordHash], [RoleID], [Rank], [AridNo]) VALUES (19, N'Beenish', N'beenish@biit.edu.pk', N'AQAAAAIAAYagAAAAEDqonlJfWoEFghG/WfUPUXBHRlciWVwGEHBIfqaKSavs4aoLCL3+AiwVdii97oijaw==', 3, N'Lecturer', NULL)
INSERT [dbo].[Users] ([UserID], [FullName], [Email], [PasswordHash], [RoleID], [Rank], [AridNo]) VALUES (20, N'Danial', N'danial@biit.edu.pk', N'AQAAAAIAAYagAAAAEDJkIVtwJfcT6z24nRwVvzdyhtveM9auM4Ix87F9JsXxatw17n15z1gDu1DADyf8Wg==', 3, N'Lecturer', NULL)
INSERT [dbo].[Users] ([UserID], [FullName], [Email], [PasswordHash], [RoleID], [Rank], [AridNo]) VALUES (21, N'Dr. Ayesha', N'ayesha@biit.edu.pk', N'AQAAAAIAAYagAAAAEE7KIJENOnauyqHLJA8PcZUbS83mu3DvmKYIIIlllhv/aOLgRG4xg5/g6S1F2cItsA==', 3, N'Lecturer', NULL)
INSERT [dbo].[Users] ([UserID], [FullName], [Email], [PasswordHash], [RoleID], [Rank], [AridNo]) VALUES (22, N'Dr. Naseer', N'naseer@biit.edu.pk', N'AQAAAAIAAYagAAAAEJBJzsgR+6/GtoqrHg0AXXCLYuRFonKIbLLNT/QM6qRvZsGM1m9VgFOJRf+pOB1nKw==', 3, N'Lecturer', NULL)
INSERT [dbo].[Users] ([UserID], [FullName], [Email], [PasswordHash], [RoleID], [Rank], [AridNo]) VALUES (23, N'Dr. Sadaf', N'sadaf@biit.edu.pk', N'AQAAAAIAAYagAAAAEC2NyhBJVPRscn4Y2IB2IQcz1Fp3Q9IiIgyKdy/3VuJ4VoTk6UPsjfWrj8+U0IPvtQ==', 3, N'Lecturer', NULL)
INSERT [dbo].[Users] ([UserID], [FullName], [Email], [PasswordHash], [RoleID], [Rank], [AridNo]) VALUES (24, N'Dr. Zeeshan', N'zeeshan@biit.edu.pk', N'AQAAAAIAAYagAAAAEHk6fvcmMivCmmiS6ChDLq4++7H6Lq5m4fzLJgI3D8F/Eug2+zp7PfxmnoP+udOD8Q==', 3, N'Lecturer', NULL)
INSERT [dbo].[Users] ([UserID], [FullName], [Email], [PasswordHash], [RoleID], [Rank], [AridNo]) VALUES (25, N'Farhana', N'farhana@biit.edu.pk', N'AQAAAAIAAYagAAAAEM4MDoLGlehbKGys3MO2maU+dpf7OPAB2+j7LewdEAW2kbaAdcXeAyrtEnp/R2bMmA==', 3, N'Lecturer', NULL)
INSERT [dbo].[Users] ([UserID], [FullName], [Email], [PasswordHash], [RoleID], [Rank], [AridNo]) VALUES (26, N'Hamza', N'hamza@biit.edu.pk', N'AQAAAAIAAYagAAAAEEDL+Pz1B5KoOkdrQUJXnAZJUJbZZp1RSXxhiO9sQTk7VCIDr5AoYzlCNTgjVy90Hg==', 3, N'Lecturer', NULL)
INSERT [dbo].[Users] ([UserID], [FullName], [Email], [PasswordHash], [RoleID], [Rank], [AridNo]) VALUES (27, N'Hina', N'hina@biit.edu.pk', N'AQAAAAIAAYagAAAAEKzGuwD7N7TsgaD8BFNscFT0YsMesYMfcQSu0TTHD+eDqXNY0ydf7YomSzsbdXTb0w==', 3, N'Lecturer', NULL)
INSERT [dbo].[Users] ([UserID], [FullName], [Email], [PasswordHash], [RoleID], [Rank], [AridNo]) VALUES (28, N'Ihsan', N'ihsan@biit.edu.pk', N'AQAAAAIAAYagAAAAEBk8JkKJW8s967Z66yJYxB4ytnpyrF+6AaR5vhwi+rG3nASAN771Rm/Ppp0DsKboEg==', 3, N'Lecturer', NULL)
INSERT [dbo].[Users] ([UserID], [FullName], [Email], [PasswordHash], [RoleID], [Rank], [AridNo]) VALUES (29, N'Ihtisham', N'ihtisham@biit.edu.pk', N'AQAAAAIAAYagAAAAEPG8wrMqOPK81JZ0xHY0OOMVY3mzz6iBXO6Ab1rpFqODZWPtjmYv63cLD4velVLIBA==', 3, N'Lecturer', NULL)
INSERT [dbo].[Users] ([UserID], [FullName], [Email], [PasswordHash], [RoleID], [Rank], [AridNo]) VALUES (30, N'Iqra', N'iqra@biit.edu.pk', N'AQAAAAIAAYagAAAAEE6882+yZfHcqAZbqLY8VHJ93NwqfByR/oVWleT+IF4RtxRh7GbmzYpDyMJ7Mm5dVA==', 3, N'Lecturer', NULL)
INSERT [dbo].[Users] ([UserID], [FullName], [Email], [PasswordHash], [RoleID], [Rank], [AridNo]) VALUES (31, N'Irfan', N'irfan@biit.edu.pk', N'AQAAAAIAAYagAAAAEKPVngq7PU8Ipk0IP8+MnRs3Ara20y/Gc/TnbdJ6epHtzU+hL4b23Y4BA112aTRvBw==', 3, N'Lecturer', NULL)
INSERT [dbo].[Users] ([UserID], [FullName], [Email], [PasswordHash], [RoleID], [Rank], [AridNo]) VALUES (32, N'Khalid', N'khalid@biit.edu.pk', N'AQAAAAIAAYagAAAAEFHvPj9m5IDBYSHrqXn52AJvrQ/BrckRBScZSSbIXSk2Z899LKwI1rshIqgY3P5gjQ==', 3, N'Lecturer', NULL)
INSERT [dbo].[Users] ([UserID], [FullName], [Email], [PasswordHash], [RoleID], [Rank], [AridNo]) VALUES (33, N'Khalid Zaman', N'khalid_zaman@biit.edu.pk', N'AQAAAAIAAYagAAAAEN7XL/lZIdaPmDZwHjx6vZBWDm+O8fo1Ja93qxiAMUjJZ5+CXMiosuNQP7Me/jW+sg==', 3, N'Lecturer', NULL)
INSERT [dbo].[Users] ([UserID], [FullName], [Email], [PasswordHash], [RoleID], [Rank], [AridNo]) VALUES (34, N'M.Shabir', N'mshabir@biit.edu.pk', N'AQAAAAIAAYagAAAAEIXf/khfPY6dzgtuHBlaUShG0sih2w1QrcBRn4Yf8ZtCvCw8lz2/gETGxdpdS0LTSg==', 3, N'Lecturer', NULL)
INSERT [dbo].[Users] ([UserID], [FullName], [Email], [PasswordHash], [RoleID], [Rank], [AridNo]) VALUES (35, N'Nadia', N'nadia@biit.edu.pk', N'AQAAAAIAAYagAAAAEH5163kgwFGBHZyZJRz+3zFZlkgL8dQf41cKLsoqZLw2hO8UWSgQaZRHEcJzAorYuw==', 3, N'Lecturer', NULL)
INSERT [dbo].[Users] ([UserID], [FullName], [Email], [PasswordHash], [RoleID], [Rank], [AridNo]) VALUES (36, N'Naeem Ullah Farooq', N'naeem_farooq@biit.edu.pk', N'AQAAAAIAAYagAAAAEAtIJ26CPasNOvLHEih9jGZm20+xl5McxBDIz34Lt32rXYKxvn51WWfPKuVnhAmJBQ==', 3, N'Lecturer', NULL)
INSERT [dbo].[Users] ([UserID], [FullName], [Email], [PasswordHash], [RoleID], [Rank], [AridNo]) VALUES (37, N'Noman', N'noman@biit.edu.pk', N'AQAAAAIAAYagAAAAECh1zUd3ZyA0KqLoDZchPRvS8Dgl+Mhu8hmfGQuUk/+ySr9xi/alvUeXzsM9vS5N8g==', 3, N'Lecturer', NULL)
INSERT [dbo].[Users] ([UserID], [FullName], [Email], [PasswordHash], [RoleID], [Rank], [AridNo]) VALUES (38, N'Noor', N'noor@biit.edu.pk', N'AQAAAAIAAYagAAAAENYK8hTivwlarQ5dAHfMH0UTUJ57dwYhIEJ/R8PTfWBDYjQpYoGBA1qv0/7iB7vlvg==', 3, N'Lecturer', NULL)
INSERT [dbo].[Users] ([UserID], [FullName], [Email], [PasswordHash], [RoleID], [Rank], [AridNo]) VALUES (39, N'Rizwan', N'rizwan@biit.edu.pk', N'AQAAAAIAAYagAAAAEGyWIt3ZA6GVsU6FAe0hU5xOLoXfmCuUSS4Ptmg/bupkrBoiH/Xy4hltCJW17bHJOQ==', 3, N'Lecturer', NULL)
INSERT [dbo].[Users] ([UserID], [FullName], [Email], [PasswordHash], [RoleID], [Rank], [AridNo]) VALUES (40, N'Roma', N'roma@biit.edu.pk', N'AQAAAAIAAYagAAAAEHSujzJqWaBNnPrcYG2h2gfZG6HngdXvvP1cjxDFAAAXkW4yBDt/9udjeaf7EV4pAw==', 3, N'Lecturer', NULL)
INSERT [dbo].[Users] ([UserID], [FullName], [Email], [PasswordHash], [RoleID], [Rank], [AridNo]) VALUES (41, N'Sadia Murtaza', N'sadia_murtaza@biit.edu.pk', N'AQAAAAIAAYagAAAAEPlLrsXH5T6V/YPjx7YdAuRVAnAcWuH2CKhZVlfvmzfLl/qq/tSWcXE7YqgZW54v8w==', 3, N'Lecturer', NULL)
INSERT [dbo].[Users] ([UserID], [FullName], [Email], [PasswordHash], [RoleID], [Rank], [AridNo]) VALUES (42, N'Sadiqa', N'sadiqa@biit.edu.pk', N'AQAAAAIAAYagAAAAECHMS3lAIxIRUyfeCtQJTpZj1hTlG7h28dLmCXf0FQ35ntYfWHLc/0Z3XI4UDOwlig==', 3, N'Lecturer', NULL)
INSERT [dbo].[Users] ([UserID], [FullName], [Email], [PasswordHash], [RoleID], [Rank], [AridNo]) VALUES (43, N'Shafiq', N'shafiq@biit.edu.pk', N'AQAAAAIAAYagAAAAEEoq+42gJ06kmMTvMr2btvbOcUwdzVV+PUL3E+4+tRy3PD4O0xxeu0BbUqJAnW0pmQ==', 3, N'Lecturer', NULL)
INSERT [dbo].[Users] ([UserID], [FullName], [Email], [PasswordHash], [RoleID], [Rank], [AridNo]) VALUES (44, N'Shafqat', N'shafqat@biit.edu.pk', N'AQAAAAIAAYagAAAAED1WtUsboXzz+gXV/6+6ygX+dxyN6lQfByt8ynPe4eKiOZ2bc173Z/2OB6wSKVfHfw==', 3, N'Lecturer', NULL)
INSERT [dbo].[Users] ([UserID], [FullName], [Email], [PasswordHash], [RoleID], [Rank], [AridNo]) VALUES (45, N'Shahid Abid', N'shahid_abid@biit.edu.pk', N'AQAAAAIAAYagAAAAEPaJRuQtNyL/VTxzYSJS+9NLLy/6kW/WGdlyUBNgWeevpPrOybqyCz6TdR+PjqIXTg==', 3, N'Lecturer', NULL)
INSERT [dbo].[Users] ([UserID], [FullName], [Email], [PasswordHash], [RoleID], [Rank], [AridNo]) VALUES (46, N'Shahid Jamil', N'shahid_jamil@biit.edu.pk', N'AQAAAAIAAYagAAAAEFTLZxqL4MFIMt4uOCx5SSxGeJrGbbM1BIGLsjOpOTm2+hZ4uDK1KbgahOaEEsav6A==', 3, N'Lecturer', NULL)
INSERT [dbo].[Users] ([UserID], [FullName], [Email], [PasswordHash], [RoleID], [Rank], [AridNo]) VALUES (47, N'Shahid Rashid', N'shahid_rashid@biit.edu.pk', N'AQAAAAIAAYagAAAAEBuKzU4je2Ki3bDa3SL6zGMU+BqiAon4XnQx+69AMnBRMYW97fF0OvdEwr9Gto760A==', 3, N'Lecturer', NULL)
INSERT [dbo].[Users] ([UserID], [FullName], [Email], [PasswordHash], [RoleID], [Rank], [AridNo]) VALUES (48, N'Sohail', N'sohail@biit.edu.pk', N'AQAAAAIAAYagAAAAEKg88zLncTfg46PxY869Fmop0rV9W9J2wScbRCtqZSU1OMxjWFHSaLr/K9wzzuZ7kA==', 3, N'Lecturer', NULL)
INSERT [dbo].[Users] ([UserID], [FullName], [Email], [PasswordHash], [RoleID], [Rank], [AridNo]) VALUES (49, N'Sumaira', N'sumaira@biit.edu.pk', N'AQAAAAIAAYagAAAAEJ0Xn2CzwfN2EfHuUSrNk05jgFi1tj+r3hn8xGw0c+a6d0F60yrctnXb3dO39glVkw==', 3, N'Lecturer', NULL)
INSERT [dbo].[Users] ([UserID], [FullName], [Email], [PasswordHash], [RoleID], [Rank], [AridNo]) VALUES (50, N'Summaya', N'summaya@biit.edu.pk', N'AQAAAAIAAYagAAAAEBtG8t6/qqNQdJ0ShQebLS1Nu9SbeXUI8S+sRA7crbR5wgK1Kt8aAO6Eo+1SNNlyVA==', 3, N'Lecturer', NULL)
INSERT [dbo].[Users] ([UserID], [FullName], [Email], [PasswordHash], [RoleID], [Rank], [AridNo]) VALUES (51, N'Tayyaba', N'tayyaba@biit.edu.pk', N'AQAAAAIAAYagAAAAEIhDeZUj56O0PkQxW8sDUQrbCZvvcnJlHEEK6HQTweVFdi50RboKZb0knyjCAo5Odw==', 3, N'Lecturer', NULL)
INSERT [dbo].[Users] ([UserID], [FullName], [Email], [PasswordHash], [RoleID], [Rank], [AridNo]) VALUES (52, N'Umer', N'umer@biit.edu.pk', N'AQAAAAIAAYagAAAAEG085flXFTqEgc4JMYACnTDVn+7O+oYBwzgamQ/k/FdzbzxmAas+xcGt9XlV8Ckw6Q==', 3, N'Lecturer', NULL)
INSERT [dbo].[Users] ([UserID], [FullName], [Email], [PasswordHash], [RoleID], [Rank], [AridNo]) VALUES (53, N'Waseem', N'waseem@biit.edu.pk', N'AQAAAAIAAYagAAAAEDCXlTOulJTDf+QCwHEnhtlb00/6uPgm+1rpODI9q+p0cWqNs1tOBCazyFRhceK45Q==', 3, N'Lecturer', NULL)
INSERT [dbo].[Users] ([UserID], [FullName], [Email], [PasswordHash], [RoleID], [Rank], [AridNo]) VALUES (54, N'Zahid', N'zahid@biit.edu.pk', N'AQAAAAIAAYagAAAAEHgUjacCm9QspMvAKnD+5sXGozGIxJIWyZOIc71+SpYm6zBftt91OS0yYTxoOAPs0w==', 3, N'Lecturer', NULL)
INSERT [dbo].[Users] ([UserID], [FullName], [Email], [PasswordHash], [RoleID], [Rank], [AridNo]) VALUES (55, N'Zahida', N'zahida@biit.edu.pk', N'AQAAAAIAAYagAAAAEJKaaTvzpobmG9P0vvOr3svDNO8r/zjenZxWRaTua8ldALaRqZiVQptJvMNOr25CLw==', 3, N'Lecturer', NULL)
INSERT [dbo].[Users] ([UserID], [FullName], [Email], [PasswordHash], [RoleID], [Rank], [AridNo]) VALUES (56, N'ZerAfshan', N'zerafshan@biit.edu.pk', N'AQAAAAIAAYagAAAAEHUgSU058fXXyK3VXOD2SuLVvwuPVlxJFJQIkCCiPbWtcGw23cZjIS3xGiZjL3eT8Q==', 3, N'Lecturer', NULL)
INSERT [dbo].[Users] ([UserID], [FullName], [Email], [PasswordHash], [RoleID], [Rank], [AridNo]) VALUES (57, N'Zohaib', N'zohaib@biit.edu.pk', N'AQAAAAIAAYagAAAAEA7wGaKt/gNFcl/x8eBSSMHxSF9z0LF+L+uc+J4SunarP3KLyDg88q8qmEvX793AnA==', 3, N'Lecturer', NULL)
INSERT [dbo].[Users] ([UserID], [FullName], [Email], [PasswordHash], [RoleID], [Rank], [AridNo]) VALUES (58, N'Second Administrator', N'sectionadmin@biit.edu.pk', N'AQAAAAIAAYagAAAAEOUBvtDuYVx2GWyTW62YvULYN/6HgF6Q7aOOu/4LCDGdcBuBGfbe99nQf7+JLMLjLQ==', 1, N'Administrator', NULL)
SET IDENTITY_INSERT [dbo].[Users] OFF
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UQ__TeacherP__F43A2A63405D37A8]    Script Date: 8/25/2026 3:34:49 PM ******/
ALTER TABLE [dbo].[TeacherPermissionTypes] ADD UNIQUE NONCLUSTERED 
(
	[PermissionType] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UQ__Roles__8A2B616048CEF89B]    Script Date: 8/25/2026 3:34:49 PM ******/
ALTER TABLE [dbo].[Roles] ADD UNIQUE NONCLUSTERED 
(
	[RoleName] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UQ_PEOPLEMappings_PEO_PLO]    Script Date: 8/25/2026 3:34:49 PM ******/
CREATE UNIQUE NONCLUSTERED INDEX [UQ_PEOPLEMappings_PEO_PLO] ON [dbo].[PEOPLEMappings]
(
	[PEOID] ASC,
	[PLOID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UQ__Assessme__480D14F553949F2B]    Script Date: 8/25/2026 3:34:49 PM ******/
ALTER TABLE [dbo].[AssessmentTypes] ADD UNIQUE NONCLUSTERED 
(
	[AssessmentTypeName] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UQ__ContentS__BEC69DA4D40F704E]    Script Date: 8/25/2026 3:34:49 PM ******/
ALTER TABLE [dbo].[ContentSubmissionTypes] ADD UNIQUE NONCLUSTERED 
(
	[ContentTypeCode] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UQ__CourseFo__0FDB50816303089A]    Script Date: 8/25/2026 3:34:49 PM ******/
ALTER TABLE [dbo].[CourseFolderTemplateItems] ADD UNIQUE NONCLUSTERED 
(
	[TemplateCode] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UQ__CourseFo__4EF99A60BB6CC033]    Script Date: 8/25/2026 3:34:49 PM ******/
ALTER TABLE [dbo].[CourseFolderTemplateItems] ADD UNIQUE NONCLUSTERED 
(
	[FolderItemName] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UQ__CourseFo__55A193B7B50E9941]    Script Date: 8/25/2026 3:34:49 PM ******/
ALTER TABLE [dbo].[CourseFolderTemplateItems] ADD UNIQUE NONCLUSTERED 
(
	[SortOrder] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UQ__Academic__19B7B721430939FC]    Script Date: 8/25/2026 3:34:49 PM ******/
ALTER TABLE [dbo].[AcademicProgramCourses] ADD UNIQUE NONCLUSTERED 
(
	[ProgramID] ASC,
	[CourseID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UQ__Users__A9D10534BC5E061E]    Script Date: 8/25/2026 3:34:49 PM ******/
ALTER TABLE [dbo].[Users] ADD UNIQUE NONCLUSTERED 
(
	[Email] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Notifications_User_Unread]    Script Date: 8/25/2026 3:34:49 PM ******/
CREATE NONCLUSTERED INDEX [IX_Notifications_User_Unread] ON [dbo].[Notifications]
(
	[UserID] ASC,
	[IsRead] ASC,
	[CreatedAt] DESC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UQ__ProgramC__BC9B98F37B4AE5D6]    Script Date: 8/25/2026 3:34:49 PM ******/
ALTER TABLE [dbo].[ProgramCoursePLOMappings] ADD UNIQUE NONCLUSTERED 
(
	[ProgramCourseID] ASC,
	[PLOID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UQ__TeacherP__BB5329FA23F1DA3A]    Script Date: 8/25/2026 3:34:49 PM ******/
ALTER TABLE [dbo].[TeacherPermissions] ADD UNIQUE NONCLUSTERED 
(
	[TeacherUserID] ASC,
	[ProgramCourseID] ASC,
	[PermissionTypeID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UQ_Sections_Session_SectionName]    Script Date: 8/25/2026 3:34:49 PM ******/
CREATE UNIQUE NONCLUSTERED INDEX [UQ_Sections_Session_SectionName] ON [dbo].[Sections]
(
	[SessionID] ASC,
	[SectionName] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UQ_CourseTopics_ProgramCourse_Session_Parent_Title]    Script Date: 8/25/2026 3:34:49 PM ******/
CREATE UNIQUE NONCLUSTERED INDEX [UQ_CourseTopics_ProgramCourse_Session_Parent_Title] ON [dbo].[CourseTopics]
(
	[ProgramCourseID] ASC,
	[SessionID] ASC,
	[ParentTopicID] ASC,
	[TopicTitle] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UQ_CourseTopics_Session]    Script Date: 8/25/2026 3:34:49 PM ******/
CREATE UNIQUE NONCLUSTERED INDEX [UQ_CourseTopics_Session] ON [dbo].[CourseTopics]
(
	[ProgramCourseID] ASC,
	[SessionID] ASC,
	[ParentTopicID] ASC,
	[TopicTitle] ASC
)
WHERE ([SessionID] IS NOT NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UQ_CLOs_ProgramCourse_Session_Code]    Script Date: 8/25/2026 3:34:49 PM ******/
CREATE UNIQUE NONCLUSTERED INDEX [UQ_CLOs_ProgramCourse_Session_Code] ON [dbo].[CLOs]
(
	[ProgramCourseID] ASC,
	[SessionID] ASC,
	[CLOCode] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UQ_CLOs_Session_Code]    Script Date: 8/25/2026 3:34:49 PM ******/
CREATE UNIQUE NONCLUSTERED INDEX [UQ_CLOs_Session_Code] ON [dbo].[CLOs]
(
	[ProgramCourseID] ASC,
	[SessionID] ASC,
	[CLOCode] ASC
)
WHERE ([SessionID] IS NOT NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UQ_OfferedCourses_Section_Course]    Script Date: 8/25/2026 3:34:49 PM ******/
CREATE UNIQUE NONCLUSTERED INDEX [UQ_OfferedCourses_Section_Course] ON [dbo].[OfferedCourses]
(
	[SectionID] ASC,
	[CourseID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UQ_OfferedCourseResultSchemes_OfferedCourse]    Script Date: 8/25/2026 3:34:49 PM ******/
ALTER TABLE [dbo].[OfferedCourseResultSchemes] ADD  CONSTRAINT [UQ_OfferedCourseResultSchemes_OfferedCourse] UNIQUE NONCLUSTERED 
(
	[OfferedCourseID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UQ__CourseFo__94C9F8E3277A91F5]    Script Date: 8/25/2026 3:34:49 PM ******/
ALTER TABLE [dbo].[CourseFolders] ADD UNIQUE NONCLUSTERED 
(
	[OfferedCourseID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UQ__CourseFo__F12009065DDD1641]    Script Date: 8/25/2026 3:34:49 PM ******/
ALTER TABLE [dbo].[CourseFolders] ADD UNIQUE NONCLUSTERED 
(
	[RootFolderPath] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UQ__Attendan__E73205C382323D44]    Script Date: 8/25/2026 3:34:49 PM ******/
ALTER TABLE [dbo].[AttendanceRecords] ADD UNIQUE NONCLUSTERED 
(
	[OfferedCourseID] ASC,
	[StudentID] ASC,
	[AttendanceDate] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UQ__Assessme__97FF75962542B52B]    Script Date: 8/25/2026 3:34:49 PM ******/
ALTER TABLE [dbo].[AssessmentWeightages] ADD UNIQUE NONCLUSTERED 
(
	[OfferedCourseID] ASC,
	[AssessmentTypeID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UQ_CourseAssessmentTypeCLOMappings_Session]    Script Date: 8/25/2026 3:34:49 PM ******/
CREATE UNIQUE NONCLUSTERED INDEX [UQ_CourseAssessmentTypeCLOMappings_Session] ON [dbo].[CourseAssessmentTypeCLOMappings]
(
	[ProgramCourseID] ASC,
	[SessionID] ASC,
	[AssessmentTypeID] ASC,
	[CLOID] ASC
)
WHERE ([SessionID] IS NOT NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UQ__CLOAttai__05058919E1362A58]    Script Date: 8/25/2026 3:34:49 PM ******/
ALTER TABLE [dbo].[CLOAttainmentResults] ADD UNIQUE NONCLUSTERED 
(
	[OfferedCourseID] ASC,
	[CLOID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UQ__Assessme__13C25EC492BEBAD5]    Script Date: 8/25/2026 3:34:49 PM ******/
ALTER TABLE [dbo].[Assessments] ADD UNIQUE NONCLUSTERED 
(
	[OfferedCourseID] ASC,
	[AssessmentTypeID] ASC,
	[AssessmentNo] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UQ_PLOCLO_ProgramCourse_Session_PLO_CLO]    Script Date: 8/25/2026 3:34:49 PM ******/
CREATE UNIQUE NONCLUSTERED INDEX [UQ_PLOCLO_ProgramCourse_Session_PLO_CLO] ON [dbo].[PLOCLOMappings]
(
	[ProgramCourseID] ASC,
	[SessionID] ASC,
	[PLOID] ASC,
	[CLOID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UQ_PLOCLOMappings_Session]    Script Date: 8/25/2026 3:34:49 PM ******/
CREATE UNIQUE NONCLUSTERED INDEX [UQ_PLOCLOMappings_Session] ON [dbo].[PLOCLOMappings]
(
	[ProgramCourseID] ASC,
	[SessionID] ASC,
	[PLOID] ASC,
	[CLOID] ASC
)
WHERE ([SessionID] IS NOT NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UQ_PLOAttainmentResults_OfferedCourse_PLO]    Script Date: 8/25/2026 3:34:49 PM ******/
ALTER TABLE [dbo].[PLOAttainmentResults] ADD  CONSTRAINT [UQ_PLOAttainmentResults_OfferedCourse_PLO] UNIQUE NONCLUSTERED 
(
	[OfferedCourseID] ASC,
	[PLOID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UQ__OfferedC__54EB18146A4CC299]    Script Date: 8/25/2026 3:34:49 PM ******/
ALTER TABLE [dbo].[OfferedCourseTopicProgress] ADD UNIQUE NONCLUSTERED 
(
	[OfferedCourseID] ASC,
	[TopicID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UQ__CourseTo__93E27E8685EA21FD]    Script Date: 8/25/2026 3:34:49 PM ******/
ALTER TABLE [dbo].[CourseTopicCLOMappings] ADD UNIQUE NONCLUSTERED 
(
	[TopicID] ASC,
	[CLOID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UQ_StudentCourseResults_Offering_Student]    Script Date: 8/25/2026 3:34:49 PM ******/
ALTER TABLE [dbo].[StudentCourseResults] ADD  CONSTRAINT [UQ_StudentCourseResults_Offering_Student] UNIQUE NONCLUSTERED 
(
	[OfferedCourseID] ASC,
	[StudentID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UQ_StudentOfferedCourses_Student_Offered]    Script Date: 8/25/2026 3:34:49 PM ******/
ALTER TABLE [dbo].[StudentOfferedCourses] ADD  CONSTRAINT [UQ_StudentOfferedCourses_Student_Offered] UNIQUE NONCLUSTERED 
(
	[StudentID] ASC,
	[OfferedCourseID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UQ_StudentCourseResultSelections]    Script Date: 8/25/2026 3:34:49 PM ******/
ALTER TABLE [dbo].[StudentCourseResultSelections] ADD  CONSTRAINT [UQ_StudentCourseResultSelections] UNIQUE NONCLUSTERED 
(
	[OfferedCourseID] ASC,
	[StudentID] ASC,
	[AssessmentID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UQ__StudentA__BE07AA98AD93FF00]    Script Date: 8/25/2026 3:34:49 PM ******/
ALTER TABLE [dbo].[StudentAssessmentMarks] ADD UNIQUE NONCLUSTERED 
(
	[AssessmentID] ASC,
	[StudentID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UQ__Assessme__CDF7F36BCDDF38EC]    Script Date: 8/25/2026 3:34:49 PM ******/
ALTER TABLE [dbo].[AssessmentQuestions] ADD UNIQUE NONCLUSTERED 
(
	[AssessmentID] ASC,
	[QuestionNo] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UQ__CourseFo__1C74B39D0664A804]    Script Date: 8/25/2026 3:34:49 PM ******/
ALTER TABLE [dbo].[CourseFolderDirectories] ADD UNIQUE NONCLUSTERED 
(
	[CourseFolderID] ASC,
	[DirectoryPath] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UQ_CourseFolderDirectories_Template]    Script Date: 8/25/2026 3:34:49 PM ******/
CREATE UNIQUE NONCLUSTERED INDEX [UQ_CourseFolderDirectories_Template] ON [dbo].[CourseFolderDirectories]
(
	[CourseFolderID] ASC,
	[TemplateItemID] ASC
)
WHERE ([TemplateItemID] IS NOT NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UQ__CourseFo__48D910BDF277FFBD]    Script Date: 8/25/2026 3:34:49 PM ******/
ALTER TABLE [dbo].[CourseFolderFiles] ADD UNIQUE NONCLUSTERED 
(
	[FilePath] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UQ__CourseFo__F81A09ED14F0FD2B]    Script Date: 8/25/2026 3:34:49 PM ******/
ALTER TABLE [dbo].[CourseFolderFiles] ADD UNIQUE NONCLUSTERED 
(
	[DirectoryID] ASC,
	[FileName] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UQ__Assessme__9C0C1E77FAFCAFB7]    Script Date: 8/25/2026 3:34:49 PM ******/
ALTER TABLE [dbo].[AssessmentQuestionCLOMappings] ADD UNIQUE NONCLUSTERED 
(
	[QuestionID] ASC,
	[CLOID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UQ__StudentQ__8EEC3D2AE7A1A877]    Script Date: 8/25/2026 3:34:49 PM ******/
ALTER TABLE [dbo].[StudentQuestionMarks] ADD UNIQUE NONCLUSTERED 
(
	[QuestionID] ASC,
	[StudentID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UQ__LessonPl__E4EB525184D65FAA]    Script Date: 8/25/2026 3:34:49 PM ******/
ALTER TABLE [dbo].[LessonPlans] ADD UNIQUE NONCLUSTERED 
(
	[OfferedCourseID] ASC,
	[LessonPlanNo] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UQ__Assessme__01C81E1B1B7C9042]    Script Date: 8/25/2026 3:34:49 PM ******/
ALTER TABLE [dbo].[AssessmentFiles] ADD UNIQUE NONCLUSTERED 
(
	[AssessmentID] ASC,
	[FileID] ASC,
	[FilePurpose] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
ALTER TABLE [dbo].[PEOPLEMappings] ADD  CONSTRAINT [DF_PEOPLEMappings_IsMapped]  DEFAULT ((1)) FOR [IsMapped]
GO
ALTER TABLE [dbo].[CourseFolderTemplateItems] ADD  DEFAULT ((1)) FOR [IsRequired]
GO
ALTER TABLE [dbo].[Notifications] ADD  DEFAULT ((0)) FOR [IsRead]
GO
ALTER TABLE [dbo].[Notifications] ADD  DEFAULT (getdate()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[ProgramCourseContentSubmissions] ADD  DEFAULT (getdate()) FOR [SubmittedAt]
GO
ALTER TABLE [dbo].[ProgramCourseContentSubmissions] ADD  DEFAULT ('Submitted') FOR [SubmissionStatus]
GO
ALTER TABLE [dbo].[ProgramCourseContentSubmissions] ADD  DEFAULT ((1)) FOR [VersionNo]
GO
ALTER TABLE [dbo].[ProgramCourseContentSubmissions] ADD  DEFAULT ((0)) FOR [IsCurrentApproved]
GO
ALTER TABLE [dbo].[TeacherPermissions] ADD  DEFAULT ((0)) FOR [IsAllowed]
GO
ALTER TABLE [dbo].[CourseTopics] ADD  DEFAULT ('Draft') FOR [ContentStatus]
GO
ALTER TABLE [dbo].[CLOs] ADD  DEFAULT ('Draft') FOR [ContentStatus]
GO
ALTER TABLE [dbo].[OfferedCourseResultSchemes] ADD  CONSTRAINT [DF_OfferedCourseResultSchemes_Attendance]  DEFAULT ((0)) FOR [AttendanceHomeworkMarks]
GO
ALTER TABLE [dbo].[OfferedCourseLabMarkSchemes] ADD  CONSTRAINT [DF_LabMarkSchemes_Quiz]  DEFAULT ((0)) FOR [QuizMarks]
GO
ALTER TABLE [dbo].[OfferedCourseLabMarkSchemes] ADD  CONSTRAINT [DF_LabMarkSchemes_Assignment]  DEFAULT ((0)) FOR [AssignmentMarks]
GO
ALTER TABLE [dbo].[OfferedCourseLabMarkSchemes] ADD  CONSTRAINT [DF_LabMarkSchemes_LabTask]  DEFAULT ((0)) FOR [LabTaskMarks]
GO
ALTER TABLE [dbo].[OfferedCourseLabMarkSchemes] ADD  CONSTRAINT [DF_LabMarkSchemes_Project]  DEFAULT ((0)) FOR [ProjectMarks]
GO
ALTER TABLE [dbo].[OfferedCourseLabMarkSchemes] ADD  CONSTRAINT [DF_LabMarkSchemes_Attendance]  DEFAULT ((0)) FOR [AttendanceMarks]
GO
ALTER TABLE [dbo].[OfferedCourseLabMarkSchemes] ADD  CONSTRAINT [DF_LabMarkSchemes_Exam]  DEFAULT ((0)) FOR [ExamMarks]
GO
ALTER TABLE [dbo].[OfferedCourseHomeworkMarkSchemes] ADD  CONSTRAINT [DF_HwMarkSchemes_Attendance]  DEFAULT ((0)) FOR [AttendanceMarks]
GO
ALTER TABLE [dbo].[OfferedCourseHomeworkMarkSchemes] ADD  CONSTRAINT [DF_HwMarkSchemes_Quiz]  DEFAULT ((0)) FOR [QuizMarks]
GO
ALTER TABLE [dbo].[OfferedCourseHomeworkMarkSchemes] ADD  CONSTRAINT [DF_HwMarkSchemes_Assignment]  DEFAULT ((0)) FOR [AssignmentMarks]
GO
ALTER TABLE [dbo].[CourseFolders] ADD  DEFAULT ('In Progress') FOR [FolderStatus]
GO
ALTER TABLE [dbo].[CourseFolders] ADD  DEFAULT (getdate()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[CourseAssessmentTypeCLOMappings] ADD  CONSTRAINT [DF_CourseAssessmentTypeCLOMappings_ContentStatus]  DEFAULT ('Draft') FOR [ContentStatus]
GO
ALTER TABLE [dbo].[CLOAttainmentResults] ADD  DEFAULT (getdate()) FOR [CalculatedAt]
GO
ALTER TABLE [dbo].[Assessments] ADD  DEFAULT (getdate()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[Assessments] ADD  CONSTRAINT [DF_Assessments_IsSetupComplete]  DEFAULT ((0)) FOR [IsSetupComplete]
GO
ALTER TABLE [dbo].[PLOCLOMappings] ADD  DEFAULT ('Draft') FOR [ContentStatus]
GO
ALTER TABLE [dbo].[PLOAttainmentResults] ADD  CONSTRAINT [DF_PLOAttainmentResults_CalculatedAt]  DEFAULT (getdate()) FOR [CalculatedAt]
GO
ALTER TABLE [dbo].[OfferedCourseTopicProgress] ADD  DEFAULT ((0)) FOR [IsCompleted]
GO
ALTER TABLE [dbo].[FolderSubmissions] ADD  DEFAULT ('Submitted') FOR [SubmissionStatus]
GO
ALTER TABLE [dbo].[FolderSubmissions] ADD  DEFAULT (getdate()) FOR [SubmittedAt]
GO
ALTER TABLE [dbo].[FolderSubmissions] ADD  DEFAULT ((1)) FOR [VersionNo]
GO
ALTER TABLE [dbo].[CourseFolderFiles] ADD  DEFAULT ('Other') FOR [FileCategory]
GO
ALTER TABLE [dbo].[CourseFolderFiles] ADD  DEFAULT (getdate()) FOR [UploadedAt]
GO
ALTER TABLE [dbo].[FolderSubmissionIssues] ADD  DEFAULT ((0)) FOR [IsResolved]
GO
ALTER TABLE [dbo].[LessonPlans] ADD  DEFAULT (getdate()) FOR [UploadedAt]
GO
ALTER TABLE [dbo].[GraderTasks] ADD  DEFAULT ('Pending') FOR [TaskStatus]
GO
ALTER TABLE [dbo].[GraderTasks] ADD  DEFAULT (getdate()) FOR [AssignedAt]
GO
ALTER TABLE [dbo].[BookImage]  WITH CHECK ADD FOREIGN KEY([ISBN])
REFERENCES [dbo].[Book] ([ISBN])
GO
ALTER TABLE [dbo].[AcademicSessions]  WITH CHECK ADD FOREIGN KEY([ProgramID])
REFERENCES [dbo].[AcademicPrograms] ([ProgramID])
GO
ALTER TABLE [dbo].[AcademicProgramCourses]  WITH CHECK ADD FOREIGN KEY([CourseID])
REFERENCES [dbo].[Courses] ([CourseID])
GO
ALTER TABLE [dbo].[AcademicProgramCourses]  WITH CHECK ADD FOREIGN KEY([ProgramID])
REFERENCES [dbo].[AcademicPrograms] ([ProgramID])
GO
ALTER TABLE [dbo].[Users]  WITH CHECK ADD FOREIGN KEY([RoleID])
REFERENCES [dbo].[Roles] ([RoleID])
GO
ALTER TABLE [dbo].[PEOs]  WITH CHECK ADD FOREIGN KEY([ProgramID])
REFERENCES [dbo].[AcademicPrograms] ([ProgramID])
GO
ALTER TABLE [dbo].[PLOs]  WITH CHECK ADD FOREIGN KEY([ProgramID])
REFERENCES [dbo].[AcademicPrograms] ([ProgramID])
GO
ALTER TABLE [dbo].[Notifications]  WITH CHECK ADD FOREIGN KEY([UserID])
REFERENCES [dbo].[Users] ([UserID])
GO
ALTER TABLE [dbo].[GraderAllocations]  WITH CHECK ADD FOREIGN KEY([GraderUserID])
REFERENCES [dbo].[Users] ([UserID])
GO
ALTER TABLE [dbo].[GraderAllocations]  WITH CHECK ADD FOREIGN KEY([TeacherUserID])
REFERENCES [dbo].[Users] ([UserID])
GO
ALTER TABLE [dbo].[ProgramCoursePLOMappings]  WITH CHECK ADD FOREIGN KEY([MappedByUserID])
REFERENCES [dbo].[Users] ([UserID])
GO
ALTER TABLE [dbo].[ProgramCoursePLOMappings]  WITH CHECK ADD FOREIGN KEY([PLOID])
REFERENCES [dbo].[PLOs] ([PLOID])
GO
ALTER TABLE [dbo].[ProgramCoursePLOMappings]  WITH CHECK ADD FOREIGN KEY([ProgramCourseID])
REFERENCES [dbo].[AcademicProgramCourses] ([ProgramCourseID])
GO
ALTER TABLE [dbo].[ProgramCourseContentSubmissions]  WITH CHECK ADD FOREIGN KEY([ContentTypeID])
REFERENCES [dbo].[ContentSubmissionTypes] ([ContentTypeID])
GO
ALTER TABLE [dbo].[ProgramCourseContentSubmissions]  WITH CHECK ADD FOREIGN KEY([ProgramCourseID])
REFERENCES [dbo].[AcademicProgramCourses] ([ProgramCourseID])
GO
ALTER TABLE [dbo].[ProgramCourseContentSubmissions]  WITH CHECK ADD FOREIGN KEY([ReviewedByUserID])
REFERENCES [dbo].[Users] ([UserID])
GO
ALTER TABLE [dbo].[ProgramCourseContentSubmissions]  WITH CHECK ADD FOREIGN KEY([SessionID])
REFERENCES [dbo].[AcademicSessions] ([SessionID])
GO
ALTER TABLE [dbo].[ProgramCourseContentSubmissions]  WITH CHECK ADD FOREIGN KEY([SubmittedByUserID])
REFERENCES [dbo].[Users] ([UserID])
GO
ALTER TABLE [dbo].[TeacherPermissions]  WITH CHECK ADD FOREIGN KEY([HODUserID])
REFERENCES [dbo].[Users] ([UserID])
GO
ALTER TABLE [dbo].[TeacherPermissions]  WITH CHECK ADD FOREIGN KEY([PermissionTypeID])
REFERENCES [dbo].[TeacherPermissionTypes] ([PermissionTypeID])
GO
ALTER TABLE [dbo].[TeacherPermissions]  WITH CHECK ADD FOREIGN KEY([ProgramCourseID])
REFERENCES [dbo].[AcademicProgramCourses] ([ProgramCourseID])
GO
ALTER TABLE [dbo].[TeacherPermissions]  WITH CHECK ADD FOREIGN KEY([TeacherUserID])
REFERENCES [dbo].[Users] ([UserID])
GO
ALTER TABLE [dbo].[Sections]  WITH CHECK ADD FOREIGN KEY([SessionID])
REFERENCES [dbo].[AcademicSessions] ([SessionID])
GO
ALTER TABLE [dbo].[Students]  WITH CHECK ADD FOREIGN KEY([SectionID])
REFERENCES [dbo].[Sections] ([SectionID])
GO
ALTER TABLE [dbo].[CourseTopics]  WITH CHECK ADD FOREIGN KEY([ParentTopicID])
REFERENCES [dbo].[CourseTopics] ([TopicID])
GO
ALTER TABLE [dbo].[CourseTopics]  WITH CHECK ADD FOREIGN KEY([ProgramCourseID])
REFERENCES [dbo].[AcademicProgramCourses] ([ProgramCourseID])
GO
ALTER TABLE [dbo].[CourseTopics]  WITH CHECK ADD  CONSTRAINT [FK_CourseTopics_Session] FOREIGN KEY([SessionID])
REFERENCES [dbo].[AcademicSessions] ([SessionID])
GO
ALTER TABLE [dbo].[CourseTopics] CHECK CONSTRAINT [FK_CourseTopics_Session]
GO
ALTER TABLE [dbo].[CourseTopics]  WITH CHECK ADD  CONSTRAINT [FK_CourseTopics_Submission] FOREIGN KEY([SubmissionID])
REFERENCES [dbo].[ProgramCourseContentSubmissions] ([SubmissionID])
GO
ALTER TABLE [dbo].[CourseTopics] CHECK CONSTRAINT [FK_CourseTopics_Submission]
GO
ALTER TABLE [dbo].[CLOs]  WITH CHECK ADD FOREIGN KEY([ProgramCourseID])
REFERENCES [dbo].[AcademicProgramCourses] ([ProgramCourseID])
GO
ALTER TABLE [dbo].[CLOs]  WITH CHECK ADD  CONSTRAINT [FK_CLOs_Session] FOREIGN KEY([SessionID])
REFERENCES [dbo].[AcademicSessions] ([SessionID])
GO
ALTER TABLE [dbo].[CLOs] CHECK CONSTRAINT [FK_CLOs_Session]
GO
ALTER TABLE [dbo].[CLOs]  WITH CHECK ADD  CONSTRAINT [FK_CLOs_Submission] FOREIGN KEY([SubmissionID])
REFERENCES [dbo].[ProgramCourseContentSubmissions] ([SubmissionID])
GO
ALTER TABLE [dbo].[CLOs] CHECK CONSTRAINT [FK_CLOs_Submission]
GO
ALTER TABLE [dbo].[OfferedCourses]  WITH CHECK ADD FOREIGN KEY([CourseID])
REFERENCES [dbo].[Courses] ([CourseID])
GO
ALTER TABLE [dbo].[OfferedCourses]  WITH CHECK ADD FOREIGN KEY([SectionID])
REFERENCES [dbo].[Sections] ([SectionID])
GO
ALTER TABLE [dbo].[OfferedCourses]  WITH CHECK ADD FOREIGN KEY([TeacherUserID])
REFERENCES [dbo].[Users] ([UserID])
GO
ALTER TABLE [dbo].[OfferedCourseResultSchemes]  WITH CHECK ADD  CONSTRAINT [FK_OfferedCourseResultSchemes_OfferedCourse] FOREIGN KEY([OfferedCourseID])
REFERENCES [dbo].[OfferedCourses] ([OfferedCourseID])
GO
ALTER TABLE [dbo].[OfferedCourseResultSchemes] CHECK CONSTRAINT [FK_OfferedCourseResultSchemes_OfferedCourse]
GO
ALTER TABLE [dbo].[OfferedCourseLabMarkSchemes]  WITH CHECK ADD  CONSTRAINT [FK_OfferedCourseLabMarkSchemes_OfferedCourse] FOREIGN KEY([OfferedCourseID])
REFERENCES [dbo].[OfferedCourses] ([OfferedCourseID])
GO
ALTER TABLE [dbo].[OfferedCourseLabMarkSchemes] CHECK CONSTRAINT [FK_OfferedCourseLabMarkSchemes_OfferedCourse]
GO
ALTER TABLE [dbo].[OfferedCourseHomeworkMarkSchemes]  WITH CHECK ADD  CONSTRAINT [FK_OfferedCourseHomeworkMarkSchemes_OfferedCourse] FOREIGN KEY([OfferedCourseID])
REFERENCES [dbo].[OfferedCourses] ([OfferedCourseID])
GO
ALTER TABLE [dbo].[OfferedCourseHomeworkMarkSchemes] CHECK CONSTRAINT [FK_OfferedCourseHomeworkMarkSchemes_OfferedCourse]
GO
ALTER TABLE [dbo].[CourseFolders]  WITH CHECK ADD FOREIGN KEY([CreatedByUserID])
REFERENCES [dbo].[Users] ([UserID])
GO
ALTER TABLE [dbo].[CourseFolders]  WITH CHECK ADD FOREIGN KEY([OfferedCourseID])
REFERENCES [dbo].[OfferedCourses] ([OfferedCourseID])
GO
ALTER TABLE [dbo].[AttendanceRecords]  WITH CHECK ADD FOREIGN KEY([OfferedCourseID])
REFERENCES [dbo].[OfferedCourses] ([OfferedCourseID])
GO
ALTER TABLE [dbo].[AttendanceRecords]  WITH CHECK ADD FOREIGN KEY([StudentID])
REFERENCES [dbo].[Students] ([StudentID])
GO
ALTER TABLE [dbo].[AssessmentWeightages]  WITH CHECK ADD FOREIGN KEY([AssessmentTypeID])
REFERENCES [dbo].[AssessmentTypes] ([AssessmentTypeID])
GO
ALTER TABLE [dbo].[AssessmentWeightages]  WITH CHECK ADD FOREIGN KEY([OfferedCourseID])
REFERENCES [dbo].[OfferedCourses] ([OfferedCourseID])
GO
ALTER TABLE [dbo].[CourseAssessmentTypeCLOMappings]  WITH CHECK ADD FOREIGN KEY([AssessmentTypeID])
REFERENCES [dbo].[AssessmentTypes] ([AssessmentTypeID])
GO
ALTER TABLE [dbo].[CourseAssessmentTypeCLOMappings]  WITH CHECK ADD FOREIGN KEY([CLOID])
REFERENCES [dbo].[CLOs] ([CLOID])
GO
ALTER TABLE [dbo].[CourseAssessmentTypeCLOMappings]  WITH CHECK ADD FOREIGN KEY([ProgramCourseID])
REFERENCES [dbo].[AcademicProgramCourses] ([ProgramCourseID])
GO
ALTER TABLE [dbo].[CourseAssessmentTypeCLOMappings]  WITH CHECK ADD FOREIGN KEY([UpdatedByUserID])
REFERENCES [dbo].[Users] ([UserID])
GO
ALTER TABLE [dbo].[CourseAssessmentTypeCLOMappings]  WITH CHECK ADD  CONSTRAINT [FK_CourseAssessmentTypeCLOMappings_Session] FOREIGN KEY([SessionID])
REFERENCES [dbo].[AcademicSessions] ([SessionID])
GO
ALTER TABLE [dbo].[CourseAssessmentTypeCLOMappings] CHECK CONSTRAINT [FK_CourseAssessmentTypeCLOMappings_Session]
GO
ALTER TABLE [dbo].[CLOAttainmentResults]  WITH CHECK ADD FOREIGN KEY([CalculatedByUserID])
REFERENCES [dbo].[Users] ([UserID])
GO
ALTER TABLE [dbo].[CLOAttainmentResults]  WITH CHECK ADD FOREIGN KEY([CLOID])
REFERENCES [dbo].[CLOs] ([CLOID])
GO
ALTER TABLE [dbo].[CLOAttainmentResults]  WITH CHECK ADD FOREIGN KEY([OfferedCourseID])
REFERENCES [dbo].[OfferedCourses] ([OfferedCourseID])
GO
ALTER TABLE [dbo].[Assessments]  WITH CHECK ADD FOREIGN KEY([AssessmentTypeID])
REFERENCES [dbo].[AssessmentTypes] ([AssessmentTypeID])
GO
ALTER TABLE [dbo].[Assessments]  WITH CHECK ADD FOREIGN KEY([CreatedByUserID])
REFERENCES [dbo].[Users] ([UserID])
GO
ALTER TABLE [dbo].[Assessments]  WITH CHECK ADD FOREIGN KEY([OfferedCourseID])
REFERENCES [dbo].[OfferedCourses] ([OfferedCourseID])
GO
ALTER TABLE [dbo].[PLOCLOMappings]  WITH CHECK ADD FOREIGN KEY([CLOID])
REFERENCES [dbo].[CLOs] ([CLOID])
GO
ALTER TABLE [dbo].[PLOCLOMappings]  WITH CHECK ADD FOREIGN KEY([PLOID])
REFERENCES [dbo].[PLOs] ([PLOID])
GO
ALTER TABLE [dbo].[PLOCLOMappings]  WITH CHECK ADD FOREIGN KEY([ProgramCourseID])
REFERENCES [dbo].[AcademicProgramCourses] ([ProgramCourseID])
GO
ALTER TABLE [dbo].[PLOCLOMappings]  WITH CHECK ADD FOREIGN KEY([UpdatedByUserID])
REFERENCES [dbo].[Users] ([UserID])
GO
ALTER TABLE [dbo].[PLOCLOMappings]  WITH CHECK ADD  CONSTRAINT [FK_PLOCLOMappings_Session] FOREIGN KEY([SessionID])
REFERENCES [dbo].[AcademicSessions] ([SessionID])
GO
ALTER TABLE [dbo].[PLOCLOMappings] CHECK CONSTRAINT [FK_PLOCLOMappings_Session]
GO
ALTER TABLE [dbo].[PLOCLOMappings]  WITH CHECK ADD  CONSTRAINT [FK_PLOCLOMappings_Submission] FOREIGN KEY([SubmissionID])
REFERENCES [dbo].[ProgramCourseContentSubmissions] ([SubmissionID])
GO
ALTER TABLE [dbo].[PLOCLOMappings] CHECK CONSTRAINT [FK_PLOCLOMappings_Submission]
GO
ALTER TABLE [dbo].[PLOAttainmentResults]  WITH CHECK ADD  CONSTRAINT [FK_PLOAttainmentResults_OfferedCourse] FOREIGN KEY([OfferedCourseID])
REFERENCES [dbo].[OfferedCourses] ([OfferedCourseID])
GO
ALTER TABLE [dbo].[PLOAttainmentResults] CHECK CONSTRAINT [FK_PLOAttainmentResults_OfferedCourse]
GO
ALTER TABLE [dbo].[PLOAttainmentResults]  WITH CHECK ADD  CONSTRAINT [FK_PLOAttainmentResults_PLO] FOREIGN KEY([PLOID])
REFERENCES [dbo].[PLOs] ([PLOID])
GO
ALTER TABLE [dbo].[PLOAttainmentResults] CHECK CONSTRAINT [FK_PLOAttainmentResults_PLO]
GO
ALTER TABLE [dbo].[PLOAttainmentResults]  WITH CHECK ADD  CONSTRAINT [FK_PLOAttainmentResults_User] FOREIGN KEY([CalculatedByUserID])
REFERENCES [dbo].[Users] ([UserID])
GO
ALTER TABLE [dbo].[PLOAttainmentResults] CHECK CONSTRAINT [FK_PLOAttainmentResults_User]
GO
ALTER TABLE [dbo].[OfferedCourseTopicProgress]  WITH CHECK ADD FOREIGN KEY([CompletedByUserID])
REFERENCES [dbo].[Users] ([UserID])
GO
ALTER TABLE [dbo].[OfferedCourseTopicProgress]  WITH CHECK ADD FOREIGN KEY([OfferedCourseID])
REFERENCES [dbo].[OfferedCourses] ([OfferedCourseID])
GO
ALTER TABLE [dbo].[OfferedCourseTopicProgress]  WITH CHECK ADD FOREIGN KEY([TopicID])
REFERENCES [dbo].[CourseTopics] ([TopicID])
GO
ALTER TABLE [dbo].[CourseTopicCLOMappings]  WITH CHECK ADD FOREIGN KEY([CLOID])
REFERENCES [dbo].[CLOs] ([CLOID])
GO
ALTER TABLE [dbo].[CourseTopicCLOMappings]  WITH CHECK ADD FOREIGN KEY([TopicID])
REFERENCES [dbo].[CourseTopics] ([TopicID])
GO
ALTER TABLE [dbo].[StudentCourseResults]  WITH CHECK ADD  CONSTRAINT [FK_StudentCourseResults_OfferedCourse] FOREIGN KEY([OfferedCourseID])
REFERENCES [dbo].[OfferedCourses] ([OfferedCourseID])
GO
ALTER TABLE [dbo].[StudentCourseResults] CHECK CONSTRAINT [FK_StudentCourseResults_OfferedCourse]
GO
ALTER TABLE [dbo].[StudentCourseResults]  WITH CHECK ADD  CONSTRAINT [FK_StudentCourseResults_Student] FOREIGN KEY([StudentID])
REFERENCES [dbo].[Students] ([StudentID])
GO
ALTER TABLE [dbo].[StudentCourseResults] CHECK CONSTRAINT [FK_StudentCourseResults_Student]
GO
ALTER TABLE [dbo].[StudentOfferedCourses]  WITH CHECK ADD  CONSTRAINT [FK_StudentOfferedCourses_OfferedCourse] FOREIGN KEY([OfferedCourseID])
REFERENCES [dbo].[OfferedCourses] ([OfferedCourseID])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[StudentOfferedCourses] CHECK CONSTRAINT [FK_StudentOfferedCourses_OfferedCourse]
GO
ALTER TABLE [dbo].[StudentOfferedCourses]  WITH CHECK ADD  CONSTRAINT [FK_StudentOfferedCourses_Student] FOREIGN KEY([StudentID])
REFERENCES [dbo].[Students] ([StudentID])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[StudentOfferedCourses] CHECK CONSTRAINT [FK_StudentOfferedCourses_Student]
GO
ALTER TABLE [dbo].[StudentCourseResultSelections]  WITH CHECK ADD  CONSTRAINT [FK_StudentCourseResultSelections_Assessment] FOREIGN KEY([AssessmentID])
REFERENCES [dbo].[Assessments] ([AssessmentID])
GO
ALTER TABLE [dbo].[StudentCourseResultSelections] CHECK CONSTRAINT [FK_StudentCourseResultSelections_Assessment]
GO
ALTER TABLE [dbo].[StudentCourseResultSelections]  WITH CHECK ADD  CONSTRAINT [FK_StudentCourseResultSelections_OfferedCourse] FOREIGN KEY([OfferedCourseID])
REFERENCES [dbo].[OfferedCourses] ([OfferedCourseID])
GO
ALTER TABLE [dbo].[StudentCourseResultSelections] CHECK CONSTRAINT [FK_StudentCourseResultSelections_OfferedCourse]
GO
ALTER TABLE [dbo].[StudentCourseResultSelections]  WITH CHECK ADD  CONSTRAINT [FK_StudentCourseResultSelections_Student] FOREIGN KEY([StudentID])
REFERENCES [dbo].[Students] ([StudentID])
GO
ALTER TABLE [dbo].[StudentCourseResultSelections] CHECK CONSTRAINT [FK_StudentCourseResultSelections_Student]
GO
ALTER TABLE [dbo].[StudentAssessmentMarks]  WITH CHECK ADD FOREIGN KEY([AssessmentID])
REFERENCES [dbo].[Assessments] ([AssessmentID])
GO
ALTER TABLE [dbo].[StudentAssessmentMarks]  WITH CHECK ADD FOREIGN KEY([StudentID])
REFERENCES [dbo].[Students] ([StudentID])
GO
ALTER TABLE [dbo].[FolderSubmissions]  WITH CHECK ADD FOREIGN KEY([CourseFolderID])
REFERENCES [dbo].[CourseFolders] ([CourseFolderID])
GO
ALTER TABLE [dbo].[FolderSubmissions]  WITH CHECK ADD FOREIGN KEY([SubmittedByUserID])
REFERENCES [dbo].[Users] ([UserID])
GO
ALTER TABLE [dbo].[FolderSubmissions]  WITH CHECK ADD  CONSTRAINT [FK_FolderSubmissions_ContentSubmission] FOREIGN KEY([ContentSubmissionID])
REFERENCES [dbo].[ProgramCourseContentSubmissions] ([SubmissionID])
GO
ALTER TABLE [dbo].[FolderSubmissions] CHECK CONSTRAINT [FK_FolderSubmissions_ContentSubmission]
GO
ALTER TABLE [dbo].[FolderSubmissions]  WITH CHECK ADD  CONSTRAINT [FK_FolderSubmissions_ReviewedBy] FOREIGN KEY([ReviewedByUserID])
REFERENCES [dbo].[Users] ([UserID])
GO
ALTER TABLE [dbo].[FolderSubmissions] CHECK CONSTRAINT [FK_FolderSubmissions_ReviewedBy]
GO
ALTER TABLE [dbo].[AssessmentQuestions]  WITH CHECK ADD FOREIGN KEY([AssessmentID])
REFERENCES [dbo].[Assessments] ([AssessmentID])
GO
ALTER TABLE [dbo].[AssessmentQuestions]  WITH CHECK ADD  CONSTRAINT [FK_AssessmentQuestions_Parent] FOREIGN KEY([ParentQuestionID])
REFERENCES [dbo].[AssessmentQuestions] ([QuestionID])
GO
ALTER TABLE [dbo].[AssessmentQuestions] CHECK CONSTRAINT [FK_AssessmentQuestions_Parent]
GO
ALTER TABLE [dbo].[CourseFolderDirectories]  WITH CHECK ADD FOREIGN KEY([CourseFolderID])
REFERENCES [dbo].[CourseFolders] ([CourseFolderID])
GO
ALTER TABLE [dbo].[CourseFolderDirectories]  WITH CHECK ADD FOREIGN KEY([ParentDirectoryID])
REFERENCES [dbo].[CourseFolderDirectories] ([DirectoryID])
GO
ALTER TABLE [dbo].[CourseFolderDirectories]  WITH CHECK ADD FOREIGN KEY([TemplateItemID])
REFERENCES [dbo].[CourseFolderTemplateItems] ([TemplateItemID])
GO
ALTER TABLE [dbo].[CourseFolderFiles]  WITH CHECK ADD FOREIGN KEY([DirectoryID])
REFERENCES [dbo].[CourseFolderDirectories] ([DirectoryID])
GO
ALTER TABLE [dbo].[CourseFolderFiles]  WITH CHECK ADD FOREIGN KEY([UploadedByUserID])
REFERENCES [dbo].[Users] ([UserID])
GO
ALTER TABLE [dbo].[AssessmentQuestionCLOMappings]  WITH CHECK ADD FOREIGN KEY([CLOID])
REFERENCES [dbo].[CLOs] ([CLOID])
GO
ALTER TABLE [dbo].[AssessmentQuestionCLOMappings]  WITH CHECK ADD FOREIGN KEY([QuestionID])
REFERENCES [dbo].[AssessmentQuestions] ([QuestionID])
GO
ALTER TABLE [dbo].[StudentQuestionMarks]  WITH CHECK ADD FOREIGN KEY([QuestionID])
REFERENCES [dbo].[AssessmentQuestions] ([QuestionID])
GO
ALTER TABLE [dbo].[StudentQuestionMarks]  WITH CHECK ADD FOREIGN KEY([StudentID])
REFERENCES [dbo].[Students] ([StudentID])
GO
ALTER TABLE [dbo].[FolderSubmissionIssues]  WITH CHECK ADD FOREIGN KEY([SubmissionID])
REFERENCES [dbo].[FolderSubmissions] ([SubmissionID])
GO
ALTER TABLE [dbo].[LessonPlans]  WITH CHECK ADD FOREIGN KEY([FileID])
REFERENCES [dbo].[CourseFolderFiles] ([FileID])
GO
ALTER TABLE [dbo].[LessonPlans]  WITH CHECK ADD FOREIGN KEY([OfferedCourseID])
REFERENCES [dbo].[OfferedCourses] ([OfferedCourseID])
GO
ALTER TABLE [dbo].[LessonPlans]  WITH CHECK ADD FOREIGN KEY([UploadedByUserID])
REFERENCES [dbo].[Users] ([UserID])
GO
ALTER TABLE [dbo].[GraderTasks]  WITH CHECK ADD FOREIGN KEY([AssessmentID])
REFERENCES [dbo].[Assessments] ([AssessmentID])
GO
ALTER TABLE [dbo].[GraderTasks]  WITH CHECK ADD FOREIGN KEY([AssignedByUserID])
REFERENCES [dbo].[Users] ([UserID])
GO
ALTER TABLE [dbo].[GraderTasks]  WITH CHECK ADD FOREIGN KEY([GraderAllocationID])
REFERENCES [dbo].[GraderAllocations] ([GraderAllocationID])
GO
ALTER TABLE [dbo].[GraderTasks]  WITH CHECK ADD FOREIGN KEY([SourceFileID])
REFERENCES [dbo].[CourseFolderFiles] ([FileID])
GO
ALTER TABLE [dbo].[AssessmentFiles]  WITH CHECK ADD FOREIGN KEY([AssessmentID])
REFERENCES [dbo].[Assessments] ([AssessmentID])
GO
ALTER TABLE [dbo].[AssessmentFiles]  WITH CHECK ADD FOREIGN KEY([FileID])
REFERENCES [dbo].[CourseFolderFiles] ([FileID])
GO
ALTER TABLE [dbo].[ProgramCoursePLOMappings]  WITH CHECK ADD CHECK  (([Percentage] IS NULL OR [Percentage]>=(0) AND [Percentage]<=(100)))
GO
ALTER TABLE [dbo].[ProgramCourseContentSubmissions]  WITH CHECK ADD CHECK  (([SubmissionStatus]='Rejected' OR [SubmissionStatus]='Approved' OR [SubmissionStatus]='Submitted'))
GO
ALTER TABLE [dbo].[CourseTopics]  WITH CHECK ADD CHECK  (([WeekNo] IS NULL OR [WeekNo]>(0)))
GO
ALTER TABLE [dbo].[CourseTopics]  WITH CHECK ADD  CONSTRAINT [CK_CourseTopics_ContentStatus] CHECK  (([ContentStatus]='Rejected' OR [ContentStatus]='Approved' OR [ContentStatus]='PendingReview' OR [ContentStatus]='Draft'))
GO
ALTER TABLE [dbo].[CourseTopics] CHECK CONSTRAINT [CK_CourseTopics_ContentStatus]
GO
ALTER TABLE [dbo].[CLOs]  WITH CHECK ADD  CONSTRAINT [CK_CLOs_ContentStatus] CHECK  (([ContentStatus]='Rejected' OR [ContentStatus]='Approved' OR [ContentStatus]='PendingReview' OR [ContentStatus]='Draft'))
GO
ALTER TABLE [dbo].[CLOs] CHECK CONSTRAINT [CK_CLOs_ContentStatus]
GO
ALTER TABLE [dbo].[CourseFolders]  WITH CHECK ADD CHECK  (([FolderStatus]='Needs Revision' OR [FolderStatus]='Failed' OR [FolderStatus]='Submitted' OR [FolderStatus]='Ready For Submission' OR [FolderStatus]='Generated' OR [FolderStatus]='In Progress'))
GO
ALTER TABLE [dbo].[AttendanceRecords]  WITH CHECK ADD CHECK  (([Status]='Late' OR [Status]='Leave' OR [Status]='Absent' OR [Status]='Present'))
GO
ALTER TABLE [dbo].[AssessmentWeightages]  WITH CHECK ADD CHECK  (([WeightagePercentage]>=(0) AND [WeightagePercentage]<=(100)))
GO
ALTER TABLE [dbo].[CourseAssessmentTypeCLOMappings]  WITH CHECK ADD CHECK  (([Percentage] IS NULL OR [Percentage]>=(0) AND [Percentage]<=(100)))
GO
ALTER TABLE [dbo].[CLOAttainmentResults]  WITH CHECK ADD CHECK  (([AttainmentPercentage]>=(0) AND [AttainmentPercentage]<=(100)))
GO
ALTER TABLE [dbo].[Assessments]  WITH CHECK ADD CHECK  (([AssessmentNo] IS NULL OR [AssessmentNo]>(0)))
GO
ALTER TABLE [dbo].[Assessments]  WITH CHECK ADD CHECK  (([TotalMarks] IS NULL OR [TotalMarks]>=(0)))
GO
ALTER TABLE [dbo].[PLOCLOMappings]  WITH CHECK ADD CHECK  (([Percentage] IS NULL OR [Percentage]>=(0) AND [Percentage]<=(100)))
GO
ALTER TABLE [dbo].[PLOCLOMappings]  WITH CHECK ADD  CONSTRAINT [CK_PLOCLOMappings_ContentStatus] CHECK  (([ContentStatus]='Rejected' OR [ContentStatus]='Approved' OR [ContentStatus]='PendingReview' OR [ContentStatus]='Draft'))
GO
ALTER TABLE [dbo].[PLOCLOMappings] CHECK CONSTRAINT [CK_PLOCLOMappings_ContentStatus]
GO
ALTER TABLE [dbo].[StudentAssessmentMarks]  WITH CHECK ADD CHECK  (([ObtainedMarks]>=(0)))
GO
ALTER TABLE [dbo].[FolderSubmissions]  WITH CHECK ADD  CONSTRAINT [CK_FolderSubmissions_Status] CHECK  (([SubmissionStatus]='Needs Revision' OR [SubmissionStatus]='Failed' OR [SubmissionStatus]='Rejected' OR [SubmissionStatus]='Approved' OR [SubmissionStatus]='Submitted' OR [SubmissionStatus]='Ready For Submission' OR [SubmissionStatus]='Generated' OR [SubmissionStatus]='Draft'))
GO
ALTER TABLE [dbo].[FolderSubmissions] CHECK CONSTRAINT [CK_FolderSubmissions_Status]
GO
ALTER TABLE [dbo].[AssessmentQuestions]  WITH CHECK ADD CHECK  (([Marks] IS NULL OR [Marks]>=(0)))
GO
ALTER TABLE [dbo].[CourseFolderFiles]  WITH CHECK ADD  CONSTRAINT [CK_CourseFolderFiles_FileCategory] CHECK  (([FileCategory]='Course Objective' OR [FileCategory]='CLO-Task Mapping' OR [FileCategory]='CLO-GA Mapping' OR [FileCategory]='Course Content' OR [FileCategory]='Weekly Plan' OR [FileCategory]='Attendance Record' OR [FileCategory]='Reference Material' OR [FileCategory]='Assignment Question' OR [FileCategory]='Quiz Question' OR [FileCategory]='Mid Exam Question' OR [FileCategory]='Final Exam Question' OR [FileCategory]='Assignment Solution' OR [FileCategory]='Quiz Solution' OR [FileCategory]='Mid Exam Solution' OR [FileCategory]='Final Exam Solution' OR [FileCategory]='Assignment Best Sample' OR [FileCategory]='Assignment Average Sample' OR [FileCategory]='Assignment Worst Sample' OR [FileCategory]='Quiz Best Sample' OR [FileCategory]='Quiz Average Sample' OR [FileCategory]='Quiz Worst Sample' OR [FileCategory]='Mid Exam Best Sample' OR [FileCategory]='Mid Exam Average Sample' OR [FileCategory]='Mid Exam Worst Sample' OR [FileCategory]='Final Exam Best Sample' OR [FileCategory]='Final Exam Average Sample' OR [FileCategory]='Final Exam Worst Sample' OR [FileCategory]='Marks Distribution and Grading' OR [FileCategory]='Result' OR [FileCategory]='Outcomes Assessment' OR [FileCategory]='Technology Involved' OR [FileCategory]='Design Techniques Practiced' OR [FileCategory]='Complete Analysis of Course' OR [FileCategory]='Other'))
GO
ALTER TABLE [dbo].[CourseFolderFiles] CHECK CONSTRAINT [CK_CourseFolderFiles_FileCategory]
GO
ALTER TABLE [dbo].[AssessmentQuestionCLOMappings]  WITH CHECK ADD CHECK  (([Percentage] IS NULL OR [Percentage]>=(0) AND [Percentage]<=(100)))
GO
ALTER TABLE [dbo].[StudentQuestionMarks]  WITH CHECK ADD CHECK  (([ObtainedMarks]>=(0)))
GO
ALTER TABLE [dbo].[GraderTasks]  WITH CHECK ADD CHECK  (([TaskStatus]='Returned' OR [TaskStatus]='Completed' OR [TaskStatus]='In Progress' OR [TaskStatus]='Pending'))
GO
ALTER TABLE [dbo].[AssessmentFiles]  WITH CHECK ADD CHECK  (([FilePurpose]='Marks Distribution' OR [FilePurpose]='Result' OR [FilePurpose]='Worst Sample' OR [FilePurpose]='Average Sample' OR [FilePurpose]='Best Sample' OR [FilePurpose]='Solution' OR [FilePurpose]='Question Paper'))
GO
