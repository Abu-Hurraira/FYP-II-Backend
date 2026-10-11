using System.Security.Cryptography;
using System.Text;
using FolderManagementSystemAPI.DTOs;
using FolderManagementSystemAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace FolderManagementSystemAPI.Services;

public class OutcomeCatalogVersionService
{
    private readonly FolderManagementDbContext _context;

    public OutcomeCatalogVersionService(FolderManagementDbContext context)
    {
        _context = context;
    }

    public async Task<List<OutcomeCatalogVersionListItemDto>> ListVersionsAsync(string outcomeType)
    {
        var type = OutcomeCatalogTypes.Normalize(outcomeType);

        return await _context.OutcomeCatalogVersions
            .AsNoTracking()
            .Include(v => v.SourceProgram)
            .Where(v => v.OutcomeType == type)
            .OrderByDescending(v => v.CreatedAt)
            .ThenByDescending(v => v.OutcomeCatalogVersionId)
            .Select(v => new OutcomeCatalogVersionListItemDto
            {
                OutcomeCatalogVersionId = v.OutcomeCatalogVersionId,
                OutcomeType = v.OutcomeType,
                VersionLabel = v.VersionLabel,
                SourceFileName = v.SourceFileName,
                ItemCount = v.ItemCount,
                SourceProgramId = v.SourceProgramId,
                SourceProgramName = v.SourceProgram != null ? v.SourceProgram.ProgramName : string.Empty,
                SourceProgramCode = v.SourceProgram != null ? v.SourceProgram.ProgramCode : null,
                CreatedAt = v.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<OutcomeCatalogVersionDetailDto?> GetVersionDetailAsync(
        int outcomeCatalogVersionId,
        int? targetProgramId)
    {
        var version = await _context.OutcomeCatalogVersions
            .AsNoTracking()
            .Include(v => v.SourceProgram)
            .Include(v => v.Items)
            .FirstOrDefaultAsync(v => v.OutcomeCatalogVersionId == outcomeCatalogVersionId);

        if (version == null)
        {
            return null;
        }

        var linkedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var linkedDescriptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (targetProgramId.HasValue)
        {
            if (version.OutcomeType == OutcomeCatalogTypes.Plo)
            {
                var rows = await _context.Plos.AsNoTracking()
                    .Where(p => p.ProgramId == targetProgramId.Value)
                    .Select(p => new { p.Plocode, p.Plodescription })
                    .ToListAsync();

                foreach (var row in rows)
                {
                    linkedCodes.Add(NormalizeKey(row.Plocode));
                    linkedDescriptions.Add(NormalizeKey(row.Plodescription));
                }
            }
            else
            {
                var rows = await _context.Peos.AsNoTracking()
                    .Where(p => p.ProgramId == targetProgramId.Value)
                    .Select(p => new { p.Peocode, p.Peodescription })
                    .ToListAsync();

                foreach (var row in rows)
                {
                    linkedCodes.Add(NormalizeKey(row.Peocode));
                    linkedDescriptions.Add(NormalizeKey(row.Peodescription));
                }
            }
        }

        return new OutcomeCatalogVersionDetailDto
        {
            OutcomeCatalogVersionId = version.OutcomeCatalogVersionId,
            OutcomeType = version.OutcomeType,
            VersionLabel = version.VersionLabel,
            SourceFileName = version.SourceFileName,
            ItemCount = version.ItemCount,
            SourceProgramId = version.SourceProgramId,
            SourceProgramName = version.SourceProgram?.ProgramName ?? string.Empty,
            SourceProgramCode = version.SourceProgram?.ProgramCode,
            CreatedAt = version.CreatedAt,
            Items = version.Items
                .OrderBy(i => i.DisplayOrder)
                .ThenBy(i => i.ItemCode)
                .Select(i => new OutcomeCatalogVersionItemDto
                {
                    OutcomeCatalogVersionItemId = i.OutcomeCatalogVersionItemId,
                    ItemCode = i.ItemCode,
                    ItemDescription = i.ItemDescription,
                    DisplayOrder = i.DisplayOrder,
                    AlreadyInProgram =
                        linkedCodes.Contains(NormalizeKey(i.ItemCode)) ||
                        linkedDescriptions.Contains(NormalizeKey(i.ItemDescription))
                })
                .ToList()
        };
    }

    public async Task<(OutcomeCatalogVersion Version, bool CreatedNew)> EnsureVersionFromSnapshotAsync(
        string outcomeType,
        int sourceProgramId,
        string? sourceFileName,
        IReadOnlyList<OutcomeCatalogSnapshotItem> items,
        int? createdByUserId = null)
    {
        var type = OutcomeCatalogTypes.Normalize(outcomeType);

        var normalized = items
            .Where(i => !string.IsNullOrWhiteSpace(i.ItemCode) && !string.IsNullOrWhiteSpace(i.ItemDescription))
            .Select(i => new OutcomeCatalogSnapshotItem
            {
                ItemCode = i.ItemCode.Trim(),
                ItemDescription = i.ItemDescription.Trim()
            })
            .GroupBy(i => NormalizeKey(i.ItemCode))
            .Select(g => g.First())
            .OrderBy(i => NormalizeKey(i.ItemCode), StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalized.Count == 0)
        {
            throw new InvalidOperationException("Cannot create an outcome catalog version with no items.");
        }

        var fingerprint = BuildFingerprint(normalized);
        var latest = await _context.OutcomeCatalogVersions
            .Where(v => v.OutcomeType == type && v.SourceProgramId == sourceProgramId)
            .OrderByDescending(v => v.CreatedAt)
            .ThenByDescending(v => v.OutcomeCatalogVersionId)
            .FirstOrDefaultAsync();

        if (latest != null &&
            string.Equals(latest.ContentFingerprint, fingerprint, StringComparison.OrdinalIgnoreCase))
        {
            return (latest, false);
        }

        var program = await _context.AcademicPrograms
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProgramId == sourceProgramId);

        var programTag = !string.IsNullOrWhiteSpace(program?.ProgramCode)
            ? program!.ProgramCode
            : (program?.ProgramName ?? $"Program {sourceProgramId}");

        var createdAt = DateTime.UtcNow;
        var label = $"{type} · {programTag} · {normalized.Count} · {createdAt:dd MMM yyyy}";

        var version = new OutcomeCatalogVersion
        {
            OutcomeType = type,
            SourceProgramId = sourceProgramId,
            VersionLabel = label,
            SourceFileName = string.IsNullOrWhiteSpace(sourceFileName)
                ? null
                : Path.GetFileName(sourceFileName.Trim()),
            ItemCount = normalized.Count,
            ContentFingerprint = fingerprint,
            CreatedAt = createdAt,
            CreatedByUserId = createdByUserId
        };

        for (var i = 0; i < normalized.Count; i++)
        {
            var item = normalized[i];
            version.Items.Add(new OutcomeCatalogVersionItem
            {
                ItemCode = item.ItemCode,
                ItemDescription = item.ItemDescription,
                DisplayOrder = i + 1
            });
        }

        _context.OutcomeCatalogVersions.Add(version);
        await _context.SaveChangesAsync();
        return (version, true);
    }

    public async Task<(bool Ok, string Message, ApplyOutcomeCatalogVersionResultDto? Result)> ApplyVersionAsync(
        int outcomeCatalogVersionId,
        ApplyOutcomeCatalogVersionDto dto)
    {
        if (dto.TargetProgramId <= 0)
        {
            return (false, "Target program is required.", null);
        }

        var programExists = await _context.AcademicPrograms
            .AnyAsync(p => p.ProgramId == dto.TargetProgramId);
        if (!programExists)
        {
            return (false, "Target program was not found.", null);
        }

        var version = await _context.OutcomeCatalogVersions
            .AsNoTracking()
            .Include(v => v.Items)
            .FirstOrDefaultAsync(v => v.OutcomeCatalogVersionId == outcomeCatalogVersionId);

        if (version == null)
        {
            return (false, "Outcome catalog version was not found.", null);
        }

        var items = version.Items.OrderBy(i => i.DisplayOrder).ToList();
        if (dto.SelectedItemIds != null && dto.SelectedItemIds.Count > 0)
        {
            var selected = dto.SelectedItemIds.ToHashSet();
            items = items.Where(i => selected.Contains(i.OutcomeCatalogVersionItemId)).ToList();
        }

        if (items.Count == 0)
        {
            return (false, "Select at least one item to add.", null);
        }

        var added = 0;
        var alreadyLinked = 0;
        var skipped = 0;

        if (version.OutcomeType == OutcomeCatalogTypes.Plo)
        {
            var existing = await _context.Plos
                .Where(p => p.ProgramId == dto.TargetProgramId)
                .ToListAsync();

            foreach (var item in items)
            {
                if (string.IsNullOrWhiteSpace(item.ItemCode) || string.IsNullOrWhiteSpace(item.ItemDescription))
                {
                    skipped++;
                    continue;
                }

                var codeKey = NormalizeKey(item.ItemCode);
                var descKey = NormalizeKey(item.ItemDescription);
                if (existing.Any(p =>
                        NormalizeKey(p.Plocode) == codeKey ||
                        NormalizeKey(p.Plodescription) == descKey))
                {
                    alreadyLinked++;
                    continue;
                }

                var row = new Plo
                {
                    ProgramId = dto.TargetProgramId,
                    Plocode = item.ItemCode.Trim(),
                    Plodescription = item.ItemDescription.Trim()
                };
                _context.Plos.Add(row);
                existing.Add(row);
                added++;
            }
        }
        else
        {
            var existing = await _context.Peos
                .Where(p => p.ProgramId == dto.TargetProgramId)
                .ToListAsync();

            foreach (var item in items)
            {
                if (string.IsNullOrWhiteSpace(item.ItemCode) || string.IsNullOrWhiteSpace(item.ItemDescription))
                {
                    skipped++;
                    continue;
                }

                var codeKey = NormalizeKey(item.ItemCode);
                var descKey = NormalizeKey(item.ItemDescription);
                if (existing.Any(p =>
                        NormalizeKey(p.Peocode) == codeKey ||
                        NormalizeKey(p.Peodescription) == descKey))
                {
                    alreadyLinked++;
                    continue;
                }

                var row = new Peo
                {
                    ProgramId = dto.TargetProgramId,
                    Peocode = item.ItemCode.Trim(),
                    Peodescription = item.ItemDescription.Trim()
                };
                _context.Peos.Add(row);
                existing.Add(row);
                added++;
            }
        }

        if (added > 0)
        {
            await _context.SaveChangesAsync();
        }

        var result = new ApplyOutcomeCatalogVersionResultDto
        {
            AddedCount = added,
            AlreadyLinkedCount = alreadyLinked,
            SkippedCount = skipped,
            Message = $"Added {added} · Already linked {alreadyLinked} · Skipped {skipped}"
        };

        return (true, result.Message, result);
    }

    public static string BuildFingerprint(IEnumerable<OutcomeCatalogSnapshotItem> items)
    {
        var sb = new StringBuilder();
        foreach (var item in items
                     .OrderBy(i => NormalizeKey(i.ItemCode), StringComparer.OrdinalIgnoreCase))
        {
            sb.Append(NormalizeKey(item.ItemCode));
            sb.Append('|');
            sb.Append(NormalizeKey(item.ItemDescription));
            sb.Append(';');
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(bytes);
    }

    private static string NormalizeKey(string? value) =>
        (value ?? string.Empty).Trim().ToUpperInvariant();
}
