using System.Text.RegularExpressions;
using Kokoabim.CommandLineInterface;

namespace Kokoabim.GitTasks;

public class Tasks
{
    public const string DefaultCommandName = "status";

    private readonly FileSystem _fileSystem = new();
    private readonly Git _git = new();
    private static readonly Regex _submoduleNewCommitsMatcher = new(@"modified:\s+(?<path>.+)\s+\(new commits\)", RegexOptions.Compiled);

    public async Task<int> CheckoutAsync(ConsoleContext context)
    {
        var path = _fileSystem.GetFullPath(context.GetStringOrDefault(Arguments.PathArgument.Name) ?? ".");
        var repositoryExecResults = _git.GetRepositories(path, context.CancellationToken);
        if (repositoryExecResults.Length == 0)
        {
            Console.WriteLine($"No git repositories found: {path}");
            return 0;
        }

        var branchOrCommit = context.GetString(Arguments.BranchOrCommitArgument.Name);
        var createBranch = context.HasSwitch(Arguments.CreateBranchSwitch.Name);
        var showGitOutput = context.HasSwitch(Arguments.ShowGitOutputSwitch.Name);

        async Task<(bool flowControl, string? result)> matchCommitOrBranchNameAsync(GitRepository r, bool dynamically = false)
        {
            if (!branchOrCommit.Contains('*')) return (true, branchOrCommit);

            var branchPattern = new Regex($"^{Regex.Escape(branchOrCommit).Replace("\\*", ".*")}$", RegexOptions.IgnoreCase);
            r.Results.Branches = await _git.GetBranchesAsync(r.Path, context.CancellationToken);

            var matchingBranches = r.Results.Branches.Success
                ? r.Results.Branches.Object.Where(b => branchPattern.IsMatch(b.Name)).DistinctBy(b => b.Name).ToArray()
                : null;

            ConsoleOutput.WriteHeaderMatchBranch(r, matchingBranches, dynamically: dynamically);

            if (matchingBranches?.Length == 1) return (true, matchingBranches[0].Name);
            else return (false, null);
        }

        if (showGitOutput)
        {
            foreach (var repoExecResult in repositoryExecResults)
            {
                var repo = repoExecResult.Object;
                if (repo is not null) repo.Results.Status = await _git.GetStatusAsync(repo.Path, porcelain: true, context.CancellationToken);

                ConsoleOutput.WriteHeaderRepository(repoExecResult, withStatus: repo?.Results.Status is not null, newline: false);

                if (!repoExecResult.Success || repo is null)
                {
                    Console.WriteLine();
                    Console.WriteLine(repoExecResult.ToString());
                    continue;
                }

                var (flowControl, matchedBranchOrCommit) = await matchCommitOrBranchNameAsync(repo);
                if (!flowControl)
                {
                    Console.WriteLine();
                    continue;
                }

                repo.Results.Checkout = await _git.CheckoutAsync(repo.Path, matchedBranchOrCommit ?? branchOrCommit, createBranch, context.CancellationToken);
                ConsoleOutput.WriteHeaderCheckout(repo, dynamically: false);

                Console.WriteLine();

                if (!string.IsNullOrWhiteSpace(repo.Results.Checkout.Output)) ConsoleOutput.WriteLight(repo.Results.Checkout.Output, true);
            }
        }
        else
        {
            var cursorTop = ConsoleOutput.WriteHeadersRepositories(repositoryExecResults);

            foreach (var repoExecResult in repositoryExecResults)
            {
                if (!repoExecResult.Success || repoExecResult.Object is null) continue;
                var repo = repoExecResult.Object;

                ConsoleOutput.WriteHeaderDynamicallyActivity(repo);
            }

            foreach (var repoExecResult in repositoryExecResults)
            {
                if (!repoExecResult.Success || repoExecResult.Object is null) continue;
                var repo = repoExecResult.Object;

                repo.Results.Status = await _git.GetStatusAsync(repo.Path, porcelain: true, context.CancellationToken);
                ConsoleOutput.WriteHeaderDynamicallyStatus(repo);
                ConsoleOutput.WriteHeaderDynamicallyActivity(repo);

                var (flowControl, matchedBranchOrCommit) = await matchCommitOrBranchNameAsync(repo, dynamically: true);
                if (!flowControl) continue;

                repo.Results.Checkout = await _git.CheckoutAsync(repo.Path, matchedBranchOrCommit ?? branchOrCommit, createBranch, context.CancellationToken);
                ConsoleOutput.WriteHeaderCheckout(repo, dynamically: true);
            }

            Console.SetCursorPosition(0, cursorTop);
        }
        return 0;
    }

