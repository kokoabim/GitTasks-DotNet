using System.Diagnostics.CodeAnalysis;

namespace Kokoabim.GitTasks;

public class GitRepository
{
    #region properties
    public ConsolePosition ConsolePosition { get; } = new();
    public string? CurrentBranch { get; set; }
    public string? DefaultBranch { get; set; }
    public string? Error { get; set; }
    public bool IsSubmodule { get; set; }
    public string Name { get; }
    public string NameAndRelativePath { get; private set; } = null!;
    public string Path { get; }
    public string RelativePath { get; private set; } = null!;
    public GitRepositoryResults Results { get; } = new();

    [MemberNotNullWhen(true, nameof(CurrentBranch), nameof(DefaultBranch), nameof(Name), nameof(Path))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool Success =>
            Error is null
            && CurrentBranch is not null
            && DefaultBranch is not null
            && !string.IsNullOrWhiteSpace(Name)
            && !string.IsNullOrWhiteSpace(Path);

    #endregion 

    public GitRepository(string name, string path)
    {
        Name = name;
        Path = path;
    }

    public void SetRelativePath(string basePath)
    {
        RelativePath = Path != basePath ? Path[(basePath.Length + 1)..] : ".";

        NameAndRelativePath = string.Equals(Name, RelativePath, StringComparison.OrdinalIgnoreCase) ? Name : $"{Name} ({RelativePath})";
    }
}