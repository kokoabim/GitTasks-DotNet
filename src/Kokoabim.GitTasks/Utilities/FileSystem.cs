using System.Text.Json;

namespace Kokoabim.GitTasks;

public class FileSystem
{
    public bool FileExists(string path) => File.Exists(path);

    public bool FileExists(string path, string fileName) => File.Exists(Path.Combine(path, fileName));

    public string GetFullPath(string path)
    {
        path = Path.GetFullPath(path);
        if (!Directory.Exists(path)) throw new DirectoryNotFoundException($"Directory not found: {path}");
        return path;
    }

    public string[] GetGitDirectories(string path)
    {
        var directories = new List<string>();
        GetGitDirectories(directories, GetFullPath(path), depth: 1);
        return [.. directories];
    }

    public string GetUserHomePath() => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public bool IsGitDirectory(string path)
    {
        path = GetFullPath(path);
        return Directory.Exists(Path.Combine(path, ".git")) ||
               (File.Exists(Path.Combine(path, ".git")) && File.ReadAllText(Path.Combine(path, ".git")).StartsWith("gitdir:"));
    }

    public string? ReadFile(string path, string? fileName = null)
    {
        if (fileName != null) path = Path.Combine(path, fileName);

        try { return File.ReadAllText(path); }
        catch { return null; }
    }

    public T? ReadFile<T>(string path, string? fileName = null)
    {
        if (fileName != null) path = Path.Combine(path, fileName);

        try { return JsonSerializer.Deserialize<T>(File.ReadAllText(path)); }
        catch { return default; }
    }

    public async Task<string?> ReadFileAsync(string path, string? fileName = null)
    {
        if (fileName != null) path = Path.Combine(path, fileName);

        using var reader = new StreamReader(path);
        try { return await reader.ReadToEndAsync(); }
        catch { return null; }
    }

    private void GetGitDirectories(List<string> directories, string path, int depth)
    {
        if (IsGitDirectory(path)) directories.Add(path);

        if (depth > 0) foreach (var d in Directory.GetDirectories(path)) GetGitDirectories(directories, d, depth - 1);
    }
}