    public async Task<int> CheckoutMainAsync(ConsoleContext context)
    {
        var path = _fileSystem.GetFullPath(context.GetStringOrDefault(Arguments.PathArgument.Name) ?? ".");
        var repositoryExecResults = _git.GetRepositories(path, context.CancellationToken);
        if (repositoryExecResults.Length == 0)
        {
            Console.WriteLine($"No git repositories found: {path}");
            return 0;
        }

        var pullSwitch = context.HasSwitch(Arguments.PullSwitch.Name);

        var pullTasks = pullSwitch
            ? repositoryExecResults.Where(r => r.Success).ToDictionary(
                r => r.Object!,
                r => _git.PullAsync(r.Object!.Path, context.CancellationToken))
            : [];

        var cursorTop = ConsoleOutput.WriteHeadersRepositories(repositoryExecResults);

        foreach (var repoExecResult in repositoryExecResults)
        {
            if (!repoExecResult.Success || repoExecResult.Object is null) continue;
            var repo = repoExecResult.Object;

            ConsoleOutput.WriteHeaderDynamicallyActivity(repo);
        }

        foreach (var repoExecResult in repositoryExecResults)
        {
            if (!repoExecResult.Success || repoExecResult.Object is null) continue;
            var repo = repoExecResult.Object;

            repo.Results.Status = _git.GetStatus(repo.Path, porcelain: true, context.CancellationToken);
            ConsoleOutput.WriteHeaderDynamicallyStatus(repo);
            ConsoleOutput.WriteHeaderDynamicallyActivity(repo);

            if (repo.DefaultBranch is not null)
            {
                repo.Results.Checkout = _git.Checkout(repo.Path, repo.DefaultBranch, createBranch: false, context.CancellationToken);
                ConsoleOutput.WriteHeaderCheckout(repo, dynamically: true);
            }

            if (!pullSwitch && repo.CurrentBranch is not null)
            {
                repo.Results.CommitPosition = _git.GetCommitPosition(repo.Path, repo.CurrentBranch, context.CancellationToken);
                ConsoleOutput.WriteHeaderCommitPosition(repo, dynamically: true);
            }

            if (pullSwitch) ConsoleOutput.WriteHeaderDynamicallyActivity(repo);
        }

        if (pullSwitch)
        {
            foreach (var repoExecResult in repositoryExecResults)
            {
                if (!repoExecResult.Success || repoExecResult.Object is null) continue;
                var repo = repoExecResult.Object;

                var pullTask = pullTasks[repo];
                if (!pullTask.IsCompleted) await pullTask;
                repo.Results.Pull = pullTask.Result;

                ConsoleOutput.WriteHeaderPull(repo, dynamically: true);

                repo.Results.CommitPosition = _git.GetCommitPosition(repo.Path, repo.CurrentBranch, context.CancellationToken);
                ConsoleOutput.WriteHeaderCommitPosition(repo, dynamically: true);
            }
        }

        Console.SetCursorPosition(0, cursorTop);

        return 0;
    }

    public async Task<int> CleanAsync(ConsoleContext context)
    {
        var path = _fileSystem.GetFullPath(context.GetStringOrDefault(Arguments.PathArgument.Name) ?? ".");
        var repositoryExecResults = _git.GetRepositories(path, context.CancellationToken);
        if (repositoryExecResults.Length == 0)
        {
            Console.WriteLine($"No git repositories found: {path}");
            return 0;
        }

        var cleanOnlyIgnored = context.HasSwitch(Arguments.CleanOnlyIgnoredSwitch.Name);
        var dryRun = context.HasSwitch(Arguments.CleanDryRunSwitch.Name);
        var force = context.HasSwitch(Arguments.ForceSwitch.Name);
        var ignoreIgnoreRules = context.HasSwitch(Arguments.IgnoreIgnoredFilesSwitch.Name);
        var recursively = context.HasSwitch(Arguments.RecursivelySwitch.Name);
        var showGitOutput = context.HasSwitch(Arguments.ShowGitOutputSwitch.Name);

        if (showGitOutput || dryRun)
        {
            foreach (var repoExecResult in repositoryExecResults)
            {
                ConsoleOutput.WriteHeaderRepository(repoExecResult, withStatus: false, newline: false);

                if (!repoExecResult.Success || repoExecResult.Object is null)
                {
                    Console.WriteLine();
                    continue;
                }

                var repo = repoExecResult.Object;

                repo.Results.Clean = await _git.CleanAsync(repo.Path, recursively, force, ignoreIgnoreRules, cleanOnlyIgnored, dryRun, context.CancellationToken);
                ConsoleOutput.WriteHeaderClean(repo, dynamically: false);

                Console.WriteLine();

                if (!string.IsNullOrWhiteSpace(repo.Results.Clean.Output)) ConsoleOutput.WriteLight(repo.Results.Clean.Output, true);
            }
        }
        else
        {
            var cursorTop = ConsoleOutput.WriteHeadersRepositories(repositoryExecResults);

            foreach (var repoExecResult in repositoryExecResults)
            {
                if (!repoExecResult.Success || repoExecResult.Object is null) continue;
                var repo = repoExecResult.Object;

                ConsoleOutput.WriteHeaderDynamicallyActivity(repo);
            }

            var tasks = repositoryExecResults.Where(r => r.Success).ToDictionary(
                r => r.Object!,
                r => Task.Run(async () =>
                {
                    var repo = r.Object!;
                    repo.Results.Clean = await _git.CleanAsync(repo.Path, recursively, force, ignoreIgnoreRules, cleanOnlyIgnored, dryRun, context.CancellationToken);
                },
                context.CancellationToken));

            foreach (var repoExecResult in repositoryExecResults)
            {
                if (!repoExecResult.Success || repoExecResult.Object is null) continue;
                var repo = repoExecResult.Object;

                var task = tasks[repo];
                if (!task.IsCompleted) await task;

                ConsoleOutput.WriteHeaderClean(repo, dynamically: true);
            }

            Console.SetCursorPosition(0, cursorTop);
        }

        return 0;
    }

