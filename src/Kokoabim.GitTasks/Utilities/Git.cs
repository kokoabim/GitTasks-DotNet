using System.Text.RegularExpressions;

namespace Kokoabim.GitTasks;

#pragma warning disable CA1822 // Mark members as static

public class Git
{
    private const string _gitLogFormat = "AuthorDate=%ad%nAuthorName=%an%nAuthorEmail=%ae%nCommitDate=%cd%nCommitterName=%cn%nDecorations=%d%nHash=%H%nMessageBody=%B%nMessageSubject=%s%nParentHashes=%P%nNumStats=";

    public bool IsInstalled => GetVersion() is not null;

    private readonly Executor _executor = new();
    private readonly FileSystem _fileSystem = new();

    #region methods

    public ExecuteResult Add(string path, string[] pathspecs, CancellationToken cancellationToken = default)
    {
        var args = $"add {string.Join(' ', pathspecs.Select(sm => $"\"{sm}\""))}";
        return _executor.Execute("git", args, workingDirectory: path, cancellationToken: cancellationToken).WithReference(path);
    }

    public ExecuteResult Checkout(string path, string branch, bool createBranch, CancellationToken cancellationToken = default) =>
        _executor.Execute("git", CreateCheckoutArgs(branch, createBranch), workingDirectory: path, cancellationToken: cancellationToken).WithReference(path);

    public async Task<ExecuteResult> CheckoutAsync(string path, string branch, bool createBranch, CancellationToken cancellationToken = default) =>
        (await _executor.ExecuteAsync("git", CreateCheckoutArgs(branch, createBranch), workingDirectory: path, cancellationToken: cancellationToken)).WithReference(path);

    public ExecuteResult Clean(string path, bool recursively, bool force, bool ignoreIgnoreRules, bool cleanOnlyIgnored, bool dryRun, CancellationToken cancellationToken = default)
    {
        (bool success, string? output) = CreateCleanArgs(recursively, force, ignoreIgnoreRules, cleanOnlyIgnored, dryRun, out string? args);
        if (!success) return new ExecuteResult
        {
            ExitCode = 1,
            Output = output,
            Reference = path,
        };

        return _executor.Execute("git", args, workingDirectory: path, cancellationToken: cancellationToken).WithReference(path);
    }

    public async Task<ExecuteResult> CleanAsync(string path, bool recursively, bool force, bool ignoreIgnoreRules, bool cleanOnlyIgnored, bool dryRun, CancellationToken cancellationToken = default)
    {
        (bool success, string? output) = CreateCleanArgs(recursively, force, ignoreIgnoreRules, cleanOnlyIgnored, dryRun, out string? args);
        if (!success) return new ExecuteResult
        {
            ExitCode = 1,
            Output = output,
            Reference = path,
        };

        return (await _executor.ExecuteAsync("git", args, workingDirectory: path, cancellationToken: cancellationToken)).WithReference(path);
    }

    public ExecuteResult Commit(string path, string message, CancellationToken cancellationToken = default)
    {
        var args = $"commit -m \"{message.Replace("\"", "\\\"")}\"";
        return _executor.Execute("git", args, workingDirectory: path, cancellationToken: cancellationToken).WithReference(path);
    }

    public async Task<ExecuteResult> FetchAsync(string path, string? branch = null, CancellationToken cancellationToken = default)
    {
        var args = branch is null ? "fetch" : $"fetch origin {branch}";
        return (await _executor.ExecuteAsync("git", args, workingDirectory: path, cancellationToken: cancellationToken)).WithReference(path);
    }

    public ExecuteResult<GitBranch[]> GetBranches(string path, CancellationToken cancellationToken = default)
    {
        var result = _executor.Execute("git", "branch --all", workingDirectory: path, cancellationToken: cancellationToken);
        result.Reference = path;
        if (!result.Success) return result.WithNull<GitBranch[]>();

        var branches = result.Output.Trim().Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(GitBranch.Parse).ToArray();
        return result.WithValue(branches);
    }

    public async Task<ExecuteResult<GitBranch[]>> GetBranchesAsync(string path, CancellationToken cancellationToken = default)
    {
        var result = (await _executor.ExecuteAsync("git", "branch --all", workingDirectory: path, cancellationToken: cancellationToken)).WithReference(path);
        if (!result.Success) return result.WithNull<GitBranch[]>();

        var branches = result.Output.Trim().Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(GitBranch.Parse).ToArray();
        return result.WithValue(branches);
    }

    public ExecuteResult<GitCommitPosition> GetCommitPosition(string path, string? branch = null, CancellationToken cancellationToken = default)
    {
        var execResult = _executor.Execute("git", $"rev-list --left-right --count \"origin/{branch}...{branch}\"", workingDirectory: path, cancellationToken: cancellationToken).WithReference(path);
        if (!execResult.Success) return execResult.WithNull<GitCommitPosition>();

        var parts = execResult.Output.Trim().Split(['\t', ' '], 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2 && int.TryParse(parts[0], out var behindBy) && int.TryParse(parts[1], out var aheadBy))
        {
            return execResult.WithValue(new GitCommitPosition
            {
                BehindBy = behindBy,
                AheadBy = aheadBy
            });
        }

        return execResult.WithNull<GitCommitPosition>();
    }

    public ExecuteResult<string> GetCurrentBranch(string path, CancellationToken cancellationToken = default)
    {
        var result = _executor.Execute("git", "rev-parse --abbrev-ref HEAD", workingDirectory: path, cancellationToken: cancellationToken);
        return result.WithValue(result.Output?.Trim(), path);
    }

    public async Task<ExecuteResult<string>> GetCurrentBranchAsync(string path, CancellationToken cancellationToken = default)
    {
        var result = await _executor.ExecuteAsync("git", "rev-parse --abbrev-ref HEAD", workingDirectory: path, cancellationToken: cancellationToken);
        return result.WithValue(result.Output?.Trim(), path);
    }

    public ExecuteResult<string> GetDefaultBranch(string path, CancellationToken cancellationToken = default)
    {
        var result = _executor.Execute("git", "symbolic-ref refs/remotes/origin/HEAD --short", workingDirectory: path, cancellationToken: cancellationToken);
        return result.Success ? result.WithValue(result.Output?.Trim().Split('/', 2)[1], path) : result.WithNull<string>(path);
    }

    public async Task<ExecuteResult<string>> GetDefaultBranchAsync(string path, CancellationToken cancellationToken = default)
    {
        var result = await _executor.ExecuteAsync("git", "symbolic-ref refs/remotes/origin/HEAD --short", workingDirectory: path, cancellationToken: cancellationToken);
        return result.Success ? result.WithValue(result.Output?.Trim().Split('/', 2)[1], path) : result.WithNull<string>(path);
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

    public ExecuteResult<GitModulesFile?> GetGitModulesFile(string path) =>
        GitModulesFile.TryParse(path, ".gitmodules", out var gitModulesFile, out var errorMessage)
            ? ExecuteResult.CreateWithObject<GitModulesFile?>(gitModulesFile, path)
            : new ExecuteResult<GitModulesFile?> { ExitCode = 1, Output = errorMessage, Reference = path };

    public async Task<ExecuteResult<GitLogEntry[]>> GetLogAsync(string path, string branch, string remoteName = "origin", string? after = null, string? before = null, string? author = null, string? messagePattern = null, string? logFilePattern = null, bool doNotIncludeAll = false, bool includeMerges = false, bool mergesOnly = false, bool doNotCompactMessages = false, CancellationToken cancellationToken = default)
    {
        var repoUrlExecResult = RepositoryName(path, remoteName, cancellationToken);
        if (!repoUrlExecResult.Success) return repoUrlExecResult.WithValue<GitLogEntry[]>([], path);
        var repoName = repoUrlExecResult.Value;

        var args = $"--no-pager log --numstat --date=iso --pretty=format:\"{_gitLogFormat}\"";
        if (after is not null) args += $" --after=\"{after}\"";
        if (before is not null) args += $" --before=\"{before}\"";
        if (author is not null) args += $" --author=\"{author}\"";
        if (!doNotIncludeAll) args += " --all";
        if (!includeMerges) args += " --no-merges"; else if (mergesOnly) args += " --merges";

        var gitLogExecResult = await _executor.ExecuteAsync("git", $"{args} {branch}", workingDirectory: path, cancellationToken: cancellationToken);
        if (!gitLogExecResult.Success)
        {
            return gitLogExecResult.WithNull<GitLogEntry[]>(path);
        }

        var entries = GitLogEntry.ParseMany(gitLogExecResult.Output).ToList();
        if (entries.Count == 0) return new ExecuteResult<GitLogEntry[]>
        {
            ExitCode = 0,
            Output = "No log entries found.",
            Value = [],
            Reference = path,
        };

        if (!string.IsNullOrWhiteSpace(messagePattern))
        {
            var logMessageRegex = new Regex(messagePattern, RegexOptions.IgnoreCase);
            entries.RemoveAll(e => logMessageRegex.IsMatch(e.Message));

            if (entries.Count == 0)
            {
                return new ExecuteResult<GitLogEntry[]>
                {
                    ExitCode = 0,
                    Output = "No log entries found matching the specified message pattern.",
                    Value = [],
                    Reference = path,
                };
            }
        }

        if (!string.IsNullOrWhiteSpace(logFilePattern))
        {
            var logFileRegex = new Regex(logFilePattern, RegexOptions.IgnoreCase);
            entries.RemoveAll(e => !e.NumStats.Any(ns => logFileRegex.IsMatch(ns.FilePath)));

            if (entries.Count == 0)
            {
                return new ExecuteResult<GitLogEntry[]>
                {
                    ExitCode = 0,
                    Output = "No log entries found matching the specified file pattern.",
                    Value = [],
                    Reference = path,
                };
            }
        }

        var distinctOrderedLogEntries = entries
            .DistinctBy(le => le.Hash)
            .ForEach(e => { e.Branch = branch; e.Repository = repoName; })
            .OrderByDescending(le => le.AuthorDate)
            .ToArray();

        return ExecuteResult.CreateWithObject(distinctOrderedLogEntries, path);
    }

    public ExecuteResult<GitRepository>[] GetRepositories(string path, CancellationToken cancellationToken = default)
    {
        var results = new List<ExecuteResult<GitRepository>>();
        results.AddRange(_fileSystem.GetGitDirectories(path).Select(d => GetRepository(d, isSubmodule: false, cancellationToken)));
        results.AddRange(GetSubmoduleDirectories(path, cancellationToken).Select(d => GetRepository(d, isSubmodule: true, cancellationToken)));

        return [.. results.OrderBy(r => r.Value!.Path).ForEach(r => r.Value!.SetRelativePath(path))];
    }

    public async Task<ExecuteResult<GitRepository>[]> GetRepositoriesAsync(string path, CancellationToken cancellationToken = default)
    {
        var tasks = new List<Task<ExecuteResult<GitRepository>>>();
        tasks.AddRange(_fileSystem.GetGitDirectories(path).Select(d => GetRepositoryAsync(d, isSubmodule: false, cancellationToken)));
        tasks.AddRange((await GetSubmoduleDirectoriesAsync(path, cancellationToken)).Select(d => GetRepositoryAsync(d, isSubmodule: true, cancellationToken)));
        var results = await Task.WhenAll(tasks);

        return [.. results.OrderBy(r => r.Value!.Path).ForEach(r => r.Value!.SetRelativePath(path))];
    }

    public ExecuteResult<GitRepository?> GetRepository(string path, CancellationToken cancellationToken = default) =>
        _fileSystem.IsGitDirectory(path)
            ? GetRepository(path, isSubmodule: false, cancellationToken).AsNullable()
            : new ExecuteResult<GitRepository?> { ExitCode = 1, Output = "Not a git repository", Reference = path };

    public ExecuteResult GetStatus(string path, bool porcelain = false, CancellationToken cancellationToken = default) =>
        _executor.Execute("git", CreateStatusArgs(porcelain), workingDirectory: path, cancellationToken: cancellationToken).WithReference(path);

    public async Task<ExecuteResult> GetStatusAsync(string path, bool porcelain = false, CancellationToken cancellationToken = default) =>
        (await _executor.ExecuteAsync("git", CreateStatusArgs(porcelain), workingDirectory: path, cancellationToken: cancellationToken)).WithReference(path);

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

    public async Task<ExecuteResult> PullAsync(string path, CancellationToken cancellationToken = default) =>
        (await _executor.ExecuteAsync("git", "pull", workingDirectory: path, cancellationToken: cancellationToken)).WithReference(path);

    public ExecuteResult Push(string path, CancellationToken cancellationToken = default) =>
        _executor.Execute("git", "push", workingDirectory: path, cancellationToken: cancellationToken).WithReference(path);

    public ExecuteResult<string> RepositoryName(string path, string remote = "origin", CancellationToken cancellationToken = default)
    {
        var execResult = _executor.Execute("git", $"config --get remote.{remote}.url", workingDirectory: path, cancellationToken: cancellationToken).WithReference(path);
        return execResult.Success
            ? execResult.WithValue(Path.GetFileNameWithoutExtension(execResult.Output.Trim().Split('/', StringSplitOptions.RemoveEmptyEntries).Last()))
            : execResult.WithNull<string>();
    }

    public ExecuteResult Reset(string path, string commit = "HEAD", GitResetMode resetType = GitResetMode.Mixed, int back = 0, CancellationToken cancellationToken = default) =>
        _executor.Execute("git", CreateResetArgs(commit, resetType, back), workingDirectory: path, cancellationToken: cancellationToken).WithReference(path);

    public async Task<ExecuteResult> ResetAsync(string path, string commit = "HEAD", GitResetMode mode = GitResetMode.Mixed, int back = 0, CancellationToken cancellationToken = default) =>
        (await _executor.ExecuteAsync("git", CreateResetArgs(commit, mode, back), workingDirectory: path, cancellationToken: cancellationToken)).WithReference(path);

    public ExecuteResult Restore(string path, string[] pathspecs, CancellationToken cancellationToken = default) =>
        _executor.Execute("git", $"restore {string.Join(' ', pathspecs.Select(sm => $"\"{sm}\""))}", workingDirectory: path, cancellationToken: cancellationToken).WithReference(path);

    public ExecuteResult SetHead(string path, string remote, string? branch = null, bool automatically = false, CancellationToken cancellationToken = default)
    {
        (bool success, string? output) = CreateSetHeadArgs(out string? args, remote, branch, automatically);
        if (!success) return new ExecuteResult { ExitCode = 1, Output = output, Reference = path };

        return _executor.Execute("git", args, workingDirectory: path, cancellationToken: cancellationToken).WithReference(path);
    }

    public async Task<ExecuteResult> SetHeadAsync(string path, string remote, string? branch = null, bool automatically = false, CancellationToken cancellationToken = default)
    {
        (bool success, string? output) = CreateSetHeadArgs(out string? args, remote, branch, automatically);
        if (!success) return new ExecuteResult { ExitCode = 1, Output = output, Reference = path };

        return (await _executor.ExecuteAsync("git", args, workingDirectory: path, cancellationToken: cancellationToken)).WithReference(path);
    }

    public ExecuteResult SetSubmoduleIgnoreOption(string path, GitSubmoduleIgnoreOption ignoreOption, CancellationToken cancellationToken = default)
    {
        if (!_fileSystem.FileExists(path, ".gitmodules")) return new ExecuteResult
        {
            ExitCode = 1,
            Output = "No .gitmodules file found",
            Reference = path,
        };

        var didParse = GitModulesFile.TryParse(path, ".gitmodules", out var gitModulesFile, out var errorMessage);
        if (!didParse) return new ExecuteResult
        {
            ExitCode = 1,
            Output = errorMessage,
            Reference = path,
        };

        gitModulesFile!.Submodules.ForEach(sm => sm.Ignore = ignoreOption);

        var didWrite = gitModulesFile.TryWrite(overwrite: true, out errorMessage);
        if (!didWrite) return new ExecuteResult
        {
            ExitCode = 1,
            Output = errorMessage,
            Reference = path,
        };

        return new ExecuteResult
        {
            ExitCode = 0,
            Output = "Updated .gitmodules file",
            Reference = path,
        };
    }

    private static string CreateCheckoutArgs(string branch, bool createBranch)
    {
        var args = "checkout";
        if (createBranch) args += " -b";
        args += $" {branch}";
        return args;
    }

    private static (bool success, string? output) CreateCleanArgs(bool recursively, bool force, bool ignoreIgnoreRules, bool cleanOnlyIgnored, bool dryRun, out string? args)
    {
        args = null;

        if (cleanOnlyIgnored && ignoreIgnoreRules) return (success: false, output: "Cannot use both 'only-ignored' and 'ignore-rules' switches together.");

        args = "clean";
        if (recursively) args += " -d";
        if (force) args += " -f";
        if (ignoreIgnoreRules) args += " -x";
        if (cleanOnlyIgnored) args += " -X";
        if (dryRun) args += " -n";

        return (success: true, output: null);
    }

    private static string CreateResetArgs(string commit, GitResetMode resetType, int back)
    {
        if (back > 0) commit = $"{commit}~{back}";

        var resetArg = resetType switch
        {
            GitResetMode.Hard => "--hard",
            GitResetMode.Soft => "--soft",
            _ => "--mixed"
        };

        return $"reset {resetArg} {commit}";
    }

    private static (bool success, string? output) CreateSetHeadArgs(out string? args, string remote = "origin", string? branch = null, bool automatically = false)
    {
        args = null;

        if (branch is null && !automatically) return (success: false, output: "Branch must be specified if not setting automatically");

        branch ??= "--auto"; // 'automatically' is true if branch is null

        args = $"remote set-head {remote} {branch}";

        return (success: true, output: null);
    }

    private static string CreateStatusArgs(bool porcelain) => porcelain ? "status --porcelain" : "status";

    private ExecuteResult<GitRepository> GetRepository(string path, bool isSubmodule, CancellationToken cancellationToken = default)
    {
        var defaultBranchExecResult = GetDefaultBranch(path, cancellationToken);
        var currentBranchExecResult = GetCurrentBranch(path, cancellationToken);

        return ExecuteResult.CreateWithObject(new GitRepository()
        {
            Error = new string?[] { currentBranchExecResult.Success ? null : currentBranchExecResult.Output, defaultBranchExecResult.Success ? null : defaultBranchExecResult.Output }.CombineNonNull("; "),
            CurrentBranch = currentBranchExecResult.Value,
            DefaultBranch = defaultBranchExecResult.Value,
            IsSubmodule = isSubmodule,
            Path = path
        }, path);
    }

    private async Task<ExecuteResult<GitRepository>> GetRepositoryAsync(string path, bool isSubmodule, CancellationToken cancellationToken = default)
    {
        var defaultBranchExecResult = await GetDefaultBranchAsync(path, cancellationToken);
        var currentBranchExecResult = await GetCurrentBranchAsync(path, cancellationToken);

        return ExecuteResult.CreateWithObject(new GitRepository()
        {
            Error = new string?[] { currentBranchExecResult.Success ? null : currentBranchExecResult.Output, defaultBranchExecResult.Success ? null : defaultBranchExecResult.Output }.CombineNonNull("; "),
            CurrentBranch = currentBranchExecResult.Value,
            DefaultBranch = defaultBranchExecResult.Value,
            IsSubmodule = isSubmodule,
            Path = path
        }, path);
    }

    #endregion 
}

#pragma warning restore CA1822 // Mark members as static