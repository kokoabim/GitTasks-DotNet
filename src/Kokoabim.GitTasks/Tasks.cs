using System.Text.RegularExpressions;
using Kokoabim.CommandLineInterface;

namespace Kokoabim.GitTasks;

public class Tasks
{
    public const string DefaultCommandName = "status";

    private readonly FileSystem _fileSystem = new();
    private readonly Git _git = new();

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

            repo.Results.Status = _git.GetStatus(repo.Path, porcelain: true, context.CancellationToken);
            ConsoleOutput.WriteHeaderDynamicallyStatus(repo);

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
}