    public async Task<int> FixReferenceAsync(ConsoleContext context)
    {
        var path = _fileSystem.GetFullPath(context.GetStringOrDefault(Arguments.PathArgument.Name) ?? ".");
        var repositoryExecResults = _git.GetRepositories(path, context.CancellationToken);
        if (repositoryExecResults.Length == 0)
        {
            Console.WriteLine($"No git repositories found: {path}");
            return 0;
        }

        var showGitOutput = context.HasSwitch(Arguments.ShowGitOutputSwitch.Name);

        if (showGitOutput)
        {
            foreach (var repoExecResult in repositoryExecResults)
            {
                ConsoleOutput.WriteHeaderRepository(repoExecResult, withStatus: false, newline: false);

                if (!repoExecResult.Success || repoExecResult.Object is null) continue;

                var repo = repoExecResult.Object;

                repo.Results.Branches = _git.GetBranches(repo.Path, context.CancellationToken);
                var remoteName = (repo.Results.Branches.Success
                    ? repo.Results.Branches.Object!.FirstOrDefault(b => b.IsRemote)?.Remote
                    : null)
                    ?? "origin";

                repo.Results.SetHead = _git.SetHead(repo.Path, remoteName, automatically: true, cancellationToken: context.CancellationToken);
                ConsoleOutput.WriteHeaderSetHead(repo, dynamically: false);

                Console.WriteLine();

                if (!string.IsNullOrWhiteSpace(repo.Results.SetHead.Output)) ConsoleOutput.WriteLight(repo.Results.SetHead.Output, true);
            }
        }
        else
        {
            var cursorTop = ConsoleOutput.WriteHeadersRepositories(repositoryExecResults);

            foreach (var repoExecResult in repositoryExecResults)
            {
                if (!repoExecResult.Success || repoExecResult.Object is null) continue;
                var repo = repoExecResult.Object;

                ConsoleOutput.WriteHeaderDynamicallyActivity(repo);
            }

            var tasks = repositoryExecResults.Where(r => r.Success).ToDictionary(
                r => r.Object!,
                r => Task.Run(async () =>
                {
                    var repo = r.Object!;
                    repo.Results.Branches = await _git.GetBranchesAsync(repo.Path, context.CancellationToken);
                    var remoteName = (repo.Results.Branches.Success
                        ? repo.Results.Branches.Object!.FirstOrDefault(b => b.IsRemote)?.Remote
                        : null)
                        ?? "origin";

                    repo.Results.SetHead = await _git.SetHeadAsync(repo.Path, remoteName, automatically: true, cancellationToken: context.CancellationToken);
                },
                context.CancellationToken));

            foreach (var repoExecResult in repositoryExecResults)
            {
                if (!repoExecResult.Success || repoExecResult.Object is null) continue;
                var repo = repoExecResult.Object;

                var task = tasks[repo];
                if (!task.IsCompleted) await task;

                ConsoleOutput.WriteHeaderSetHead(repo, dynamically: true);
            }

            Console.SetCursorPosition(0, cursorTop);
        }

        return 0;
    }

