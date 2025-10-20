using System.Diagnostics.CodeAnalysis;

namespace Kokoabim.GitTasks;

public class GitRepository
{
    public ConsolePosition ConsolePosition { get; } = new();
    public string? CurrentBranch { get; set; }
    public string? DefaultBranch { get; set; }
    public string? Error { get; set; }
    public bool IsSubmodule { get; set; }
    public string Path { get; set; } = null!;
    public string RelativePath { get; private set; } = null!;
    public GitRepositoryResults Results { get; } = new();

    [MemberNotNullWhen(true, nameof(CurrentBranch), nameof(DefaultBranch))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool Success => Error is null;

    public void SetRelativePath(string basePath)
    {
        RelativePath = Path != basePath ? Path[(basePath.Length + 1)..] : ".";
    }
}

public class GitRepositoryResults
{
    public ExecutorResult<GitBranch[]>? Branches { get; set; }
    public ExecutorResult? Checkout { get; set; }
    public ExecutorResult<GitCommitPosition>? CommitPosition { get; set; }
    public ExecutorResult? Fetch { get; set; }
    public ExecutorResult? FullStatus { get; set; }
    public ExecutorResult? Pull { get; set; }
    public ExecutorResult? SetHead { get; set; }
    public ExecutorResult? Status { get; set; }
}