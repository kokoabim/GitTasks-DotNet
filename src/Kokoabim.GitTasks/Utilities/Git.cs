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

    public ExecutorResult<GitCommitPosition> GetCommitPosition(string path, string? branch = null, CancellationToken cancellationToken = default)
    {
        var execResult = _executor.Execute("git", $"rev-list --left-right --count \"origin/{branch}...{branch}\"", workingDirectory: path, cancellationToken: cancellationToken);
        if (!execResult.Success) return execResult.WithObject<GitCommitPosition>(null);

        var parts = execResult.Output.Trim().Split(['\t', ' '], 2, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 2 && int.TryParse(parts[0], out var behindBy) && int.TryParse(parts[1], out var aheadBy))
        {
            return execResult.WithObject(new GitCommitPosition
            {
                BehindBy = behindBy,
                AheadBy = aheadBy
            });
        }

        return execResult.WithObject<GitCommitPosition>(null);
    }

    public ExecutorResult GetCurrentBranch(string path, CancellationToken cancellationToken = default) =>
        _executor.Execute("git", "rev-parse --abbrev-ref HEAD", workingDirectory: path, cancellationToken: cancellationToken);

    public async Task<ExecutorResult> GetCurrentBranchAsync(string path, CancellationToken cancellationToken = default) =>
        await _executor.ExecuteAsync("git", "rev-parse --abbrev-ref HEAD", workingDirectory: path, cancellationToken: cancellationToken);

    public ExecutorResult<string> GetDefaultBranch(string path, CancellationToken cancellationToken = default)
    {
        var result = _executor.Execute("git", "symbolic-ref refs/remotes/origin/HEAD --short", workingDirectory: path, cancellationToken: cancellationToken);
        var resultWithObject = result.WithObject(result.Output?.Trim().Split('/', 2)[1]);
        return resultWithObject;
    }

    public async Task<ExecutorResult<string>> GetDefaultBranchAsync(string path, CancellationToken cancellationToken = default)
    {
        var result = await _executor.ExecuteAsync("git", "symbolic-ref refs/remotes/origin/HEAD --short", workingDirectory: path, cancellationToken: cancellationToken);
        var resultWithObject = result.WithObject(result.Output?.Trim().Split('/', 2)[1]);
        return resultWithObject;
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

    public GitRepository[] GetRepositories(string path, CancellationToken cancellationToken = default)
    {
        var results = new List<GitRepository?>();
        results.AddRange(_fileSystem.GetGitDirectories(path).Select(d => GetRepository(d, isSubmodule: false, cancellationToken)));
        results.AddRange(GetSubmoduleDirectories(path, cancellationToken).Select(d => GetRepository(d, isSubmodule: true, cancellationToken)));

        return [.. results.Where(r => r is not null).Cast<GitRepository>().Distinct().OrderBy(r => r.Path).ForEach(r => r.SetRelativePath(path))];
    }

    public async Task<GitRepository[]> GetRepositoriesAsync(string path, CancellationToken cancellationToken = default)
    {
        var tasks = new List<Task<GitRepository?>>();
        tasks.AddRange(_fileSystem.GetGitDirectories(path).Select(d => GetRepositoryAsync(d, isSubmodule: false, cancellationToken)));
        tasks.AddRange((await GetSubmoduleDirectoriesAsync(path, cancellationToken)).Select(d => GetRepositoryAsync(d, isSubmodule: true, cancellationToken)));
        var results = await Task.WhenAll(tasks);

        return [.. results.Where(r => r is not null).Cast<GitRepository>().Distinct().OrderBy(r => r.Path).ForEach(r => r.SetRelativePath(path))];
    }

    public ExecutorResult GetStatus(string path, bool porcelain = false, CancellationToken cancellationToken = default)
    {
        var args = porcelain ? "status --porcelain" : "status";
        return _executor.Execute("git", args, workingDirectory: path, cancellationToken: cancellationToken);
    }

    public async Task<ExecutorResult> GetStatusAsync(string path, bool porcelain = false, CancellationToken cancellationToken = default)
    {
        var args = porcelain ? "status --porcelain" : "status";
        return await _executor.ExecuteAsync("git", args, workingDirectory: path, cancellationToken: cancellationToken);
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

    public async Task<ExecutorResult> PullAsync(string path, CancellationToken cancellationToken = default) =>
        await _executor.ExecuteAsync("git", "pull", workingDirectory: path, cancellationToken: cancellationToken);

    private GitRepository? GetRepository(string path, bool isSubmodule, CancellationToken cancellationToken = default)
    {
        var defaultBranch = GetDefaultBranch(path, cancellationToken);
        if (!defaultBranch.Success) return null;

        var currentBranch = GetCurrentBranch(path, cancellationToken);
        if (!currentBranch.Success) return null;

        return new GitRepository
        {
            CurrentBranch = currentBranch.Output.Trim(),
            DefaultBranch = defaultBranch.Object,
            IsSubmodule = isSubmodule,
            Path = path
        };
    }

    private async Task<GitRepository?> GetRepositoryAsync(string path, bool isSubmodule, CancellationToken cancellationToken = default)
    {
        var defaultBranch = await GetDefaultBranchAsync(path, cancellationToken);
        if (!defaultBranch.Success) return null;

        var currentBranch = await GetCurrentBranchAsync(path, cancellationToken);
        if (!currentBranch.Success) return null;

        return new GitRepository
        {
            CurrentBranch = currentBranch.Output.Trim(),
            DefaultBranch = defaultBranch.Object,
            IsSubmodule = isSubmodule,
            Path = path
        };
    }

    #endregion 
}

public class GitCommitPosition
{
    public int AheadBy { get; set; }
    public int BehindBy { get; set; }
}