    public async Task<int> PullBranchAsync(ConsoleContext context)
    {
        var path = _fileSystem.GetFullPath(context.GetStringOrDefault(Arguments.PathArgument.Name) ?? ".");
        var repositoryExecResults = _git.GetRepositories(path, context.CancellationToken);
        if (repositoryExecResults.Length == 0)
        {
            Console.WriteLine($"No git repositories found: {path}");
            return 0;
        }

        var showGitOutput = context.HasSwitch(Arguments.ShowGitOutputSwitch.Name);

        if (showGitOutput)
        {
            var pullTasks = repositoryExecResults.Where(r => r.Success).ToDictionary(
                r => r.Object!,
                r => _git.PullAsync(r.Object!.Path, context.CancellationToken));

            foreach (var repoExecResult in repositoryExecResults)
            {
                var repo = repoExecResult.Object;

                if (repo is not null) repo.Results.Status = _git.GetStatus(repo.Path, porcelain: true, context.CancellationToken);
                ConsoleOutput.WriteHeaderRepository(repoExecResult, withStatus: true, newline: false);

                if (!repoExecResult.Success || repo is null)
                {
                    Console.WriteLine();
                    continue;
                }

                var pullTask = pullTasks[repo];
                if (!pullTask.IsCompleted) await pullTask;
                repo.Results.Pull = pullTask.Result;

                ConsoleOutput.WriteHeaderPull(repo, dynamically: false);

                repo.Results.CommitPosition = _git.GetCommitPosition(repo.Path, repo.CurrentBranch, context.CancellationToken);
                ConsoleOutput.WriteHeaderCommitPosition(repo, dynamically: false);

                Console.WriteLine();

                if (!string.IsNullOrWhiteSpace(repo.Results.Pull.Output)) ConsoleOutput.WriteLight(repo.Results.Pull.Output, true);
            }
        }
        else
        {
            var cursorTop = ConsoleOutput.WriteHeadersRepositories(repositoryExecResults);

            foreach (var repoExecResult in repositoryExecResults)
            {
                if (!repoExecResult.Success || repoExecResult.Object is null) continue;
                var repo = repoExecResult.Object;

                ConsoleOutput.WriteHeaderDynamicallyActivity(repo);
            }

            foreach (var repoExecResult in repositoryExecResults)
            {
                if (!repoExecResult.Success || repoExecResult.Object is null) continue;
                var repo = repoExecResult.Object;

                repo.Results.Status = _git.GetStatus(repo.Path, porcelain: true, context.CancellationToken);
                ConsoleOutput.WriteHeaderDynamicallyStatus(repo);
                ConsoleOutput.WriteHeaderDynamicallyActivity(repo);
            }

            await Task.WhenAll(repositoryExecResults.Where(r => r.Success).Select(r => _git.PullAsync(r.Object!.Path, context.CancellationToken)
                .ContinueWith(t =>
                {
                    var repo = r.Object!;
                    repo.Results.Pull = t.Result;
                    ConsoleOutput.WriteHeaderPull(repo, dynamically: true);

                    repo.Results.CommitPosition = _git.GetCommitPosition(repo.Path, repo.CurrentBranch, context.CancellationToken);
                    ConsoleOutput.WriteHeaderCommitPosition(repo, dynamically: true);
                },
                context.CancellationToken)
            ));

            Console.SetCursorPosition(0, cursorTop);
        }

        return 0;
    }

    public async Task<int> ResetAsync(ConsoleContext context)
    {
        var path = _fileSystem.GetFullPath(context.GetStringOrDefault(Arguments.PathArgument.Name) ?? ".");
        var repositoryExecResults = _git.GetRepositories(path, context.CancellationToken);
        if (repositoryExecResults.Length == 0)
        {
            Console.WriteLine($"No git repositories found: {path}");
            return 0;
        }

        var clean = context.HasSwitch(Arguments.ResetCleanSwitch.Name);
        var commit = context.GetOptionString(Arguments.CommitOption.Name);
        var moveBack = context.GetOptionInt(Arguments.MoveBackOption.Name);
        var resetMode = context.GetOptionEnum<GitResetMode>(Arguments.ResetModeOption.Name);
        var showGitOutput = context.HasSwitch(Arguments.ShowGitOutputSwitch.Name);

        if (showGitOutput)
        {
            foreach (var repoExecResult in repositoryExecResults)
            {
                ConsoleOutput.WriteHeaderRepository(repoExecResult, withStatus: false, newline: false);

                if (!repoExecResult.Success || repoExecResult.Object is null)
                {
                    Console.WriteLine();
                    continue;
                }

                var repo = repoExecResult.Object;

                repo.Results.Reset = await _git.ResetAsync(repo.Path, commit, resetMode, moveBack, context.CancellationToken);
                ConsoleOutput.WriteHeaderReset(repo, dynamically: false);

                if (repo.Results.Reset.Success && clean)
                {
                    repo.Results.Clean = await _git.CleanAsync(repo.Path, recursively: true, force: true, ignoreIgnoreRules: false, cleanOnlyIgnored: false, dryRun: false, context.CancellationToken);
                    ConsoleOutput.WriteHeaderClean(repo, dynamically: false);
                }

                Console.WriteLine();

                if (!string.IsNullOrWhiteSpace(repo.Results.Reset.Output)) ConsoleOutput.WriteLight(repo.Results.Reset.Output, true);
                if (clean && repo.Results.Clean is not null && !string.IsNullOrWhiteSpace(repo.Results.Clean.Output)) ConsoleOutput.WriteLight(repo.Results.Clean.Output, true);
            }
        }
        else
        {
            var cursorTop = ConsoleOutput.WriteHeadersRepositories(repositoryExecResults);

            foreach (var repoExecResult in repositoryExecResults)
            {
                if (repoExecResult.Success) ConsoleOutput.WriteHeaderDynamicallyActivity(repoExecResult.Object);
            }

            await Task.WhenAll(repositoryExecResults.Where(r => r.Success).Select(r => _git.ResetAsync(r.Object!.Path, commit, resetMode, moveBack, context.CancellationToken)
                .ContinueWith(t =>
                {
                    var repo = r.Object!;
                    repo.Results.Reset = t.Result;
                    ConsoleOutput.WriteHeaderReset(repo, dynamically: true);

                    ConsoleOutput.WriteHeaderDynamicallyActivity(repo);

                    if (repo.Results.Reset.Success && clean)
                    {
                        repo.Results.Clean = _git.Clean(repo.Path, recursively: true, force: true, ignoreIgnoreRules: false, cleanOnlyIgnored: false, dryRun: false, context.CancellationToken);
                        ConsoleOutput.WriteHeaderClean(repo, dynamically: true);
                    }
                },
                context.CancellationToken)
            ));

            Console.SetCursorPosition(0, cursorTop);
        }

        return 0;
    }

