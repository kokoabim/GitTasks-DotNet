namespace Kokoabim.GitTasks;

public class Git
{
    public bool IsInstalled => GetVersion() is not null;

    private readonly Executor _executor = new();
    private readonly FileSystem _fileSystem = new();

    #region methods

    public ExecutorResult Checkout(string path, string branch, CancellationToken cancellationToken = default) =>
        _executor.Execute("git", $"checkout {branch}", workingDirectory: path, cancellationToken: cancellationToken);

    public async Task<ExecutorResult> CheckoutAsync(string path, string branch, CancellationToken cancellationToken = default)
    {
        var currentBranch = await GetCurrentBranchAsync(path, cancellationToken);
        return currentBranch.Output == branch
            ? currentBranch
            : await _executor.ExecuteAsync("git", $"checkout {branch}", workingDirectory: path, cancellationToken: cancellationToken);
    }

    public async Task<ExecutorResult> FetchAsync(string path, string? branch = null, CancellationToken cancellationToken = default)
    {
        var args = branch is null ? "fetch" : $"fetch origin {branch}";
        return await _executor.ExecuteAsync("git", args, workingDirectory: path, cancellationToken: cancellationToken);
    }

    public ExecutorResult<GitBranch[]> GetBranches(string path, CancellationToken cancellationToken = default)
    {
        var result = _executor.Execute("git", "branch --all", workingDirectory: path, cancellationToken: cancellationToken);
        result.Reference = path;
        if (!result.Success) return result.WithNull<GitBranch[]>();

        var branches = result.Output.Trim().Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(GitBranch.Parse).ToArray();
        return result.WithObject(branches);
    }

    public async Task<ExecutorResult<GitBranch[]>> GetBranchesAsync(string path, CancellationToken cancellationToken = default)
    {
        var result = await _executor.ExecuteAsync("git", "branch --all", workingDirectory: path, cancellationToken: cancellationToken);
        result.Reference = path;
        if (!result.Success) return result.WithNull<GitBranch[]>();

        var branches = result.Output.Trim().Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(GitBranch.Parse).ToArray();
        return result.WithObject(branches);
    }

    public ExecutorResult<GitCommitPosition> GetCommitPosition(string path, string? branch = null, CancellationToken cancellationToken = default)
    {
        var execResult = _executor.Execute("git", $"rev-list --left-right --count \"origin/{branch}...{branch}\"", workingDirectory: path, cancellationToken: cancellationToken);
        execResult.Reference = path;
        if (!execResult.Success) return execResult.WithNull<GitCommitPosition>();

        var parts = execResult.Output.Trim().Split(['\t', ' '], 2, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 2 && int.TryParse(parts[0], out var behindBy) && int.TryParse(parts[1], out var aheadBy))
        {
            return execResult.WithObject(new GitCommitPosition
            {
                BehindBy = behindBy,
                AheadBy = aheadBy
            });
        }

        return execResult.WithNull<GitCommitPosition>();
    }

    public ExecutorResult<string> GetCurrentBranch(string path, CancellationToken cancellationToken = default)
    {
        var result = _executor.Execute("git", "rev-parse --abbrev-ref HEAD", workingDirectory: path, cancellationToken: cancellationToken);
        return result.WithObject(result.Output?.Trim(), path);
    }

    public async Task<ExecutorResult<string>> GetCurrentBranchAsync(string path, CancellationToken cancellationToken = default)
    {
        var result = await _executor.ExecuteAsync("git", "rev-parse --abbrev-ref HEAD", workingDirectory: path, cancellationToken: cancellationToken);
        return result.WithObject(result.Output?.Trim(), path);
    }

    public ExecutorResult<string> GetDefaultBranch(string path, CancellationToken cancellationToken = default)
    {
        var result = _executor.Execute("git", "symbolic-ref refs/remotes/origin/HEAD --short", workingDirectory: path, cancellationToken: cancellationToken);
        return result.Success ? result.WithObject(result.Output?.Trim().Split('/', 2)[1], path) : result.WithNull<string>(path);
    }

    public async Task<ExecutorResult<string>> GetDefaultBranchAsync(string path, CancellationToken cancellationToken = default)
    {
        var result = await _executor.ExecuteAsync("git", "symbolic-ref refs/remotes/origin/HEAD --short", workingDirectory: path, cancellationToken: cancellationToken);
        return result.Success ? result.WithObject(result.Output?.Trim().Split('/', 2)[1], path) : result.WithNull<string>(path);
    }

