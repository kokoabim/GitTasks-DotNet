namespace Kokoabim.GitTasks;

public class GitRepository
{
    public ConsolePosition ConsolePosition { get; } = new();
    public string CurrentBranch { get; init; } = null!;
    public string DefaultBranch { get; init; } = null!;
    public bool IsSubmodule { get; init; }
    public string Path { get; init; } = null!;
    public string RelativePath { get; private set; } = null!;
    public GitRepositoryResults Results { get; } = new();

    public void SetRelativePath(string basePath)
    {
        RelativePath = Path != basePath ? Path[(basePath.Length + 1)..] : ".";
    }
}

public class GitRepositoryResults
{
    public ExecutorResult? Checkout { get; set; }
    public ExecutorResult<GitCommitPosition>? CommitPosition { get; set; }
    public ExecutorResult? Fetch { get; set; }
    public ExecutorResult? FullStatus { get; set; }
    public ExecutorResult? Pull { get; set; }
    public ExecutorResult? Status { get; set; }
}