    public async Task<int> SetSubmoduleIgnoreOptionAsync(ConsoleContext context)
    {
        var path = _fileSystem.GetFullPath(context.GetStringOrDefault(Arguments.PathArgument.Name) ?? ".");
        var repositoryExecResult = _git.GetRepository(path, context.CancellationToken);
        if (!repositoryExecResult.Success || repositoryExecResult.Object?.Success is false)
        {
            Console.WriteLine(repositoryExecResult.Object?.Error ?? repositoryExecResult.Output);
            return 1;
        }

        var repo = repositoryExecResult.Object!;
        var ignoreOption = context.GetEnumOrDefault<GitSubmoduleIgnoreOption>(Arguments.SubmoduleIgnoreArgument.Name);
        if (ignoreOption is null)
        {
            Console.WriteLine($"Invalid submodule ignore option: {context.GetString(Arguments.SubmoduleIgnoreArgument.Name)}");
            return 1;
        }

        var setIgnoreOptionExecResult = _git.SetSubmoduleIgnoreOption(repo.Path, ignoreOption.Value, context.CancellationToken);
        if (!setIgnoreOptionExecResult.Success)
        {
            Console.WriteLine($"Failed to set submodule ignore option: {setIgnoreOptionExecResult.Output}");
            return 1;
        }

        Console.WriteLine($"Set submodule ignore option to '{ignoreOption.Value}'.");
        return 0;
    }

    public async Task<int> ShowLogAsync(ConsoleContext context)
    {
        var path = _fileSystem.GetFullPath(context.GetStringOrDefault(Arguments.PathArgument.Name) ?? ".");
        var repositoryExecResults = _git.GetRepositories(path, context.CancellationToken);
        if (repositoryExecResults.Length == 0)
        {
            Console.WriteLine($"No git repositories found: {path}");
            return 0;
        }

        var logAfter = context.GetOptionStringOrDefault(Arguments.LogAfterOption.Name);
        var logAuthor = context.GetOptionStringOrDefault(Arguments.LogAuthorOption.Name);
        var logBefore = context.GetOptionStringOrDefault(Arguments.LogBeforeOption.Name);
        var logBranchPattern = context.GetOptionStringOrDefault(Arguments.LogBranchPatternOption.Name);
        var logDoNotCompactMessages = context.HasSwitch(Arguments.LogDoNotCompactMessageSwitch.Name);
        var logDoNotFetch = context.HasSwitch(Arguments.LogDoNotFetchSwitch.Name);
        var logDoNotIncludeAll = context.HasSwitch(Arguments.LogDoNotIncludeAllSwitch.Name);
        var logFilePattern = context.GetOptionStringOrDefault(Arguments.LogFilePatternOption.Name);
        var logIncludeMerges = context.HasSwitch(Arguments.LogIncludeMergesSwitch.Name);
        var logMergesOnly = context.HasSwitch(Arguments.LogMergesOnlySwitch.Name);
        var logMessagePattern = context.GetOptionStringOrDefault(Arguments.LogMessagePatternOption.Name);
        var logRemoteName = context.GetOptionString(Arguments.LogRemoteNameOption.Name);
        var logSubjectOnly = context.HasSwitch(Arguments.LogSubjectOnlySwitch.Name);

        var logBranchRegex = !string.IsNullOrWhiteSpace(logBranchPattern)
            ? new Regex($"^{Regex.Escape(logBranchPattern).Replace("\\*", ".*")}$", RegexOptions.IgnoreCase)
            : null;

        foreach (var repoExecResult in repositoryExecResults)
        {
            if (!repoExecResult.Success || repoExecResult.Object is null) continue;

            var repo = repoExecResult.Object;

            ConsoleOutput.WriteNormal(repo.RelativePath, false);
            repo.ConsolePosition.Left = repo.RelativePath.Length;
            repo.ConsolePosition.Top = Console.CursorTop;
            ConsoleOutput.WriteHeaderDynamicallyActivity(repo);

            string? branch = null;
            if (!string.IsNullOrWhiteSpace(logBranchPattern))
            {
                var branchesExecResult = _git.GetBranches(repo.Path, context.CancellationToken);
                if (!branchesExecResult.Success)
                {
                    ConsoleOutput.ClearHeaderDynamicallyActivity(repo, resetPosition: true);
                    ConsoleOutput.WriteRedAndLight(" Failed to get branches ", branchesExecResult.ToString(), true);
                    continue;
                }

                var matchingBranches = branchesExecResult.Success
                    ? branchesExecResult.Object.Where(b => logBranchRegex!.IsMatch(b.Name) && b.Name != "HEAD").DistinctBy(b => b.Name).ToArray().Select(b => b.Name).ToArray()
                    : [];

                if (matchingBranches.Length == 0)
                {
                    ConsoleOutput.ClearHeaderDynamicallyActivity(repo, resetPosition: true);
                    ConsoleOutput.WriteYellowAndLight(" No matching branches found ", $"No branches matching pattern '{logBranchPattern}' were found.", true);
                    continue;
                }
                else if (matchingBranches.Length > 1)
                {
                    ConsoleOutput.ClearHeaderDynamicallyActivity(repo, resetPosition: true);
                    ConsoleOutput.WriteYellowAndLight(" Multiple matching branches found ", $"Branches matching pattern '{logBranchPattern}': {string.Join(", ", matchingBranches)}. Specify a more specific pattern or the full branch name.", true);
                    continue;
                }

                branch = matchingBranches[0];
            }
            else
            {
                var currentBranchExecResult = _git.GetCurrentBranch(path, context.CancellationToken);
                if (!currentBranchExecResult.Success)
                {
                    ConsoleOutput.ClearHeaderDynamicallyActivity(repo, resetPosition: true);
                    ConsoleOutput.WriteRedAndLight(" Failed to get current branch ", currentBranchExecResult.ToString(), true);
                    continue;
                }

                branch = currentBranchExecResult.Object;
            }

            if (!logDoNotFetch)
            {
                var fetchExecResult = await _git.FetchAsync(repo.Path, branch, context.CancellationToken);
                if (!fetchExecResult.Success)
                {
                    ConsoleOutput.ClearHeaderDynamicallyActivity(repo, resetPosition: true);
                    ConsoleOutput.WriteRedAndLight(" Failed to fetch ", fetchExecResult.ToString(), true);
                    continue;
                }
            }

            var logExecResult = await _git.GetLogAsync(
                repo.Path,
                branch,
                logRemoteName,
                logAfter,
                logBefore,
                logAuthor,
                logMessagePattern,
                logFilePattern,
                logDoNotIncludeAll,
                logIncludeMerges,
                logMergesOnly,
                logDoNotCompactMessages,
                context.CancellationToken);

            ConsoleOutput.ClearHeaderDynamicallyActivity(repo, resetPosition: true);

            if (!logExecResult.Success)
            {
                ConsoleOutput.WriteRedAndLight(" Failed to get log ", logExecResult.ToString(), true);
                continue;
            }

            var logEntries = logExecResult.Object;
            if (logEntries.Length == 0)
            {
                ConsoleOutput.WriteYellowAndLight(" No log entries found ", "No log entries matched the specified criteria.", true);
                continue;
            }

            Console.WriteLine();
            ConsoleOutput.WriteLogEntries(logEntries, subjectOnly: logSubjectOnly, doNotCompactMessages: logDoNotCompactMessages);
        }

        return 0;
    }

