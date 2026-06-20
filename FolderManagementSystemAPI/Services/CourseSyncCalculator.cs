namespace FolderManagementSystemAPI.Services;

public static class CourseSyncCalculator
{
    public sealed class SyncMetrics
    {
        public int TotalTopics { get; init; }
        public int CoveredTopics { get; init; }
        public int GapTopics { get; init; }
        public int AlignedTopics { get; init; }
        public decimal ProgressPercentage { get; init; }
        public decimal SyncScore { get; init; }
        public string DeviationLabel { get; init; } = string.Empty;
        public bool HasWeeklyContent { get; init; }
        public bool ShowActionButtons { get; init; }
    }

    public static SyncMetrics Compute(
        int offeredCourseId,
        IReadOnlyList<int> leafTopicIds,
        IReadOnlyList<int> comparableOfferedCourseIds,
        IReadOnlyDictionary<string, bool> progressMap)
    {
        var totalTopics = leafTopicIds.Count;
        var hasWeeklyContent = totalTopics > 0;

        var coveredTopics = leafTopicIds.Count(topicId =>
            progressMap.TryGetValue($"{offeredCourseId}|{topicId}", out var completed) && completed);

        var gapTopics = comparableOfferedCourseIds.Count <= 1
            ? 0
            : CountGapTopics(offeredCourseId, leafTopicIds, comparableOfferedCourseIds, progressMap);

        var alignedTopics = Math.Max(totalTopics - gapTopics, 0);
        var progressPercentage = totalTopics > 0
            ? Math.Round((decimal)coveredTopics / totalTopics * 100, 0)
            : 0m;

        var syncScore = totalTopics > 0
            ? Math.Round((decimal)alignedTopics / totalTopics * 100, 0)
            : 0m;

        var deviationLabel = BuildDeviationLabel(hasWeeklyContent, gapTopics);

        return new SyncMetrics
        {
            TotalTopics = totalTopics,
            CoveredTopics = coveredTopics,
            GapTopics = gapTopics,
            AlignedTopics = alignedTopics,
            ProgressPercentage = progressPercentage,
            SyncScore = syncScore,
            DeviationLabel = deviationLabel,
            HasWeeklyContent = hasWeeklyContent,
            ShowActionButtons = gapTopics > 0 || syncScore < 100
        };
    }

    public static int CountGapTopics(
        int offeredCourseId,
        IReadOnlyList<int> leafTopicIds,
        IReadOnlyList<int> comparableOfferedCourseIds,
        IReadOnlyDictionary<string, bool> progressMap)
    {
        if (comparableOfferedCourseIds.Count <= 1)
        {
            return 0;
        }

        var gapCount = 0;

        foreach (var topicId in leafTopicIds)
        {
            var currentCompleted = progressMap.TryGetValue($"{offeredCourseId}|{topicId}", out var value) && value;

            if (currentCompleted)
            {
                continue;
            }

            var coveredByPeer = comparableOfferedCourseIds
                .Where(id => id != offeredCourseId)
                .Any(id => progressMap.TryGetValue($"{id}|{topicId}", out var other) && other);

            if (coveredByPeer)
            {
                gapCount++;
            }
        }

        return gapCount;
    }

    public static string BuildDeviationLabel(bool hasWeeklyContent, int gapTopics)
    {
        if (!hasWeeklyContent)
        {
            return "No topics";
        }

        if (gapTopics == 0)
        {
            return "In sync";
        }

        return gapTopics == 1 ? "1 gap" : $"{gapTopics} gaps";
    }
}
