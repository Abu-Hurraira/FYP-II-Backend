namespace FolderManagementSystemAPI.Services;

public static class FolderStoragePath
{
    public static IEnumerable<string> GetCandidates(IWebHostEnvironment environment, string relativePath)
    {
        var normalized = (relativePath ?? string.Empty)
            .Replace('\\', '/')
            .TrimStart('/');
        var cleanPath = normalized.Replace('/', Path.DirectorySeparatorChar);
        var webRoot = string.IsNullOrWhiteSpace(environment.WebRootPath)
            ? Path.Combine(environment.ContentRootPath, "wwwroot")
            : environment.WebRootPath;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            seen.Add(Path.GetFullPath(path));
        }

        if (normalized.StartsWith("course-folders/", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "course-folders", StringComparison.OrdinalIgnoreCase))
        {
            Add(Path.Combine(webRoot, cleanPath));
            Add(Path.Combine(environment.ContentRootPath, cleanPath));
        }
        else
        {
            Add(Path.Combine(environment.ContentRootPath, cleanPath));
            Add(Path.Combine(webRoot, cleanPath));
        }

        return seen;
    }

    public static string Resolve(IWebHostEnvironment environment, string relativePath)
    {
        var candidates = GetCandidates(environment, relativePath).ToList();
        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate) || Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return candidates[0];
    }

    public static string? ResolveExistingFile(IWebHostEnvironment environment, string relativePath)
    {
        foreach (var candidate in GetCandidates(environment, relativePath))
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        var fileName = Path.GetFileName((relativePath ?? string.Empty).Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var directoryRelative = Path.GetDirectoryName((relativePath ?? string.Empty).Replace('\\', '/'))
            ?.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(directoryRelative))
        {
            return null;
        }

        foreach (var directory in GetCandidates(environment, directoryRelative))
        {
            if (!Directory.Exists(directory))
            {
                continue;
            }

            var match = Directory.GetFiles(directory, fileName).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(match))
            {
                return match;
            }
        }

        return null;
    }

    public static string WriteAllBytes(IWebHostEnvironment environment, string relativePath, byte[] bytes)
    {
        string? first = null;
        foreach (var candidate in GetCandidates(environment, relativePath))
        {
            var directory = Path.GetDirectoryName(candidate);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(candidate, bytes);
            first ??= candidate;
        }

        return first ?? Resolve(environment, relativePath);
    }

    public static void DeleteExistingFiles(IWebHostEnvironment environment, string relativePath)
    {
        foreach (var candidate in GetCandidates(environment, relativePath))
        {
            if (File.Exists(candidate))
            {
                File.Delete(candidate);
            }
        }
    }
}
