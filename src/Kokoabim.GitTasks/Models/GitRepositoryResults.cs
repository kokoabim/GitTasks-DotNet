namespace Kokoabim.GitTasks;

public class GitRepositoryResults
{
    #region properties
    public ExecuteResult<GitBranch[]>? Branches { get; set; }
    public ExecuteResult? Checkout { get; set; }
    public ExecuteResult? Clean { get; set; }
    public ExecuteResult? Commit { get; set; }
    public ExecuteResult<GitCommitPosition>? CommitPosition { get; set; }
    public ExecuteResult? Fetch { get; set; }
    public ExecuteResult? FullStatus { get; set; }
    public ExecuteResult? Pull { get; set; }
    public ExecuteResult? Push { get; set; }
    public ExecuteResult? Reset { get; set; }
    public ExecuteResult? SetHead { get; set; }
    public ExecuteResult? Status { get; set; }
    #endregion 
}