    public async Task<int> ShowStatusAsync(ConsoleContext context)
    {
        var path = _fileSystem.GetFullPath(context.GetStringOrDefault(Arguments.PathArgument.Name) ?? ".");
        var repositoryExecResults = _git.GetRepositories(path, context.CancellationToken);
        if (repositoryExecResults.Length == 0)
        {
            Console.WriteLine($"No git repositories found: {path}");
            return 0;
        }

        var fetchRemote = context.HasSwitch(Arguments.FetchSwitch.Name);
        var showFullStatus = context.HasSwitch(Arguments.ShowGitOutputSwitch.Name);
        var showPendingChanges = context.HasSwitch(Arguments.PendingChangesSwitch.Name);

        if (showFullStatus || showPendingChanges)
        {
            var fetchTasks = fetchRemote
                ? repositoryExecResults.Where(r => r.Success && r.Object.CurrentBranch is not null).ToDictionary(
                    r => r.Object!,
                    r => _git.FetchAsync(r.Object!.Path, r.Object.CurrentBranch, context.CancellationToken))
                : [];

            foreach (var repoExecResult in repositoryExecResults)
            {
                var repo = repoExecResult.Object;

                if (repo is not null) repo.Results.Status = _git.GetStatus(repo.Path, porcelain: true, context.CancellationToken);
                ConsoleOutput.WriteHeaderRepository(repoExecResult, withStatus: true, newline: false);

                if (!repoExecResult.Success || repo is null)
                {
                    Console.WriteLine();
                    continue;
                }

                if (fetchRemote)
                {
                    var fetchTask = fetchTasks[repo];
                    if (!fetchTask.IsCompleted) await fetchTask;
                    repo.Results.Fetch = fetchTask.Result;
                }

                repo.Results.CommitPosition = _git.GetCommitPosition(repo.Path, repo.CurrentBranch, context.CancellationToken);
                ConsoleOutput.WriteHeaderCommitPosition(repo, dynamically: false);
                Console.WriteLine();

                repo.Results.FullStatus = _git.GetStatus(repo.Path, porcelain: false, context.CancellationToken);

                var statusOutput = showFullStatus && !string.IsNullOrWhiteSpace(repo.Results.FullStatus.Output)
                    ? repo.Results.FullStatus.Output
                    : showPendingChanges && !string.IsNullOrWhiteSpace(repo.Results.Status!.Output)
                        ? repo.Results.Status.Output
                        : null;

                var usePrefixes = (showFullStatus || showPendingChanges) && fetchRemote
                    && !string.IsNullOrWhiteSpace(repo.Results.Fetch?.Output) && !string.IsNullOrWhiteSpace(statusOutput);

                if (fetchRemote && !string.IsNullOrWhiteSpace(repo.Results.Fetch?.Output))
                    ConsoleOutput.WriteLight((usePrefixes ? $"FETCH:{Environment.NewLine}" : "") + repo.Results.Fetch.Output, true);

                if (!string.IsNullOrWhiteSpace(statusOutput))
                    ConsoleOutput.WriteLight((usePrefixes ? $"STATUS:{Environment.NewLine}" : "") + statusOutput, true);
            }
        }
        else
        {
            var cursorTop = ConsoleOutput.WriteHeadersRepositories(repositoryExecResults);

            foreach (var repoExecResult in repositoryExecResults)
            {
                if (!repoExecResult.Success || repoExecResult.Object is null) continue;
                var repo = repoExecResult.Object;

                ConsoleOutput.WriteHeaderDynamicallyActivity(repo);
            }

            foreach (var repoExecResult in repositoryExecResults)
            {
                if (!repoExecResult.Success || repoExecResult.Object is null) continue;
                var repo = repoExecResult.Object;

                repo.Results.Status = _git.GetStatus(repo.Path, porcelain: true, context.CancellationToken);
                ConsoleOutput.WriteHeaderDynamicallyStatus(repo);

                if (fetchRemote) ConsoleOutput.WriteHeaderDynamicallyActivity(repo);
                else
                {
                    repo.Results.CommitPosition = _git.GetCommitPosition(repo.Path, repo.CurrentBranch, context.CancellationToken);
                    ConsoleOutput.WriteHeaderCommitPosition(repo, dynamically: true);
                }
            }

            if (fetchRemote)
            {
                await Task.WhenAll(repositoryExecResults.Where(r => r.Success).Select(r => _git.FetchAsync(r.Object!.Path, r.Object.CurrentBranch, context.CancellationToken).ContinueWith(t =>
                {
                    var repo = r.Object!;
                    repo.Results.Fetch = t.Result;

                    if (repo.CurrentBranch is not null)
                    {
                        repo.Results.CommitPosition = _git.GetCommitPosition(repo.Path, repo.CurrentBranch, context.CancellationToken);
                        ConsoleOutput.WriteHeaderCommitPosition(repo, dynamically: true);
                    }
                },
                context.CancellationToken)));
            }

            Console.SetCursorPosition(0, cursorTop);
        }

        return 0;
    }

