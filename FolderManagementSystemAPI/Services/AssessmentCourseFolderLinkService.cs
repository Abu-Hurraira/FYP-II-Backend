using System.Text.RegularExpressions;
using FolderManagementSystemAPI.Controllers;
using FolderManagementSystemAPI.Models;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models.TeacherScaffold;

namespace FolderManagementSystemAPI.Services;

/// <summary>
/// Keeps AssessmentFiles in sync when teachers upload g/h/i PDFs from the course folder UI
/// (same master paths as quiz/assignment upload screens).
/// </summary>
public class AssessmentCourseFolderLinkService
{
    private static readonly Regex AssessmentKeyFromFileName = new(
        @"^A(\d+)_",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly FolderManagementDbContext _context;

    public AssessmentCourseFolderLinkService(FolderManagementDbContext context)
    {
        _context = context;
    }

    public async Task TryLinkFromFolderUploadAsync(
        int offeredCourseId,
        int fileId,
        string fileName,
        string fileCategory,
        string? remarks = null)
    {
        if (!TryMapFileCategory(fileCategory, out var assessmentTypeName, out var filePurpose))
        {
            return;
        }

        var assessment = await ResolveAssessmentAsync(
            offeredCourseId,
            fileName,
            remarks,
            assessmentTypeName,
            filePurpose);

        if (assessment == null)
        {
            return;
        }

        var existingLinks = await _context.AssessmentFiles
            .Where(af => af.AssessmentId == assessment.AssessmentId && af.FilePurpose == filePurpose)
            .ToListAsync();

        if (existingLinks.Count > 0)
        {
            _context.AssessmentFiles.RemoveRange(existingLinks);
        }

        _context.AssessmentFiles.Add(new AssessmentFile
        {
            AssessmentId = assessment.AssessmentId,
            FileId = fileId,
            FilePurpose = filePurpose
        });

        await _context.SaveChangesAsync();
    }

    private async Task<Assessment?> ResolveAssessmentAsync(
        int offeredCourseId,
        string fileName,
        string? remarks,
        string assessmentTypeName,
        string filePurpose)
    {
        if (TryParseAssessmentIdFromRemarks(remarks, out var assessmentIdFromRemarks))
        {
            var byRemarks = await _context.Assessments
                .Include(a => a.AssessmentType)
                .FirstOrDefaultAsync(a =>
                    a.AssessmentId == assessmentIdFromRemarks &&
                    a.OfferedCourseId == offeredCourseId);

            if (byRemarks != null &&
                string.Equals(
                    byRemarks.AssessmentType?.AssessmentTypeName,
                    assessmentTypeName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return byRemarks;
            }
        }

        var match = AssessmentKeyFromFileName.Match(fileName ?? string.Empty);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var key))
        {
            var byKey = await _context.Assessments
                .Include(a => a.AssessmentType)
                .Where(a =>
                    a.OfferedCourseId == offeredCourseId &&
                    a.AssessmentType!.AssessmentTypeName == assessmentTypeName)
                .FirstOrDefaultAsync(a => a.AssessmentNo == key || a.AssessmentId == key);

            if (byKey != null)
            {
                return byKey;
            }
        }

        if (string.Equals(filePurpose, AssessmentFolderSyncService.PurposeQuestion, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var candidates = await _context.Assessments
            .Include(a => a.AssessmentType)
            .Include(a => a.AssessmentFiles)
            .Where(a =>
                a.OfferedCourseId == offeredCourseId &&
                a.AssessmentType!.AssessmentTypeName == assessmentTypeName)
            .ToListAsync();

        var withoutPurpose = candidates
            .Where(a => !a.AssessmentFiles.Any(f =>
                string.Equals(f.FilePurpose, filePurpose, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        return withoutPurpose.Count == 1 ? withoutPurpose[0] : null;
    }

    private static bool TryParseAssessmentIdFromRemarks(string? remarks, out int assessmentId)
    {
        assessmentId = 0;
        if (string.IsNullOrWhiteSpace(remarks))
        {
            return false;
        }

        var match = Regex.Match(remarks, @"AssessmentID=(\d+)", RegexOptions.IgnoreCase);
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out assessmentId))
        {
            return false;
        }

        return true;
    }

    private static bool TryMapFileCategory(
        string fileCategory,
        out string assessmentTypeName,
        out string filePurpose)
    {
        assessmentTypeName = string.Empty;
        filePurpose = string.Empty;

        if (string.IsNullOrWhiteSpace(fileCategory))
        {
            return false;
        }

        var category = fileCategory.Trim();

        if (category.EndsWith(" Solution", StringComparison.OrdinalIgnoreCase))
        {
            filePurpose = AssessmentsController.PurposeSolution;
            return TryMapTypePrefix(category[..^" Solution".Length], out assessmentTypeName);
        }

        if (category.EndsWith(" Best Sample", StringComparison.OrdinalIgnoreCase))
        {
            filePurpose = AssessmentsController.PurposeBestSample;
            return TryMapTypePrefix(category[..^" Best Sample".Length], out assessmentTypeName);
        }

        if (category.EndsWith(" Average Sample", StringComparison.OrdinalIgnoreCase))
        {
            filePurpose = AssessmentsController.PurposeAverageSample;
            return TryMapTypePrefix(category[..^" Average Sample".Length], out assessmentTypeName);
        }

        if (category.EndsWith(" Worst Sample", StringComparison.OrdinalIgnoreCase))
        {
            filePurpose = AssessmentsController.PurposeWorstSample;
            return TryMapTypePrefix(category[..^" Worst Sample".Length], out assessmentTypeName);
        }

        if (category.EndsWith(" Question", StringComparison.OrdinalIgnoreCase))
        {
            filePurpose = AssessmentFolderSyncService.PurposeQuestion;
            return TryMapTypePrefix(category[..^" Question".Length], out assessmentTypeName);
        }

        return false;
    }

    private static bool TryMapTypePrefix(string prefix, out string assessmentTypeName)
    {
        assessmentTypeName = prefix.Trim() switch
        {
            "Quiz" => AssessmentsControllerTypeNames.Quiz,
            "Mid Exam" => AssessmentsControllerTypeNames.Mid,
            "Final Exam" => AssessmentsControllerTypeNames.Final,
            "Assignment" => AssessmentsControllerTypeNames.Assignment,
            _ => string.Empty
        };

        return !string.IsNullOrEmpty(assessmentTypeName);
    }
}
