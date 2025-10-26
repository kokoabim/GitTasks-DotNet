using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Kokoabim.GitTasks;

public class GitModulesFile
{
    public string Path { get; set; }
    public IEnumerable<GitModulesFileEntry> Submodules { get; set; } = [];

    private static readonly FileSystem _fileSystem = new();
    private static readonly Regex _ignoreMatch = new Regex(@"^\s*ignore\s*=\s*(?<ignore>.+)\s*$", RegexOptions.Multiline);
    private static readonly Regex _pathMatch = new Regex(@"^\s*path\s*=\s*(?<path>.+)\s*$", RegexOptions.Multiline);
    private static readonly Regex _submoduleMatches = new Regex(@"^\s*\[\s*submodule\s+""(?<name>[^""]+)""\s*\]\s*^(?<body>[^\[]+)", RegexOptions.Multiline | RegexOptions.Singleline);
    private static readonly Regex _urlMatch = new Regex(@"^\s*url\s*=\s*(?<url>.+)\s*$", RegexOptions.Multiline);

    public GitModulesFile(string path, IEnumerable<GitModulesFileEntry> submodules)
    {
        Path = path;
        Submodules = submodules;
    }

    public override string ToString() => string.Join(Environment.NewLine, Submodules.Select(sm => sm.ToString()));

    public static bool TryParse(string directory, string fileName, [NotNullWhen(true)] out GitModulesFile? gitModulesFile, [NotNullWhen(false)] out string? errorMessage)
    {
        gitModulesFile = null;
        errorMessage = null;

        var filePath = System.IO.Path.Combine(directory, fileName);

        var fileContents = _fileSystem.ReadFile(filePath);
        if (fileContents is null)
        {
            errorMessage = "Failed to read .gitmodules file";
            return false;
        }

        var matches = _submoduleMatches.Matches(fileContents);
        if (matches.Count == 0)
        {
            errorMessage = "No submodules found in .gitmodules file";
            return false;
        }

        var submodules = new List<GitModulesFileEntry>();
        foreach (Match match in matches)
        {
            var name = match.Groups["name"].Value.Trim();
            var body = match.Groups["body"].Value;

            var path = _pathMatch.Match(body) is { Success: true } pm ? pm.Groups["path"].Value.Trim() : null;
            var url = _urlMatch.Match(body) is { Success: true } um ? um.Groups["url"].Value.Trim() : null;

            if (path is null || url is null)
            {
                errorMessage = $"Invalid submodule entry for '{name}' in .gitmodules file";
                return false;
            }

            var ignore = _ignoreMatch.Match(body) is { Success: true } im ? im.Groups["ignore"].Value.Trim() : null;
            var ignoreOption = ignore is not null && Enum.TryParse<GitSubmoduleIgnoreOption>(ignore, true, out var parsedIgnore)
                ? parsedIgnore : GitSubmoduleIgnoreOption.None;

            submodules.Add(new GitModulesFileEntry(name, path, url, ignoreOption));
        }

        gitModulesFile = new GitModulesFile(filePath, submodules);
        return true;
    }

    public bool TryWrite(bool overwrite, out string? errorMessage)
    {
        errorMessage = null;

        try
        {
            if (!overwrite && _fileSystem.FileExists(Path))
            {
                errorMessage = $".gitmodules file already exists: {Path}";
                return false;
            }

            File.WriteAllText(Path, ToString());
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"Failed to write .gitmodules file '{Path}': {ex.Message}";
            return false;
        }
    }
}