    public async Task<int> UpdateSubmoduleCommitsAsync(ConsoleContext context)
    {
        // ! TODO: TEST THIS!

        var path = _fileSystem.GetFullPath(context.GetStringOrDefault(Arguments.PathArgument.Name) ?? ".");
        var repositoryExecResult = _git.GetRepository(path, context.CancellationToken);
        if (!repositoryExecResult.Success || repositoryExecResult.Object?.Success is false)
        {
            Console.WriteLine(repositoryExecResult.Object?.Error ?? repositoryExecResult.Output);
            return 1;
        }

        var repo = repositoryExecResult.Object!;

        var commitMessage = context.GetOptionStringOrDefault(Arguments.CommitMessageOption.Name);
        var createBranchName = context.GetOptionStringOrDefault(Arguments.CreateBranchOption.Name);
        var pushChanges = context.HasSwitch(Arguments.PushSwitch.Name);
        var showGitOutput = context.HasSwitch(Arguments.ShowGitOutputSwitch.Name);

        repo.Results.Status = _git.GetStatus(repo.Path, porcelain: true, context.CancellationToken);
        if (!repo.Results.Status.Success)
        {
            ConsoleOutput.WriteRedAndLight("Failed to get repository status: ", repo.Results.Status.ToString(), newline: true);
            return 1;
        }
        else
        {
            if (showGitOutput && !string.IsNullOrWhiteSpace(repo.Results.Status.Output))
                ConsoleOutput.WriteLight($"STATUS:{Environment.NewLine}" + repo.Results.Status.Output, newline: true);

            if (repo.Results.Status.Output != string.Empty)
            {
                ConsoleOutput.WriteYellow("Repository has uncommitted changes.", newline: true);
                return 1;
            }
        }

        var gitModulesFileExecResult = _git.GetGitModulesFile(repo.Path, context.CancellationToken);
        if (!gitModulesFileExecResult.Success)
        {
            ConsoleOutput.WriteRed(gitModulesFileExecResult.ToString(), newline: true);
            return 1;
        }

        var submodules = gitModulesFileExecResult.Object.Submodules;
        if (!submodules.Any())
        {
            ConsoleOutput.WriteYellow("No submodules found in repository.", newline: true);
            return 0;
        }

        var groupedByIgnore = submodules.GroupBy(s => s.Ignore);
        if (groupedByIgnore.Count() > 1)
        {
            ConsoleOutput.WriteYellowAndLight("Submodules have different ignore options. ", "Use 'submodule-set-ignore' command to set all submodules to the same ignore option.", newline: true);
            return 1;
        }

        if (!string.IsNullOrWhiteSpace(createBranchName))
        {
            repo.Results.Checkout = _git.Checkout(repo.Path, createBranchName, createBranch: true, context.CancellationToken);
            if (!repo.Results.Checkout.Success)
            {
                ConsoleOutput.WriteRedAndLight($"Failed to create and switch to branch '{createBranchName}': ", repo.Results.Checkout.ToString(), newline: true);
                return 1;
            }
            else if (showGitOutput && !string.IsNullOrWhiteSpace(repo.Results.Checkout.Output))
                ConsoleOutput.WriteLight($"CHECKOUT:{Environment.NewLine}" + repo.Results.Checkout.Output, newline: true);
        }

        var currentIgnoreOption = groupedByIgnore.First().Key;

        var setIgnoreOptionExecResult = _git.SetSubmoduleIgnoreOption(repo.Path, GitSubmoduleIgnoreOption.Dirty, context.CancellationToken);
        if (!setIgnoreOptionExecResult.Success)
        {
            ConsoleOutput.WriteRedAndLight($"Failed to set submodule ignore option: ", setIgnoreOptionExecResult.ToString(), newline: true);
            return 1;
        }

        try
        {
            repo.Results.FullStatus = _git.GetStatus(repo.Path, porcelain: false, context.CancellationToken);
            if (!repo.Results.FullStatus.Success)
            {
                ConsoleOutput.WriteRedAndLight($"Failed to get repository status: ", repo.Results.FullStatus.ToString(), newline: true);
                return 1;
            }
            else if (showGitOutput && !string.IsNullOrWhiteSpace(repo.Results.FullStatus.Output))
                ConsoleOutput.WriteLight($"STATUS:{Environment.NewLine}" + repo.Results.FullStatus.Output, newline: true);

            var submodulesWithNewCommits = _submoduleNewCommitsMatcher.Matches(repo.Results.FullStatus.Output)
                .Cast<Match>()
                .Select(m => m.Groups["path"].Value)
                .Where(p => submodules.Any(s => s.Path == p))
                .ToArray();

            if (submodulesWithNewCommits.Length == 0)
            {
                ConsoleOutput.WriteYellow("No new commits found in submodules.", newline: true);
                return 0;
            }

            var addExecResult = _git.Add(repo.Path, submodulesWithNewCommits, context.CancellationToken);
            if (!addExecResult.Success)
            {
                ConsoleOutput.WriteRedAndLight($"Failed to add submodule commits: ", addExecResult.ToString(), newline: true);
                return 1;
            }
            else if (showGitOutput && !string.IsNullOrWhiteSpace(addExecResult.Output))
                ConsoleOutput.WriteLight($"ADD:{Environment.NewLine}" + addExecResult.Output, newline: true);

            ConsoleOutput.WriteGreen($"Staged {submodulesWithNewCommits.Length} submodule commit{(submodulesWithNewCommits.Length > 1 ? "s" : "")}:{Environment.NewLine}", newline: true);
            ConsoleOutput.WriteNormal($" • {string.Join($"{Environment.NewLine} • ", submodulesWithNewCommits)}", newline: true);

            if (string.IsNullOrWhiteSpace(commitMessage))
            {
                ConsoleOutput.WriteLight("Perform a commit to finalize updating the submodule commits. Use \"git restore --staged .\" to unstage.", newline: true);
                return 0;
            }

            repo.Results.Commit = _git.Commit(repo.Path, commitMessage, context.CancellationToken);
            if (!repo.Results.Commit.Success)
            {
                ConsoleOutput.WriteRedAndLight("Failed to commit submodule updates: ", repo.Results.Commit.ToString(), newline: true);
                return 1;
            }
            else if (showGitOutput && !string.IsNullOrWhiteSpace(repo.Results.Commit.Output))
                ConsoleOutput.WriteLight($"COMMIT:{Environment.NewLine}" + repo.Results.Commit.Output, newline: true);

            ConsoleOutput.WriteGreen("Committed submodule updates.", newline: true);

            if (!pushChanges) return 0;

            repo.Results.Push = _git.Push(repo.Path, context.CancellationToken);
            if (!repo.Results.Push.Success)
            {
                ConsoleOutput.WriteRedAndLight("Failed to push submodule updates: ", repo.Results.Push.ToString(), newline: true);
                return 1;
            }
            else if (showGitOutput && !string.IsNullOrWhiteSpace(repo.Results.Push.Output))
                ConsoleOutput.WriteLight($"PUSH:{Environment.NewLine}" + repo.Results.Push.Output, newline: true);

            ConsoleOutput.WriteGreen("Pushed submodule updates to remote.", newline: true);

            return 0;
        }
        finally
        {
            setIgnoreOptionExecResult = _git.SetSubmoduleIgnoreOption(repo.Path, currentIgnoreOption, context.CancellationToken);
            if (!setIgnoreOptionExecResult.Success)
            {
                ConsoleOutput.WriteRedAndLight($"Failed to set reset submodule ignore option to '{currentIgnoreOption.ToString().ToLower()}': ", setIgnoreOptionExecResult.ToString(), newline: true);
            }

            _ = _git.Restore(repo.Path, [".gitmodules"], context.CancellationToken);
        }
    }
}