using System.Text;

namespace Kokoabim.GitTasks;

public class GitModulesFileEntry
{
    public GitSubmoduleIgnoreOption Ignore { get; set; }
    public string Name { get; set; }
    public string Path { get; set; }
    public string Url { get; set; }

    public GitModulesFileEntry(string name, string path, string url, GitSubmoduleIgnoreOption ignore)
    {
        Name = name;
        Path = path;
        Url = url;
        Ignore = ignore;
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[submodule \"{Name}\"]");
        sb.AppendLine($"\tpath = {Path}");
        sb.AppendLine($"\turl = {Url}");
        if (Ignore != GitSubmoduleIgnoreOption.None) sb.AppendLine($"\tignore = {Ignore.ToString().ToLower()}");
        return sb.ToString().TrimEnd();
    }
}