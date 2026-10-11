using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using WebApplication1.Models.TeacherScaffold;

namespace FolderManagementSystemAPI.Models;

public partial class FolderManagementDbContext : DbContext
{
    public FolderManagementDbContext(DbContextOptions<FolderManagementDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AcademicProgram> AcademicPrograms { get; set; }

    public virtual DbSet<AcademicSession> AcademicSessions { get; set; }

    public virtual DbSet<Course> Courses { get; set; }

    public virtual DbSet<CourseCatalogVersion> CourseCatalogVersions { get; set; }

    public virtual DbSet<CourseCatalogVersionItem> CourseCatalogVersionItems { get; set; }

    public virtual DbSet<OutcomeCatalogVersion> OutcomeCatalogVersions { get; set; }

    public virtual DbSet<OutcomeCatalogVersionItem> OutcomeCatalogVersionItems { get; set; }

    public virtual DbSet<GraderAllocation> GraderAllocations { get; set; }

    public virtual DbSet<OfferedCourse> OfferedCourses { get; set; }

    public virtual DbSet<Peo> Peos { get; set; }

    public virtual DbSet<Plo> Plos { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<Section> Sections { get; set; }

    public virtual DbSet<Student> Students { get; set; }

    public virtual DbSet<StudentOfferedCourse> StudentOfferedCourses { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<AcademicProgramCourse> AcademicProgramCourses { get; set; }

    public virtual DbSet<Assessment> Assessments { get; set; }

    public virtual DbSet<AssessmentFile> AssessmentFiles { get; set; }

    public virtual DbSet<AssessmentQuestion> AssessmentQuestions { get; set; }

    public virtual DbSet<AssessmentQuestionClomapping> AssessmentQuestionClomappings { get; set; }

    public virtual DbSet<AssessmentType> AssessmentTypes { get; set; }

    public virtual DbSet<AssessmentWeightage> AssessmentWeightages { get; set; }

    public virtual DbSet<AttendanceRecord> AttendanceRecords { get; set; }

    public virtual DbSet<Clo> Clos { get; set; }

    public virtual DbSet<CloattainmentResult> CloattainmentResults { get; set; }

    public virtual DbSet<PloattainmentResult> PloattainmentResults { get; set; }

    public virtual DbSet<CourseAssessmentTypeClomapping> CourseAssessmentTypeClomappings { get; set; }

    public virtual DbSet<CourseSessionTaskType> CourseSessionTaskTypes { get; set; }

    public virtual DbSet<CourseFolder> CourseFolders { get; set; }

    public virtual DbSet<CourseFolderDirectory> CourseFolderDirectories { get; set; }

    public virtual DbSet<CourseFolderFile> CourseFolderFiles { get; set; }

    public virtual DbSet<CourseFolderTemplateItem> CourseFolderTemplateItems { get; set; }

    public virtual DbSet<CourseTopic> CourseTopics { get; set; }

    public virtual DbSet<CourseTopicClomapping> CourseTopicClomappings { get; set; }

    public virtual DbSet<FolderSubmission> FolderSubmissions { get; set; }

    public virtual DbSet<FolderSubmissionIssue> FolderSubmissionIssues { get; set; }

    public virtual DbSet<GraderTask> GraderTasks { get; set; }

    public virtual DbSet<LessonPlan> LessonPlans { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<OfferedCourseTopicProgress> OfferedCourseTopicProgresses { get; set; }

    public virtual DbSet<Ploclomapping> Ploclomappings { get; set; }

    public virtual DbSet<ProgramCoursePlomapping> ProgramCoursePlomappings { get; set; }

    public virtual DbSet<PeoPlomapping> PeoPlomappings { get; set; }

    public virtual DbSet<StudentAssessmentMark> StudentAssessmentMarks { get; set; }

    public virtual DbSet<StudentQuestionMark> StudentQuestionMarks { get; set; }

    public virtual DbSet<OfferedCourseResultScheme> OfferedCourseResultSchemes { get; set; }

    public virtual DbSet<OfferedCourseLabMarkScheme> OfferedCourseLabMarkSchemes { get; set; }

    public virtual DbSet<OfferedCourseHomeworkMarkScheme> OfferedCourseHomeworkMarkSchemes { get; set; }

    public virtual DbSet<StudentCourseResult> StudentCourseResults { get; set; }

    public virtual DbSet<StudentCourseResultSelection> StudentCourseResultSelections { get; set; }

    public virtual DbSet<StudentComponentMark> StudentComponentMarks { get; set; }

    public virtual DbSet<FcarReportInput> FcarReportInputs { get; set; }

    public virtual DbSet<HodUploadAllocation> HodUploadAllocations { get; set; }

    public virtual DbSet<TeacherPermission> TeacherPermissions { get; set; }

    public virtual DbSet<TeacherPermissionType> TeacherPermissionTypes { get; set; }

    public virtual DbSet<ContentSubmissionType> ContentSubmissionTypes { get; set; }

    public virtual DbSet<ProgramCourseContentSubmission> ProgramCourseContentSubmissions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FcarReportInput>(entity =>
        {
            entity.HasIndex(e => e.OfferedCourseId).IsUnique();
            entity.Property(e => e.CatalogDescription).HasColumnType("nvarchar(max)");
            entity.Property(e => e.CourseModifications).HasColumnType("nvarchar(max)");
            entity.Property(e => e.StudentFeedbackText).HasColumnType("nvarchar(max)");
            entity.Property(e => e.ReflectionText).HasColumnType("nvarchar(max)");
            entity.Property(e => e.ProposedActionsText).HasColumnType("nvarchar(max)");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())");
        });

        modelBuilder.Entity<HodUploadAllocation>(entity =>
        {
            entity.ToTable("HodUploadAllocations");
            entity.HasKey(e => e.OfferedCourseId);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.HasOne(e => e.OfferedCourse)
                .WithMany()
                .HasForeignKey(e => e.OfferedCourseId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AcademicProgram>(entity =>
        {
            entity.HasKey(e => e.ProgramId).HasName("PK__Academic__752560382CF64A69");
        });

        modelBuilder.Entity<AcademicSession>(entity =>
        {
            entity.HasKey(e => e.SessionId).HasName("PK__Academic__C9F49270E7DF6C31");

            entity.HasOne(d => d.Program).WithMany(p => p.AcademicSessions)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__AcademicS__Progr__5535A963");
        });

        modelBuilder.Entity<Course>(entity =>
        {
            entity.HasKey(e => e.CourseId).HasName("PK__Courses__C92D718700C1254F");
        });

        modelBuilder.Entity<GraderAllocation>(entity =>
        {
            entity.HasKey(e => e.GraderAllocationId).HasName("PK__GraderAl__01D0B237D8995A12");

            entity.HasOne(d => d.GraderUser).WithMany(p => p.GraderAllocationGraderUsers)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__GraderAll__Grade__693CA210");

            entity.HasOne(d => d.TeacherUser).WithMany(p => p.GraderAllocationTeacherUsers)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__GraderAll__Teach__68487DD7");
        });

        modelBuilder.Entity<OfferedCourse>(entity =>
        {
            entity.HasKey(e => e.OfferedCourseId).HasName("PK__OfferedC__94C9F8E2F963C8D7");

            entity.HasOne(d => d.Course).WithMany(p => p.OfferedCourses)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__OfferedCo__Cours__5EBF139D");

            entity.HasOne(d => d.Section).WithMany(p => p.OfferedCourses)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__OfferedCo__Secti__5DCAEF64");

            entity.HasOne(d => d.TeacherUser).WithMany(p => p.OfferedCourses)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__OfferedCo__Teach__5FB337D6");
        });

        modelBuilder.Entity<Peo>(entity =>
        {
            entity.HasKey(e => e.Peoid).HasName("PK__PEOs__4C0E80203B15CDE5");

            entity.HasOne(d => d.Program).WithMany(p => p.Peos)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PEOs__ProgramID__656C112C");
        });

        modelBuilder.Entity<Plo>(entity =>
        {
            entity.HasKey(e => e.Ploid).HasName("PK__PLOs__7436BCC20187A7E0");

            entity.HasOne(d => d.Program).WithMany(p => p.Plos)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PLOs__ProgramID__628FA481");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.RoleId).HasName("PK__Roles__8AFACE3AD8688904");
        });

        modelBuilder.Entity<Section>(entity =>
        {
            entity.HasKey(e => e.SectionId).HasName("PK__Sections__80EF089299AFAA09");

            entity.HasOne(d => d.Session).WithMany(p => p.Sections)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Sections__Sessio__5812160E");
        });

        modelBuilder.Entity<Student>(entity =>
        {
            entity.HasKey(e => e.StudentId).HasName("PK__Students__32C52A7934CBBA6B");

            entity.HasOne(d => d.Section).WithMany(p => p.Students)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Students__Sectio__5AEE82B9");
        });

        modelBuilder.Entity<StudentOfferedCourse>(entity =>
        {
            entity.HasKey(e => e.StudentOfferedCourseId).HasName("PK_StudentOfferedCourses");

            entity.HasIndex(e => new { e.StudentId, e.OfferedCourseId }, "UQ_StudentOfferedCourses_Student_Offered")
                .IsUnique();

            entity.Property(e => e.StudentOfferedCourseId).HasColumnName("StudentOfferedCourseID");
            entity.Property(e => e.StudentId).HasColumnName("StudentID");
            entity.Property(e => e.OfferedCourseId).HasColumnName("OfferedCourseID");

            entity.HasOne(d => d.Student).WithMany(p => p.StudentOfferedCourses)
                .HasForeignKey(d => d.StudentId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_StudentOfferedCourses_Student");

            entity.HasOne(d => d.OfferedCourse).WithMany(p => p.StudentOfferedCourses)
                .HasForeignKey(d => d.OfferedCourseId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_StudentOfferedCourses_OfferedCourse");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__Users__1788CCAC72E4EBE2");

            entity.HasOne(d => d.Role).WithMany(p => p.Users)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Users__RoleID__4E88ABD4");
        });

        modelBuilder.Entity<AcademicProgramCourse>(entity =>
        {
            entity.HasKey(e => e.ProgramCourseId).HasName("PK__Academic__8BD8F33EA2A1816E");

            entity.HasIndex(e => new { e.ProgramId, e.CourseId }, "UQ__Academic__19B7B721430939FC").IsUnique();

            entity.Property(e => e.ProgramCourseId).HasColumnName("ProgramCourseID");
            entity.Property(e => e.CourseId).HasColumnName("CourseID");
            entity.Property(e => e.ProgramId).HasColumnName("ProgramID");
        });

        modelBuilder.Entity<Assessment>(entity =>
        {
            entity.HasKey(e => e.AssessmentId).HasName("PK__Assessme__3D2BF83E5B4F33E4");

            entity.HasIndex(e => new { e.OfferedCourseId, e.AssessmentTypeId, e.AssessmentNo }, "UQ__Assessme__13C25EC492BEBAD5").IsUnique();

            entity.Property(e => e.AssessmentId).HasColumnName("AssessmentID");
            entity.Property(e => e.AssessmentTypeId).HasColumnName("AssessmentTypeID");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.CreatedByUserId).HasColumnName("CreatedByUserID");
            entity.Property(e => e.OfferedCourseId).HasColumnName("OfferedCourseID");
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.TotalMarks).HasColumnType("decimal(6, 2)");
            entity.Property(e => e.IsSetupComplete).HasDefaultValue(false);
            entity.Property(e => e.SetupCompletedAt).HasColumnType("datetime");

            entity.HasOne(d => d.AssessmentType).WithMany(p => p.Assessments)
                .HasForeignKey(d => d.AssessmentTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Assessmen__Asses__489AC854");
        });

        modelBuilder.Entity<AssessmentFile>(entity =>
        {
            entity.HasKey(e => e.AssessmentFileId).HasName("PK__Assessme__68FA11AE17C00E0A");

            entity.HasIndex(e => new { e.AssessmentId, e.FileId, e.FilePurpose }, "UQ__Assessme__01C81E1B1B7C9042").IsUnique();

            entity.Property(e => e.AssessmentFileId).HasColumnName("AssessmentFileID");
            entity.Property(e => e.AssessmentId).HasColumnName("AssessmentID");
            entity.Property(e => e.FileId).HasColumnName("FileID");
            entity.Property(e => e.FilePurpose).HasMaxLength(100);

            entity.HasOne(d => d.Assessment).WithMany(p => p.AssessmentFiles)
                .HasForeignKey(d => d.AssessmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Assessmen__Asses__0F2D40CE");

            entity.HasOne(d => d.File).WithMany(p => p.AssessmentFiles)
                .HasForeignKey(d => d.FileId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Assessmen__FileI__10216507");
        });

        modelBuilder.Entity<AssessmentQuestion>(entity =>
        {
            entity.HasKey(e => e.QuestionId).HasName("PK__Assessme__0DC06F8C4C51CCFC");

            entity.HasIndex(e => new { e.AssessmentId, e.QuestionNo }, "UQ__Assessme__CDF7F36BCDDF38EC").IsUnique();

            entity.Property(e => e.QuestionId).HasColumnName("QuestionID");
            entity.Property(e => e.AssessmentId).HasColumnName("AssessmentID");
            entity.Property(e => e.ParentQuestionId).HasColumnName("ParentQuestionID");
            entity.Property(e => e.DisplayOrder);
            entity.Property(e => e.Marks).HasColumnType("decimal(6, 2)");
            entity.Property(e => e.QuestionNo).HasMaxLength(50);
            entity.Property(e => e.QuestionText).HasMaxLength(1000);

            entity.HasOne(d => d.Assessment).WithMany(p => p.AssessmentQuestions)
                .HasForeignKey(d => d.AssessmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Assessmen__Asses__4F47C5E3");

            entity.HasOne(d => d.ParentQuestion).WithMany(p => p.InverseParentQuestion)
                .HasForeignKey(d => d.ParentQuestionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AssessmentQuestions_Parent");
        });

        modelBuilder.Entity<AssessmentQuestionClomapping>(entity =>
        {
            entity.HasKey(e => e.QuestionClomappingId).HasName("PK__Assessme__098331E89BE8E3D7");

            entity.ToTable("AssessmentQuestionCLOMappings");

            entity.HasIndex(e => new { e.QuestionId, e.Cloid }, "UQ__Assessme__9C0C1E77FAFCAFB7").IsUnique();

            entity.Property(e => e.QuestionClomappingId).HasColumnName("QuestionCLOMappingID");
            entity.Property(e => e.Cloid).HasColumnName("CLOID");
            entity.Property(e => e.Percentage).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.QuestionId).HasColumnName("QuestionID");

            entity.HasOne(d => d.Clo).WithMany(p => p.AssessmentQuestionClomappings)
                .HasForeignKey(d => d.Cloid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Assessmen__CLOID__55009F39");

            entity.HasOne(d => d.Question).WithMany(p => p.AssessmentQuestionClomappings)
                .HasForeignKey(d => d.QuestionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Assessmen__Quest__540C7B00");
        });

        modelBuilder.Entity<AssessmentType>(entity =>
        {
            entity.HasKey(e => e.AssessmentTypeId).HasName("PK__Assessme__3368D75100744142");

            entity.HasIndex(e => e.AssessmentTypeName, "UQ__Assessme__480D14F553949F2B").IsUnique();

            entity.Property(e => e.AssessmentTypeId).HasColumnName("AssessmentTypeID");
            entity.Property(e => e.AssessmentTypeName).HasMaxLength(50);
        });

        modelBuilder.Entity<AssessmentWeightage>(entity =>
        {
            entity.HasKey(e => e.WeightageId).HasName("PK__Assessme__B836400F480ED575");

            entity.HasIndex(e => new { e.OfferedCourseId, e.AssessmentTypeId }, "UQ__Assessme__97FF75962542B52B").IsUnique();

            entity.Property(e => e.WeightageId).HasColumnName("WeightageID");
            entity.Property(e => e.AssessmentTypeId).HasColumnName("AssessmentTypeID");
            entity.Property(e => e.OfferedCourseId).HasColumnName("OfferedCourseID");
            entity.Property(e => e.WeightagePercentage).HasColumnType("decimal(5, 2)");

            entity.HasOne(d => d.AssessmentType).WithMany(p => p.AssessmentWeightages)
                .HasForeignKey(d => d.AssessmentTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Assessmen__Asses__5AB9788F");
        });

        modelBuilder.Entity<AttendanceRecord>(entity =>
        {
            entity.HasKey(e => e.AttendanceId).HasName("PK__Attendan__8B69263C433F637F");

            entity.HasIndex(e => new { e.OfferedCourseId, e.StudentId, e.AttendanceDate }, "UQ__Attendan__E73205C382323D44").IsUnique();

            entity.Property(e => e.AttendanceId).HasColumnName("AttendanceID");
            entity.Property(e => e.OfferedCourseId).HasColumnName("OfferedCourseID");
            entity.Property(e => e.Status).HasMaxLength(20);
            entity.Property(e => e.StudentId).HasColumnName("StudentID");
        });

        modelBuilder.Entity<Clo>(entity =>
        {
            entity.HasKey(e => e.Cloid).HasName("PK__CLOs__1CC71FA8DB51F151");

            entity.ToTable("CLOs");

            entity.HasIndex(e => new { e.ProgramCourseId, e.SessionId, e.Clocode }, "UQ_CLOs_Session_Code")
                .IsUnique()
                .HasFilter("[SessionID] IS NOT NULL");

            entity.Property(e => e.Cloid).HasColumnName("CLOID");
            entity.Property(e => e.Clocode)
                .HasMaxLength(20)
                .HasColumnName("CLOCode");
            entity.Property(e => e.Clodescription)
                .HasMaxLength(1000)
                .HasColumnName("CLODescription");
            entity.Property(e => e.ProgramCourseId).HasColumnName("ProgramCourseID");
            entity.Property(e => e.SessionId).HasColumnName("SessionID");
            entity.Property(e => e.ContentStatus)
                .HasMaxLength(20)
                .HasDefaultValue("Draft");
            entity.Property(e => e.SubmissionId).HasColumnName("SubmissionID");

            entity.HasOne(d => d.ProgramCourse).WithMany(p => p.Clos)
                .HasForeignKey(d => d.ProgramCourseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__CLOs__ProgramCou__1332DBDC");
        });

        modelBuilder.Entity<CloattainmentResult>(entity =>
        {
            entity.HasKey(e => e.AttainmentId).HasName("PK__CLOAttai__BEF0E939D5BBC9B6");

            entity.ToTable("CLOAttainmentResults");

            entity.HasIndex(e => new { e.OfferedCourseId, e.Cloid }, "UQ__CLOAttai__05058919E1362A58").IsUnique();

            entity.Property(e => e.AttainmentId).HasColumnName("AttainmentID");
            entity.Property(e => e.AttainmentPercentage).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.CalculatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.CalculatedByUserId).HasColumnName("CalculatedByUserID");
            entity.Property(e => e.Cloid).HasColumnName("CLOID");
            entity.Property(e => e.OfferedCourseId).HasColumnName("OfferedCourseID");

            entity.HasOne(d => d.Clo).WithMany(p => p.CloattainmentResults)
                .HasForeignKey(d => d.Cloid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__CLOAttain__CLOID__6CD828CA");
        });

        modelBuilder.Entity<PloattainmentResult>(entity =>
        {
            entity.HasKey(e => e.AttainmentId).HasName("PK_PLOAttainmentResults");

            entity.ToTable("PLOAttainmentResults");

            entity.HasIndex(e => new { e.OfferedCourseId, e.Ploid }, "UQ_PLOAttainmentResults_OfferedCourse_PLO").IsUnique();

            entity.Property(e => e.AttainmentId).HasColumnName("AttainmentID");
            entity.Property(e => e.AttainmentPercentage).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.CalculatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.CalculatedByUserId).HasColumnName("CalculatedByUserID");
            entity.Property(e => e.Ploid).HasColumnName("PLOID");
            entity.Property(e => e.OfferedCourseId).HasColumnName("OfferedCourseID");

            entity.HasOne(d => d.Plo).WithMany()
                .HasForeignKey(d => d.Ploid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PLOAttainmentResults_PLO");
        });

        modelBuilder.Entity<CourseAssessmentTypeClomapping>(entity =>
        {
            entity.HasKey(e => e.MappingId).HasName("PK__CourseAs__8B5781BD30AEC392");

            entity.ToTable("CourseAssessmentTypeCLOMappings");

            entity.HasIndex(e => new { e.ProgramCourseId, e.SessionId, e.AssessmentTypeId, e.Cloid }, "UQ_CourseAssessmentTypeCLOMappings_Session").IsUnique();

            entity.Property(e => e.MappingId).HasColumnName("MappingID");
            entity.Property(e => e.AssessmentTypeId).HasColumnName("AssessmentTypeID");
            entity.Property(e => e.Cloid).HasColumnName("CLOID");
            entity.Property(e => e.Percentage).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.ProgramCourseId).HasColumnName("ProgramCourseID");
            entity.Property(e => e.SessionId).HasColumnName("SessionID");
            entity.Property(e => e.UpdatedByUserId).HasColumnName("UpdatedByUserID");
            entity.Property(e => e.ContentStatus).HasMaxLength(30);
            entity.Property(e => e.SubmissionId).HasColumnName("SubmissionID");

            entity.HasOne(d => d.AssessmentType).WithMany(p => p.CourseAssessmentTypeClomappings)
                .HasForeignKey(d => d.AssessmentTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__CourseAss__Asses__1AD3FDA4");

            entity.HasOne(d => d.Clo).WithMany(p => p.CourseAssessmentTypeClomappings)
                .HasForeignKey(d => d.Cloid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__CourseAss__CLOID__1BC821DD");

            entity.HasOne(d => d.ProgramCourse).WithMany(p => p.CourseAssessmentTypeClomappings)
                .HasForeignKey(d => d.ProgramCourseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__CourseAss__Progr__19DFD96B");
        });

        modelBuilder.Entity<CourseSessionTaskType>(entity =>
        {
            entity.ToTable("CourseSessionTaskTypes");
            entity.HasKey(e => e.CourseSessionTaskTypeId);
            entity.HasIndex(e => new { e.ProgramCourseId, e.SessionId, e.AssessmentTypeId })
                .IsUnique();
            entity.Property(e => e.CourseSessionTaskTypeId).HasColumnName("CourseSessionTaskTypeID");
            entity.Property(e => e.ProgramCourseId).HasColumnName("ProgramCourseID");
            entity.Property(e => e.SessionId).HasColumnName("SessionID");
            entity.Property(e => e.AssessmentTypeId).HasColumnName("AssessmentTypeID");
            entity.Property(e => e.CreatedAt).HasColumnType("datetime");

            entity.HasOne(d => d.AssessmentType)
                .WithMany()
                .HasForeignKey(d => d.AssessmentTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.ProgramCourse)
                .WithMany()
                .HasForeignKey(d => d.ProgramCourseId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<CourseFolder>(entity =>
        {
            entity.HasKey(e => e.CourseFolderId).HasName("PK__CourseFo__FDAC4DF9FD951A01");

            entity.HasIndex(e => e.OfferedCourseId, "UQ__CourseFo__94C9F8E3277A91F5").IsUnique();

            entity.HasIndex(e => e.RootFolderPath, "UQ__CourseFo__F12009065DDD1641").IsUnique();

            entity.Property(e => e.CourseFolderId).HasColumnName("CourseFolderID");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.CreatedByUserId).HasColumnName("CreatedByUserID");
            entity.Property(e => e.FolderStatus)
                .HasMaxLength(50)
                .HasDefaultValue("In Progress");
            entity.Property(e => e.OfferedCourseId).HasColumnName("OfferedCourseID");
            entity.Property(e => e.RootFolderName).HasMaxLength(250);
        });

        modelBuilder.Entity<CourseFolderDirectory>(entity =>
        {
            entity.HasKey(e => e.DirectoryId).HasName("PK__CourseFo__3D93EF029ABF533D");

            entity.HasIndex(e => new { e.CourseFolderId, e.TemplateItemId }, "UQ_CourseFolderDirectories_Template")
                .IsUnique()
                .HasFilter("([TemplateItemID] IS NOT NULL)");

            entity.HasIndex(e => new { e.CourseFolderId, e.DirectoryPath }, "UQ__CourseFo__1C74B39D0664A804").IsUnique();

            entity.Property(e => e.DirectoryId).HasColumnName("DirectoryID");
            entity.Property(e => e.CourseFolderId).HasColumnName("CourseFolderID");
            entity.Property(e => e.DirectoryName).HasMaxLength(250);
            entity.Property(e => e.ParentDirectoryId).HasColumnName("ParentDirectoryID");
            entity.Property(e => e.TemplateItemId).HasColumnName("TemplateItemID");

            entity.HasOne(d => d.CourseFolder).WithMany(p => p.CourseFolderDirectories)
                .HasForeignKey(d => d.CourseFolderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__CourseFol__Cours__00DF2177");

            entity.HasOne(d => d.ParentDirectory).WithMany(p => p.InverseParentDirectory)
                .HasForeignKey(d => d.ParentDirectoryId)
                .HasConstraintName("FK__CourseFol__Paren__01D345B0");

            entity.HasOne(d => d.TemplateItem).WithMany(p => p.CourseFolderDirectories)
                .HasForeignKey(d => d.TemplateItemId)
                .HasConstraintName("FK__CourseFol__Templ__02C769E9");
        });

        modelBuilder.Entity<CourseFolderFile>(entity =>
        {
            entity.HasKey(e => e.FileId).HasName("PK__CourseFo__6F0F989F83E496F3");

            entity.HasIndex(e => e.FilePath, "UQ__CourseFo__48D910BDF277FFBD").IsUnique();

            entity.HasIndex(e => new { e.DirectoryId, e.FileName }, "UQ__CourseFo__F81A09ED14F0FD2B").IsUnique();

            entity.Property(e => e.FileId).HasColumnName("FileID");
            entity.Property(e => e.DirectoryId).HasColumnName("DirectoryID");
            entity.Property(e => e.FileCategory)
                .HasMaxLength(100)
                .HasDefaultValue("Other");
            entity.Property(e => e.FileName).HasMaxLength(250);
            entity.Property(e => e.Remarks).HasMaxLength(500);
            entity.Property(e => e.UploadedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.UploadedByUserId).HasColumnName("UploadedByUserID");

            entity.HasOne(d => d.Directory).WithMany(p => p.CourseFolderFiles)
                .HasForeignKey(d => d.DirectoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__CourseFol__Direc__09746778");
        });

        modelBuilder.Entity<CourseFolderTemplateItem>(entity =>
        {
            entity.HasKey(e => e.TemplateItemId).HasName("PK__CourseFo__681D44EFE3664829");

            entity.HasIndex(e => e.TemplateCode, "UQ__CourseFo__0FDB50816303089A").IsUnique();

            entity.HasIndex(e => e.FolderItemName, "UQ__CourseFo__4EF99A60BB6CC033").IsUnique();

            entity.HasIndex(e => e.SortOrder, "UQ__CourseFo__55A193B7B50E9941").IsUnique();

            entity.Property(e => e.TemplateItemId).HasColumnName("TemplateItemID");
            entity.Property(e => e.FolderItemName).HasMaxLength(200);
            entity.Property(e => e.IsRequired).HasDefaultValue(true);
            entity.Property(e => e.TemplateCode).HasMaxLength(5);
        });

        modelBuilder.Entity<CourseTopic>(entity =>
        {
            entity.HasKey(e => e.TopicId).HasName("PK__CourseTo__022E0F7D90057FFE");

            entity.HasIndex(e => new { e.ProgramCourseId, e.SessionId, e.ParentTopicId, e.TopicTitle }, "UQ_CourseTopics_Session")
                .IsUnique()
                .HasFilter("[SessionID] IS NOT NULL");

            entity.Property(e => e.TopicId).HasColumnName("TopicID");
            entity.Property(e => e.ParentTopicId).HasColumnName("ParentTopicID");
            entity.Property(e => e.ProgramCourseId).HasColumnName("ProgramCourseID");
            entity.Property(e => e.SessionId).HasColumnName("SessionID");
            entity.Property(e => e.TopicDescription).HasMaxLength(1000);
            entity.Property(e => e.TopicTitle).HasMaxLength(300);
            entity.Property(e => e.ContentStatus)
                .HasMaxLength(20)
                .HasDefaultValue("Draft");
            entity.Property(e => e.SubmissionId).HasColumnName("SubmissionID");

            entity.HasOne(d => d.ParentTopic).WithMany(p => p.InverseParentTopic)
                .HasForeignKey(d => d.ParentTopicId)
                .HasConstraintName("FK__CourseTop__Paren__30C33EC3");

            entity.HasOne(d => d.ProgramCourse).WithMany(p => p.CourseTopics)
                .HasForeignKey(d => d.ProgramCourseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__CourseTop__Progr__2FCF1A8A");
        });

        modelBuilder.Entity<CourseTopicClomapping>(entity =>
        {
            entity.HasKey(e => e.TopicClomappingId).HasName("PK__CourseTo__8C4A73A8D370015B");

            entity.ToTable("CourseTopicCLOMappings");

            entity.HasIndex(e => new { e.TopicId, e.Cloid }, "UQ__CourseTo__93E27E8685EA21FD").IsUnique();

            entity.Property(e => e.TopicClomappingId).HasColumnName("TopicCLOMappingID");
            entity.Property(e => e.Cloid).HasColumnName("CLOID");
            entity.Property(e => e.TopicId).HasColumnName("TopicID");

            entity.HasOne(d => d.Clo).WithMany(p => p.CourseTopicClomappings)
                .HasForeignKey(d => d.Cloid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__CourseTop__CLOID__367C1819");

            entity.HasOne(d => d.Topic).WithMany(p => p.CourseTopicClomappings)
                .HasForeignKey(d => d.TopicId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__CourseTop__Topic__3587F3E0");
        });

        modelBuilder.Entity<FolderSubmission>(entity =>
        {
            entity.HasKey(e => e.SubmissionId).HasName("PK__FolderSu__449EE10500D8AF4E");

            entity.Property(e => e.SubmissionId).HasColumnName("SubmissionID");
            entity.Property(e => e.CourseFolderId).HasColumnName("CourseFolderID");
            entity.Property(e => e.Remarks).HasMaxLength(500);
            entity.Property(e => e.SubmissionStatus)
                .HasMaxLength(50)
                .HasDefaultValue("Submitted");
            entity.Property(e => e.SubmittedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.SubmittedByUserId).HasColumnName("SubmittedByUserID");

            entity.HasOne(d => d.CourseFolder).WithMany(p => p.FolderSubmissions)
                .HasForeignKey(d => d.CourseFolderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__FolderSub__Cours__251C81ED");
        });

        modelBuilder.Entity<FolderSubmissionIssue>(entity =>
        {
            entity.HasKey(e => e.IssueId).HasName("PK__FolderSu__6C861624C30BCCA3");

            entity.Property(e => e.IssueId).HasColumnName("IssueID");
            entity.Property(e => e.IssueMessage).HasMaxLength(500);
            entity.Property(e => e.SubmissionId).HasColumnName("SubmissionID");

            entity.HasOne(d => d.Submission).WithMany(p => p.FolderSubmissionIssues)
                .HasForeignKey(d => d.SubmissionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__FolderSub__Submi__2AD55B43");
        });

        modelBuilder.Entity<GraderTask>(entity =>
        {
            entity.HasKey(e => e.GraderTaskId).HasName("PK__GraderTa__94232B22D762B3E0");

            entity.Property(e => e.GraderTaskId).HasColumnName("GraderTaskID");
            entity.Property(e => e.AssessmentId).HasColumnName("AssessmentID");
            entity.Property(e => e.AssignedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.AssignedByUserId).HasColumnName("AssignedByUserID");
            entity.Property(e => e.GraderAllocationId).HasColumnName("GraderAllocationID");
            entity.Property(e => e.Instructions).HasMaxLength(1000);
            entity.Property(e => e.SourceFileId).HasColumnName("SourceFileID");
            entity.Property(e => e.TaskStatus)
                .HasMaxLength(50)
                .HasDefaultValue("Pending");

            entity.HasOne(d => d.Assessment).WithMany(p => p.GraderTasks)
                .HasForeignKey(d => d.AssessmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__GraderTas__Asses__1C873BEC");

            entity.HasOne(d => d.SourceFile).WithMany(p => p.GraderTasks)
                .HasForeignKey(d => d.SourceFileId)
                .HasConstraintName("FK__GraderTas__Sourc__1D7B6025");
        });

        modelBuilder.Entity<LessonPlan>(entity =>
        {
            entity.HasKey(e => e.LessonPlanId).HasName("PK__LessonPl__0229AF7D59440ADC");

            entity.HasIndex(e => new { e.OfferedCourseId, e.LessonPlanNo }, "UQ__LessonPl__E4EB525184D65FAA").IsUnique();

            entity.Property(e => e.LessonPlanId).HasColumnName("LessonPlanID");
            entity.Property(e => e.FileId).HasColumnName("FileID");
            entity.Property(e => e.LessonPlanNo).HasMaxLength(50);
            entity.Property(e => e.LessonPlanTitle).HasMaxLength(200);
            entity.Property(e => e.OfferedCourseId).HasColumnName("OfferedCourseID");
            entity.Property(e => e.Topics).HasMaxLength(1000);
            entity.Property(e => e.UploadedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.UploadedByUserId).HasColumnName("UploadedByUserID");

            entity.HasOne(d => d.File).WithMany(p => p.LessonPlans)
                .HasForeignKey(d => d.FileId)
                .HasConstraintName("FK__LessonPla__FileI__16CE6296");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.NotificationId).HasName("PK__Notifica__20CF2E328E87C7A5");

            entity.Property(e => e.NotificationId).HasColumnName("NotificationID");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.NotificationMessage).HasMaxLength(1000);
            entity.Property(e => e.NotificationTitle).HasMaxLength(200);
            entity.Property(e => e.NotificationType).HasMaxLength(100);
            entity.Property(e => e.RelatedId).HasColumnName("RelatedID");
            entity.Property(e => e.UserId).HasColumnName("UserID");
        });

        modelBuilder.Entity<OfferedCourseTopicProgress>(entity =>
        {
            entity.HasKey(e => e.ProgressId).HasName("PK__OfferedC__BAE29C851D5D8F04");

            entity.ToTable("OfferedCourseTopicProgress");

            entity.HasIndex(e => new { e.OfferedCourseId, e.TopicId }, "UQ__OfferedC__54EB18146A4CC299").IsUnique();

            entity.Property(e => e.ProgressId).HasColumnName("ProgressID");
            entity.Property(e => e.CompletedAt).HasColumnType("datetime");
            entity.Property(e => e.CompletedByUserId).HasColumnName("CompletedByUserID");
            entity.Property(e => e.OfferedCourseId).HasColumnName("OfferedCourseID");
            entity.Property(e => e.TopicId).HasColumnName("TopicID");

            entity.HasOne(d => d.Topic).WithMany(p => p.OfferedCourseTopicProgresses)
                .HasForeignKey(d => d.TopicId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__OfferedCo__Topic__3C34F16F");
        });

        modelBuilder.Entity<Ploclomapping>(entity =>
        {
            entity.HasKey(e => e.MappingId).HasName("PK__PLOCLOMa__8B5781BD0C831191");

            entity.ToTable("PLOCLOMappings");

            entity.HasIndex(e => new { e.ProgramCourseId, e.SessionId, e.Ploid, e.Cloid }, "UQ_PLOCLOMappings_Session")
                .IsUnique()
                .HasFilter("[SessionID] IS NOT NULL");

            entity.Property(e => e.MappingId).HasColumnName("MappingID");
            entity.Property(e => e.Cloid).HasColumnName("CLOID");
            entity.Property(e => e.Percentage).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.Ploid).HasColumnName("PLOID");
            entity.Property(e => e.ProgramCourseId).HasColumnName("ProgramCourseID");
            entity.Property(e => e.SessionId).HasColumnName("SessionID");
            entity.Property(e => e.UpdatedByUserId).HasColumnName("UpdatedByUserID");
            entity.Property(e => e.ContentStatus)
                .HasMaxLength(20)
                .HasDefaultValue("Draft");
            entity.Property(e => e.SubmissionId).HasColumnName("SubmissionID");

            entity.HasOne(d => d.Clo).WithMany(p => p.Ploclomappings)
                .HasForeignKey(d => d.Cloid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PLOCLOMap__CLOID__236943A5");

            entity.HasOne(d => d.Plo).WithMany()
                .HasForeignKey(d => d.Ploid)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.ProgramCourse).WithMany(p => p.Ploclomappings)
                .HasForeignKey(d => d.ProgramCourseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PLOCLOMap__Progr__2180FB33");
        });

        modelBuilder.Entity<ProgramCoursePlomapping>(entity =>
        {
            entity.HasKey(e => e.MappingId).HasName("PK__ProgramC__8B5781BD24E29D87");

            entity.ToTable("ProgramCoursePLOMappings");

            entity.HasIndex(e => new { e.ProgramCourseId, e.Ploid }, "UQ__ProgramC__BC9B98F37B4AE5D6").IsUnique();

            entity.Property(e => e.MappingId).HasColumnName("MappingID");
            entity.Property(e => e.MappedByUserId).HasColumnName("MappedByUserID");
            entity.Property(e => e.Percentage).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.Ploid).HasColumnName("PLOID");
            entity.Property(e => e.ProgramCourseId).HasColumnName("ProgramCourseID");

            entity.HasOne(d => d.ProgramCourse).WithMany(p => p.ProgramCoursePlomappings)
                .HasForeignKey(d => d.ProgramCourseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ProgramCo__Progr__29221CFB");
        });

        modelBuilder.Entity<PeoPlomapping>(entity =>
        {
            entity.HasKey(e => e.MappingId);

            entity.ToTable("PEOPLEMappings");

            entity.Property(e => e.MappingId).HasColumnName("MappingID");
            entity.Property(e => e.IsMapped).HasColumnName("IsMapped");
            entity.Property(e => e.MappedByUserId).HasColumnName("MappedByUserID");
            entity.Property(e => e.Peoid).HasColumnName("PEOID");
            entity.Property(e => e.Ploid).HasColumnName("PLOID");
        });

        modelBuilder.Entity<StudentAssessmentMark>(entity =>
        {
            entity.HasKey(e => e.StudentAssessmentMarkId).HasName("PK__StudentA__B2846D6B892F6E89");

            entity.HasIndex(e => new { e.AssessmentId, e.StudentId }, "UQ__StudentA__BE07AA98AD93FF00").IsUnique();

            entity.Property(e => e.StudentAssessmentMarkId).HasColumnName("StudentAssessmentMarkID");
            entity.Property(e => e.AssessmentId).HasColumnName("AssessmentID");
            entity.Property(e => e.ObtainedMarks).HasColumnType("decimal(6, 2)");
            entity.Property(e => e.StudentId).HasColumnName("StudentID");

            entity.HasOne(d => d.Assessment).WithMany(p => p.StudentAssessmentMarks)
                .HasForeignKey(d => d.AssessmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__StudentAs__Asses__5F7E2DAC");
        });

        modelBuilder.Entity<StudentQuestionMark>(entity =>
        {
            entity.HasKey(e => e.StudentQuestionMarkId).HasName("PK__StudentQ__C727C7E31E776139");

            entity.HasIndex(e => new { e.QuestionId, e.StudentId }, "UQ__StudentQ__8EEC3D2AE7A1A877").IsUnique();

            entity.Property(e => e.StudentQuestionMarkId).HasColumnName("StudentQuestionMarkID");
            entity.Property(e => e.ObtainedMarks).HasColumnType("decimal(6, 2)");
            entity.Property(e => e.QuestionId).HasColumnName("QuestionID");
            entity.Property(e => e.StudentId).HasColumnName("StudentID");

            entity.HasOne(d => d.Question).WithMany(p => p.StudentQuestionMarks)
                .HasForeignKey(d => d.QuestionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__StudentQu__Quest__65370702");
        });

        modelBuilder.Entity<TeacherPermission>(entity =>
        {
            entity.HasKey(e => e.PermissionId).HasName("PK__TeacherP__EFA6FB0F19522E88");

            entity.HasIndex(e => new { e.TeacherUserId, e.ProgramCourseId, e.PermissionTypeId }, "UQ__TeacherP__BB5329FA23F1DA3A").IsUnique();

            entity.Property(e => e.PermissionId).HasColumnName("PermissionID");
            entity.Property(e => e.HoduserId).HasColumnName("HODUserID");
            entity.Property(e => e.PermissionTypeId).HasColumnName("PermissionTypeID");
            entity.Property(e => e.ProgramCourseId).HasColumnName("ProgramCourseID");
            entity.Property(e => e.TeacherUserId).HasColumnName("TeacherUserID");

            entity.HasOne(d => d.PermissionType).WithMany(p => p.TeacherPermissions)
                .HasForeignKey(d => d.PermissionTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__TeacherPe__Permi__0E6E26BF");

            entity.HasOne(d => d.ProgramCourse).WithMany(p => p.TeacherPermissions)
                .HasForeignKey(d => d.ProgramCourseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__TeacherPe__Progr__0D7A0286");
        });

        modelBuilder.Entity<TeacherPermissionType>(entity =>
        {
            entity.HasKey(e => e.PermissionTypeId).HasName("PK__TeacherP__53B420EFF8FD83EB");

            entity.HasIndex(e => e.PermissionType, "UQ__TeacherP__F43A2A63405D37A8").IsUnique();

            entity.Property(e => e.PermissionTypeId).HasColumnName("PermissionTypeID");
            entity.Property(e => e.PermissionType).HasMaxLength(100);
        });

        modelBuilder.Entity<ContentSubmissionType>(entity =>
        {
            entity.HasKey(e => e.ContentTypeId);
            entity.ToTable("ContentSubmissionTypes");
            entity.Property(e => e.ContentTypeId).HasColumnName("ContentTypeID");
            entity.Property(e => e.ContentTypeCode).HasMaxLength(50);
            entity.Property(e => e.ContentTypeName).HasMaxLength(100);
        });

        modelBuilder.Entity<ProgramCourseContentSubmission>(entity =>
        {
            entity.HasKey(e => e.SubmissionId);
            entity.ToTable("ProgramCourseContentSubmissions");
            entity.Property(e => e.SubmissionId).HasColumnName("SubmissionID");
            entity.Property(e => e.ProgramCourseId).HasColumnName("ProgramCourseID");
            entity.Property(e => e.SessionId).HasColumnName("SessionID");
            entity.Property(e => e.ContentTypeId).HasColumnName("ContentTypeID");
            entity.Property(e => e.SubmittedByUserId).HasColumnName("SubmittedByUserID");
            entity.Property(e => e.SubmittedAt).HasColumnType("datetime");
            entity.Property(e => e.SubmissionStatus).HasMaxLength(30);
            entity.Property(e => e.ReviewedByUserId).HasColumnName("ReviewedByUserID");
            entity.Property(e => e.ReviewedAt).HasColumnType("datetime");
            entity.Property(e => e.HodFeedback).HasMaxLength(1000);

            entity.HasOne(d => d.ContentType).WithMany(p => p.Submissions)
                .HasForeignKey(d => d.ContentTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.Session).WithMany()
                .HasForeignKey(d => d.SessionId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.SubmittedByUser).WithMany()
                .HasForeignKey(d => d.SubmittedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.ReviewedByUser).WithMany()
                .HasForeignKey(d => d.ReviewedByUserId);
        });

        modelBuilder.Entity<OfferedCourseResultScheme>(entity =>
        {
            entity.ToTable("OfferedCourseResultSchemes");
            entity.HasIndex(e => e.OfferedCourseId).IsUnique();
            entity.Property(e => e.TotalMarks).HasColumnType("decimal(6, 2)");
            entity.Property(e => e.QuizMarks).HasColumnType("decimal(6, 2)");
            entity.Property(e => e.HomeworkMarks).HasColumnType("decimal(6, 2)");
            entity.Property(e => e.MidMarks).HasColumnType("decimal(6, 2)");
            entity.Property(e => e.LabMarks).HasColumnType("decimal(6, 2)");
            entity.Property(e => e.FinalTheoryMarks).HasColumnType("decimal(6, 2)");
            entity.Property(e => e.FinalLabMarks).HasColumnType("decimal(6, 2)");
            entity.Property(e => e.AttendanceHomeworkMarks).HasColumnType("decimal(6, 2)");
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime");
            entity.Property(e => e.LastGeneratedAt).HasColumnType("datetime");
        });

        modelBuilder.Entity<StudentCourseResult>(entity =>
        {
            entity.ToTable("StudentCourseResults");
            entity.HasIndex(e => new { e.OfferedCourseId, e.StudentId }).IsUnique();
            entity.Property(e => e.QuizMarks).HasColumnType("decimal(6, 2)");
            entity.Property(e => e.HomeworkMarks).HasColumnType("decimal(6, 2)");
            entity.Property(e => e.MidMarks).HasColumnType("decimal(6, 2)");
            entity.Property(e => e.LabMarks).HasColumnType("decimal(6, 2)");
            entity.Property(e => e.FinalTheoryMarks).HasColumnType("decimal(6, 2)");
            entity.Property(e => e.FinalLabMarks).HasColumnType("decimal(6, 2)");
            entity.Property(e => e.TotalObtained).HasColumnType("decimal(6, 2)");
            entity.Property(e => e.TotalMax).HasColumnType("decimal(6, 2)");
            entity.Property(e => e.Percentage).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.GeneratedAt).HasColumnType("datetime");
        });

        modelBuilder.Entity<StudentComponentMark>(entity =>
        {
            entity.ToTable("StudentComponentMarks");
            entity.HasIndex(e => new { e.OfferedCourseId, e.StudentId }).IsUnique();
            entity.Property(e => e.AttendancePercent).HasColumnType("decimal(6, 2)");
            entity.Property(e => e.LabTaskObtained).HasColumnType("decimal(6, 2)");
            entity.Property(e => e.ProjectObtained).HasColumnType("decimal(6, 2)");
            entity.Property(e => e.ExtraMarksJson).HasColumnType("nvarchar(max)");
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime2");
        });

        modelBuilder.Entity<OfferedCourseLabMarkScheme>(entity =>
        {
            entity.ToTable("OfferedCourseLabMarkSchemes");
            entity.Property(e => e.QuizMarks).HasColumnType("decimal(6, 2)");
            entity.Property(e => e.AssignmentMarks).HasColumnType("decimal(6, 2)");
            entity.Property(e => e.LabTaskMarks).HasColumnType("decimal(6, 2)");
            entity.Property(e => e.ProjectMarks).HasColumnType("decimal(6, 2)");
            entity.Property(e => e.AttendanceMarks).HasColumnType("decimal(6, 2)");
            entity.Property(e => e.ExamMarks).HasColumnType("decimal(6, 2)");
            entity.Property(e => e.ExtraItemsJson).HasColumnType("nvarchar(max)");
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime");
        });

        modelBuilder.Entity<OfferedCourseHomeworkMarkScheme>(entity =>
        {
            entity.ToTable("OfferedCourseHomeworkMarkSchemes");
            entity.Property(e => e.AttendanceMarks).HasColumnType("decimal(6, 2)");
            entity.Property(e => e.QuizMarks).HasColumnType("decimal(6, 2)");
            entity.Property(e => e.AssignmentMarks).HasColumnType("decimal(6, 2)");
            entity.Property(e => e.ExtraItemsJson).HasColumnType("nvarchar(max)");
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime");
        });

        modelBuilder.Entity<StudentCourseResultSelection>(entity =>
        {
            entity.ToTable("StudentCourseResultSelections");
            entity.HasIndex(e => new { e.OfferedCourseId, e.StudentId, e.AssessmentId }).IsUnique();
            entity.Property(e => e.Category).HasMaxLength(20);
        });

        modelBuilder.Entity<CourseCatalogVersion>(entity =>
        {
            entity.ToTable("CourseCatalogVersions");
            entity.HasIndex(e => new { e.SourceProgramId, e.CreatedAt });
            entity.HasIndex(e => new { e.SourceProgramId, e.ContentFingerprint });
            entity.Property(e => e.VersionLabel).HasMaxLength(200);
            entity.Property(e => e.SourceFileName).HasMaxLength(260);
            entity.Property(e => e.ContentFingerprint).HasMaxLength(64);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime2");
            entity.HasOne(e => e.SourceProgram)
                .WithMany()
                .HasForeignKey(e => e.SourceProgramId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CourseCatalogVersionItem>(entity =>
        {
            entity.ToTable("CourseCatalogVersionItems");
            entity.HasIndex(e => new { e.CatalogVersionId, e.DisplayOrder });
            entity.Property(e => e.CourseCode).HasMaxLength(50);
            entity.Property(e => e.CourseTitle).HasMaxLength(200);
            entity.HasOne(e => e.Version)
                .WithMany(v => v.Items)
                .HasForeignKey(e => e.CatalogVersionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OutcomeCatalogVersion>(entity =>
        {
            entity.ToTable("OutcomeCatalogVersions");
            entity.HasIndex(e => new { e.OutcomeType, e.SourceProgramId, e.CreatedAt });
            entity.HasIndex(e => new { e.OutcomeType, e.SourceProgramId, e.ContentFingerprint });
            entity.Property(e => e.OutcomeType).HasMaxLength(10);
            entity.Property(e => e.VersionLabel).HasMaxLength(200);
            entity.Property(e => e.SourceFileName).HasMaxLength(260);
            entity.Property(e => e.ContentFingerprint).HasMaxLength(64);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime2");
            entity.HasOne(e => e.SourceProgram)
                .WithMany()
                .HasForeignKey(e => e.SourceProgramId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OutcomeCatalogVersionItem>(entity =>
        {
            entity.ToTable("OutcomeCatalogVersionItems");
            entity.HasIndex(e => new { e.OutcomeCatalogVersionId, e.DisplayOrder });
            entity.Property(e => e.ItemCode).HasMaxLength(20);
            entity.Property(e => e.ItemDescription).HasMaxLength(1000);
            entity.HasOne(e => e.Version)
                .WithMany(v => v.Items)
                .HasForeignKey(e => e.OutcomeCatalogVersionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