    public string[] GetDirectories(string path, CancellationToken cancellationToken = default)
    {
        var directories = new List<string>();
        directories.AddRange(_fileSystem.GetGitDirectories(path));
        directories.AddRange(GetSubmoduleDirectories(path, cancellationToken));

        return [.. directories.Distinct().Order()];
    }

    public async Task<string[]> GetDirectoriesAsync(string path, CancellationToken cancellationToken = default)
    {
        var directories = new List<string>();
        directories.AddRange(_fileSystem.GetGitDirectories(path));
        directories.AddRange(await GetSubmoduleDirectoriesAsync(path, cancellationToken));

        return [.. directories.Distinct().Order()];
    }

    public ExecutorResult<GitRepository>[] GetRepositories(string path, CancellationToken cancellationToken = default)
    {
        var results = new List<ExecutorResult<GitRepository>>();
        results.AddRange(_fileSystem.GetGitDirectories(path).Select(d => GetRepository(d, isSubmodule: false, cancellationToken)));
        results.AddRange(GetSubmoduleDirectories(path, cancellationToken).Select(d => GetRepository(d, isSubmodule: true, cancellationToken)));

        return [.. results.OrderBy(r => r.Object!.Path).ForEach(r => r.Object!.SetRelativePath(path))];
    }

    public async Task<ExecutorResult<GitRepository>[]> GetRepositoriesAsync(string path, CancellationToken cancellationToken = default)
    {
        var tasks = new List<Task<ExecutorResult<GitRepository>>>();
        tasks.AddRange(_fileSystem.GetGitDirectories(path).Select(d => GetRepositoryAsync(d, isSubmodule: false, cancellationToken)));
        tasks.AddRange((await GetSubmoduleDirectoriesAsync(path, cancellationToken)).Select(d => GetRepositoryAsync(d, isSubmodule: true, cancellationToken)));
        var results = await Task.WhenAll(tasks);

        return [.. results.OrderBy(r => r.Object!.Path).ForEach(r => r.Object!.SetRelativePath(path))];
    }

    public ExecutorResult GetStatus(string path, bool porcelain = false, CancellationToken cancellationToken = default)
    {
        var args = porcelain ? "status --porcelain" : "status";
        var result = _executor.Execute("git", args, workingDirectory: path, cancellationToken: cancellationToken);
        return result.WithReference(path);
    }

    public async Task<ExecutorResult> GetStatusAsync(string path, bool porcelain = false, CancellationToken cancellationToken = default)
    {
        var args = porcelain ? "status --porcelain" : "status";
        var result = await _executor.ExecuteAsync("git", args, workingDirectory: path, cancellationToken: cancellationToken);
        return result.WithReference(path);
    }

    public string[] GetSubmoduleDirectories(string path, CancellationToken cancellationToken = default)
    {
        var result = _executor.Execute("git", "config --file .gitmodules --get-regexp path", workingDirectory: path, cancellationToken: cancellationToken);
        if (!result.Success || result.Output.Length == 0) return [];

        return [.. result.Output.Trim().Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(l => Path.Combine(path, l.Split(' ', 2)[1]))];
    }

    public async Task<string[]> GetSubmoduleDirectoriesAsync(string path, CancellationToken cancellationToken = default)
    {
        var result = await _executor.ExecuteAsync("git", "config --file .gitmodules --get-regexp path", workingDirectory: path, cancellationToken: cancellationToken);
        if (!result.Success || result.Output.Length == 0) return [];

        return [.. result.Output.Trim().Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(l => Path.Combine(path, l.Split(' ', 2)[1]))];
    }

    public string? GetVersion(CancellationToken cancellationToken = default)
    {
        var result = _executor.Execute("git", "--version", cancellationToken: cancellationToken);
        return result.Success ? result.Output.Trim() : null;
    }

    public async Task<string?> GetVersionAsync(CancellationToken cancellationToken = default)
    {
        var result = await _executor.ExecuteAsync("git", "--version", cancellationToken: cancellationToken);
        return result.Success ? result.Output.Trim() : null;
    }

