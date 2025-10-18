namespace Kokoabim.GitTasks;

public interface IFileSystem
{
    string GetFullPath(string path);
    string[] GetGitDirectories(string path);
}

public class FileSystem : IFileSystem
{
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

    private static void GetGitDirectories(List<string> directories, string path, int depth)
    {
        if (Directory.Exists(Path.Combine(path, ".git"))) directories.Add(path);
        else if (File.Exists(Path.Combine(path, ".git")) && File.ReadAllText(Path.Combine(path, ".git")).StartsWith("gitdir:")) directories.Add(path);

        if (depth > 0) foreach (var d in Directory.GetDirectories(path)) GetGitDirectories(directories, d, depth - 1);
    }
}