    public async Task<ExecutorResult> PullAsync(string path, CancellationToken cancellationToken = default)
    {
        var result = await _executor.ExecuteAsync("git", "pull", workingDirectory: path, cancellationToken: cancellationToken);
        return result.WithReference(path);
    }

    public ExecutorResult Reset(string path, string commit = "HEAD", GitResetMode resetType = GitResetMode.Mixed, int back = 0, CancellationToken cancellationToken = default)
    {
        if (back > 0) commit = $"{commit}~{back}";

        var resetArg = resetType switch
        {
            GitResetMode.Hard => "--hard",
            GitResetMode.Soft => "--soft",
            _ => "--mixed"
        };

        var result = _executor.Execute("git", $"reset {resetArg} {commit}", workingDirectory: path, cancellationToken: cancellationToken);
        return result.WithReference(path);
    }

    public async Task<ExecutorResult> ResetAsync(string path, string commit = "HEAD", GitResetMode mode = GitResetMode.Mixed, int back = 0, CancellationToken cancellationToken = default)
    {
        if (back > 0) commit = $"{commit}~{back}";

        var resetArg = mode switch
        {
            GitResetMode.Soft => "--soft",
            GitResetMode.Hard => "--hard",
            _ => "--mixed"
        };

        var result = await _executor.ExecuteAsync("git", $"reset {resetArg} {commit}", workingDirectory: path, cancellationToken: cancellationToken);
        return result.WithReference(path);
    }

    public ExecutorResult SetHead(string path, string remote, string? branch = null, bool automatically = false, CancellationToken cancellationToken = default)
    {
        if (branch is null && !automatically) return new ExecutorResult
        {
            ExitCode = 1,
            Output = "Branch must be specified if not setting automatically",
            Reference = path,
        };

        var arg = branch is not null ? branch : "--auto"; // 'automatically' is true if branch is null
        var result = _executor.Execute("git", $"remote set-head {remote} {arg}", workingDirectory: path, cancellationToken: cancellationToken);
        return result.WithReference(path);
    }

    public async Task<ExecutorResult> SetHeadAsync(string path, string remote, string? branch = null, bool automatically = false, CancellationToken cancellationToken = default)
    {
        if (branch is null && !automatically) return new ExecutorResult
        {
            ExitCode = 1,
            Output = "Branch must be specified if not setting automatically",
            Reference = path,
        };

        var arg = branch is not null ? branch : "--auto"; // 'automatically' is true if branch is null
        var result = await _executor.ExecuteAsync("git", $"remote set-head {remote} {arg}", workingDirectory: path, cancellationToken: cancellationToken);
        return result.WithReference(path);
    }

    private ExecutorResult<GitRepository> GetRepository(string path, bool isSubmodule, CancellationToken cancellationToken = default)
    {
        var defaultBranchExecResult = GetDefaultBranch(path, cancellationToken);
        var currentBranchExecResult = GetCurrentBranch(path, cancellationToken);

        return ExecutorResult.CreateWithObject(new GitRepository()
        {
            Error = new string?[] { currentBranchExecResult.Success ? null : currentBranchExecResult.Output, defaultBranchExecResult.Success ? null : defaultBranchExecResult.Output }.CombineNonNulls("; "),
            CurrentBranch = currentBranchExecResult.Object,
            DefaultBranch = defaultBranchExecResult.Object,
            IsSubmodule = isSubmodule,
            Path = path
        }, path);
    }

    private async Task<ExecutorResult<GitRepository>> GetRepositoryAsync(string path, bool isSubmodule, CancellationToken cancellationToken = default)
    {
        var defaultBranchExecResult = await GetDefaultBranchAsync(path, cancellationToken);
        var currentBranchExecResult = await GetCurrentBranchAsync(path, cancellationToken);

        return ExecutorResult.CreateWithObject(new GitRepository()
        {
            Error = new string?[] { currentBranchExecResult.Success ? null : currentBranchExecResult.Output, defaultBranchExecResult.Success ? null : defaultBranchExecResult.Output }.CombineNonNulls("; "),
            CurrentBranch = currentBranchExecResult.Object,
            DefaultBranch = defaultBranchExecResult.Object,
            IsSubmodule = isSubmodule,
            Path = path
        }, path);
    }

    #endregion 
}