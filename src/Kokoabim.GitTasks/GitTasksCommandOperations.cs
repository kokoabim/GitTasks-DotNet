using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Kokoabim.CommandLineInterface;

namespace Kokoabim.GitTasks;

public static class GitTasksCommandOperations
{
    public const string DefaultCommandName = "status";

    private static readonly FileSystem _fileSystem = new();
    private static readonly Git _git = new();
    private static readonly Regex _submoduleNewCommitsMatcher = new(@"modified:\s+(?<path>.+)\s+\(new commits\)", RegexOptions.Compiled);

    #region methods

    public static int Checkout(ConsoleContext context)
    {
        if (!TryGetRepository(context, out GitRepository? repository)) return 1;

        var branchOrCommit = context.GetString(GitTasksArguments.CheckoutBranchOrCommitArgument.Name);
        var createBranch = context.HasSwitch(GitTasksArguments.CheckoutCreateBranchSwitch.Name);
        var showGitOutput = context.HasSwitch(GitTasksArguments.ShowGitOutputSwitch.Name);

        ConsoleOutput.WriteHeaderRepository(repository, newline: false, withActivity: true, consoleTop: Console.CursorTop);

        if (branchOrCommit == "!")
        {
            if (repository.DefaultBranch is null)
            {
                if (!showGitOutput) ConsoleOutput.ClearHeaderDynamicallyActivity(repository, resetPosition: true);
                ConsoleOutput.WriteRed((showGitOutput ? "F" : " f") + "ailed to determine primary branch", newline: true);
                return 1;
            }

            branchOrCommit = repository.DefaultBranch;
        }
        else
        {
            var matchedBranches = _git.MatchBranches(repository, branchOrCommit).DistinctBy(b => b.Name).OrderBy(b => b.Name).ToArray();
            if (matchedBranches.Length > 1)
            {
                ConsoleOutput.ClearHeaderDynamicallyActivity(repository, resetPosition: true);
                Console.WriteLine();

                int option = ConsoleApp.GetOptionInput([.. matchedBranches.Select(b => b.Name)], "Select branch to checkout");
                if (option == 0) return 0;

                branchOrCommit = matchedBranches[option - 1].Name;
            }
            else
            {
                // NOTE: use original 'branchOrCommit' value if no branch matched since it may be a commit hash
                branchOrCommit = matchedBranches.Length == 1 ? matchedBranches[0].Name : branchOrCommit;
            }
        }

        repository.Results.Checkout = _git.Checkout(repository.Path, branchOrCommit, createBranch, context.CancellationToken);
        ConsoleOutput.WriteHeaderCheckout(repository, dynamically: Console.CursorLeft != 0 && !showGitOutput, onNewLine: showGitOutput || Console.CursorLeft == 0);
        Console.WriteLine();

        if (showGitOutput && !string.IsNullOrWhiteSpace(repository.Results.Checkout.Output)) ConsoleOutput.WriteLight(repository.Results.Checkout.Output, true);

        return 0;
    }

    public static async Task<int> CheckoutMainAsync(ConsoleContext context)
    {
        if (!TryGetRepositories(context, out ExecuteResult<GitRepository>[] repositoriesExecResults)) return 1;

        var pullMainBranch = context.HasSwitch(GitTasksArguments.MainPullSwitch.Name);

        var tasksByRepo = pullMainBranch
            ? repositoriesExecResults.Where(er => er.Success).ToDictionary(
                er => er.Value!,
                er => Task.Run(() =>
                {
                    var repo = er.Value!;
                    repo.Results.Pull = _git.Pull(repo.Path, context.CancellationToken);
                    if (repo.CurrentBranch is not null) repo.Results.CommitPosition = _git.GetCommitPosition(repo.Path, repo.CurrentBranch, context.CancellationToken);
                },
                context.CancellationToken))
            : [];

        var cursorTop = ConsoleOutput.WriteHeadersRepositories(repositoriesExecResults, withActivity: true);

        foreach (var repoExecResult in repositoriesExecResults)
        {
            if (!repoExecResult.Success) continue;
            var repo = repoExecResult.Value;

            if (repo.DefaultBranch is not null)
            {
                repo.Results.Checkout = _git.Checkout(repo.Path, repo.DefaultBranch, createBranch: false, context.CancellationToken);
                ConsoleOutput.WriteHeaderCheckout(repo, dynamically: true);
            }

            if (!pullMainBranch && repo.CurrentBranch is not null)
            {
                repo.Results.CommitPosition = _git.GetCommitPosition(repo.Path, repo.CurrentBranch, context.CancellationToken);
                ConsoleOutput.WriteHeaderCommitPosition(repo, dynamically: true);
            }

            if (pullMainBranch) ConsoleOutput.WriteHeaderDynamicallyActivity(repo);
        }

        if (pullMainBranch)
        {
            await Task.WhenAll(tasksByRepo.Values);

            foreach (var repo in tasksByRepo.Keys)
            {
                ConsoleOutput.WriteHeaderPull(repo, dynamically: true);
                ConsoleOutput.WriteHeaderCommitPosition(repo, dynamically: true);
            }
        }

        Console.SetCursorPosition(0, cursorTop);

        return 0;
    }

    public static async Task<int> CleanAsync(ConsoleContext context)
    {
        if (!TryGetRepositories(context, out ExecuteResult<GitRepository>[] repositoriesExecResults)) return 1;

        var cleanOnlyIgnored = context.HasSwitch(GitTasksArguments.CleanOnlyIgnoredSwitch.Name);
        var dryRun = context.HasSwitch(GitTasksArguments.CleanDryRunSwitch.Name);
        var force = context.HasSwitch(GitTasksArguments.CleanForceSwitch.Name);
        var ignoreIgnoreRules = context.HasSwitch(GitTasksArguments.CleanIgnoreIgnoredFilesSwitch.Name);
        var recursively = context.HasSwitch(GitTasksArguments.CleanRecursivelySwitch.Name);
        var showGitOutput = context.HasSwitch(GitTasksArguments.ShowGitOutputSwitch.Name);

        if (showGitOutput || dryRun)
        {
            foreach (var repoExecResult in repositoriesExecResults)
            {
                ConsoleOutput.WriteHeaderRepository(repoExecResult, withStatus: false, newline: !repoExecResult.Success);

                if (!repoExecResult.Success) continue;
                var repo = repoExecResult.Value;

                repo.Results.Clean = _git.Clean(repo.Path, recursively, force, ignoreIgnoreRules, cleanOnlyIgnored, dryRun, context.CancellationToken);
                ConsoleOutput.WriteHeaderClean(repo, dynamically: false);
                Console.WriteLine();

                if (!string.IsNullOrWhiteSpace(repo.Results.Clean.Output)) ConsoleOutput.WriteLight(repo.Results.Clean.Output, true);
            }
        }
        else
        {
            var cursorTop = ConsoleOutput.WriteHeadersRepositories(repositoriesExecResults, withActivity: true);

            var tasksByRepo = repositoriesExecResults.Where(r => r.Success).ToDictionary(
                er => er.Value!,
                er => Task.Run(() =>
                {
                    var repo = er.Value!;
                    repo.Results.Clean = _git.Clean(repo.Path, recursively, force, ignoreIgnoreRules, cleanOnlyIgnored, dryRun, context.CancellationToken);
                    ConsoleOutput.WriteHeaderClean(repo, dynamically: true);
                },
                context.CancellationToken));

            await Task.WhenAll(tasksByRepo.Values);

            Console.SetCursorPosition(0, cursorTop);
        }

        return 0;
    }

    public static async Task<int> FixReferenceAsync(ConsoleContext context)
    {
        if (!TryGetRepositories(context, out ExecuteResult<GitRepository>[] repositoriesExecResults)) return 1;

        var showGitOutput = context.HasSwitch(GitTasksArguments.ShowGitOutputSwitch.Name);

        if (showGitOutput)
        {
            foreach (var repoExecResult in repositoriesExecResults)
            {
                ConsoleOutput.WriteHeaderRepository(repoExecResult, withStatus: false, newline: !repoExecResult.Success, withActivity: repoExecResult.Success);

                if (!repoExecResult.Success) continue;
                var repo = repoExecResult.Value;

                setHead(context, repo);
                ConsoleOutput.WriteHeaderSetHead(repo, dynamically: true);
                Console.WriteLine();

                if (!string.IsNullOrWhiteSpace(repo.Results.SetHead?.Output)) ConsoleOutput.WriteLight(repo.Results.SetHead.Output, true);
            }
        }
        else
        {
            var cursorTop = ConsoleOutput.WriteHeadersRepositories(repositoriesExecResults, withActivity: true);

            var tasksByRepo = repositoriesExecResults.Where(r => r.Success).ToDictionary(
                er => er.Value!,
                er => Task.Run(() =>
                {
                    var repo = er.Value!;
                    setHead(context, repo);
                    ConsoleOutput.WriteHeaderSetHead(repo, dynamically: true);
                },
                context.CancellationToken));

            await Task.WhenAll(tasksByRepo.Values);

            Console.SetCursorPosition(0, cursorTop);
        }

        return 0;

        static void setHead(ConsoleContext context, GitRepository repo)
        {
            repo.Results.Branches = _git.GetBranches(repo.Path, context.CancellationToken);
            var remoteName = (repo.Results.Branches.Success
                ? repo.Results.Branches.Value!.FirstOrDefault(b => b.IsRemote)?.Remote
                : null)
                ?? "origin";

            repo.Results.SetHead = _git.SetHead(repo.Path, remoteName, automatically: true, cancellationToken: context.CancellationToken);
        }
    }

    public static int MergeInBranch(ConsoleContext context)
    {
        var pathOption = context.GetStringOrDefault(GitTasksArguments.MergeInPathOption.Name) ?? ".";

        if (!TryGetRepository(context, out GitRepository? repository, pathOverride: pathOption)) return 1;

        var branchOrCommit = context.GetString(GitTasksArguments.CheckoutBranchOrCommitArgument.Name);
        var doNotFetch = context.HasSwitch(GitTasksArguments.MergeInDoNotFetchSwitch.Name);
        var onlyLocal = context.HasSwitch(GitTasksArguments.MergeInUseLocalSwitch.Name);
        var onlyRemote = context.HasSwitch(GitTasksArguments.MergeInUseRemoteSwitch.Name);
        var remoteNameOption = context.GetStringOrDefault(GitTasksArguments.MergeInRemoteNameOption.Name) ?? repository.RemoteName ?? "origin";
        var showDiffOnly = context.HasSwitch(GitTasksArguments.MergeInDiffOnlySwitch.Name);
        var showGitOutput = context.HasSwitch(GitTasksArguments.ShowGitOutputSwitch.Name);
        var yes = context.HasSwitch(GitTasksArguments.MergeInYesSwitch.Name);

        ConsoleOutput.WriteHeaderRepository(repository, newline: showGitOutput, withActivity: !showGitOutput, consoleTop: Console.CursorTop);

        if (branchOrCommit == "!")
        {
            if (repository.DefaultBranch is null)
            {
                if (!showGitOutput) ConsoleOutput.ClearHeaderDynamicallyActivity(repository, resetPosition: true);
                ConsoleOutput.WriteRed((showGitOutput ? "F" : " f") + "ailed to determine primary branch", newline: true);
                return 1;
            }

            branchOrCommit = !onlyLocal
                ? $"{remoteNameOption}/{repository.DefaultBranch}"
                : repository.DefaultBranch;
        }
        else
        {
            var matchedBranches = _git.MatchBranches(repository, branchOrCommit).Where(b =>
                (!onlyLocal || !b.IsRemote)
                && (!onlyRemote || (b.IsRemote && b.Remote == remoteNameOption))).OrderBy(b => b.FullName).ToArray();

            if (matchedBranches.Length > 1)
            {
                if (!showGitOutput)
                {
                    ConsoleOutput.ClearHeaderDynamicallyActivity(repository, resetPosition: true);
                    Console.WriteLine();
                }

                int option = ConsoleApp.GetOptionInput([.. matchedBranches.Select(b => b.FullName)], "Select branch to merge in");
                if (option == 0) return 0;

                branchOrCommit = matchedBranches[option - 1].FullName;
            }
            else
            {
                // NOTE: use original 'branchOrCommit' value if no branch matched since it may be a commit hash
                branchOrCommit = matchedBranches.Length == 1 ? matchedBranches[0].FullName : branchOrCommit;
            }
        }

        if (branchOrCommit == repository.CurrentBranch)
        {
            if (!showGitOutput) ConsoleOutput.ClearHeaderDynamicallyActivity(repository, resetPosition: true);
            ConsoleOutput.WriteYellowAndLight((showGitOutput ? "N" : " n") + "o changes to merge ", "already on branch", newline: true);
            return 0;
        }

        if (!doNotFetch)
        {
            repository.Results.Fetch = _git.Fetch(repository.Path, cancellationToken: context.CancellationToken);

            if (showGitOutput && !string.IsNullOrWhiteSpace(repository.Results.Fetch.Output))
            {
                if (repository.Results.Fetch.Success) ConsoleOutput.WriteLight($"FETCH:{Environment.NewLine}" + repository.Results.Fetch.Output, true);
                else ConsoleOutput.WriteRedAndLight($"Failed to fetch:{Environment.NewLine}", repository.Results.Fetch.ToString(), true);
            }

            if (!showGitOutput && !repository.Results.Fetch.Success)
            {
                ConsoleOutput.ClearHeaderDynamicallyActivity(repository, resetPosition: true);
                ConsoleOutput.WriteRedAndLight(" failed to fetch ", repository.Results.Fetch.ToString(), true);
            }

            if (!repository.Results.Fetch.Success) return 1;
        }

        repository.Results.Diff = _git.DiffWith(repository.Path, branchOrCommit, context.CancellationToken);
        if (!repository.Results.Diff.Success)
        {
            if (!showGitOutput) ConsoleOutput.ClearHeaderDynamicallyActivity(repository, resetPosition: true);
            ConsoleOutput.WriteRedAndLight((showGitOutput ? "F" : " f") + "ailed to get diff" + (showGitOutput ? ":" : "") + Environment.NewLine, repository.Results.Diff.ToString(), true);

            return 1;
        }
        else if (repository.Results.Diff.Value.FilesChanged == 0)
        {
            if (!showGitOutput) ConsoleOutput.ClearHeaderDynamicallyActivity(repository, resetPosition: true);
            ConsoleOutput.WriteYellowAndLight((showGitOutput ? "N" : " n") + "o changes to merge ", $"no differences with {branchOrCommit}", newline: true);
            return 0;
        }

        if (showDiffOnly)
        {
            if (!showGitOutput)
            {
                ConsoleOutput.ClearHeaderDynamicallyActivity(repository, resetPosition: true);
                Console.WriteLine();
            }

            if (repository.Results.Diff.Value.FilesChanged == 0)
            {
                ConsoleOutput.WriteGreen((showGitOutput ? "N" : " n") + "o changes to merge", newline: true);
                return 0;
            }

            ConsoleOutput.WriteLight(repository.Results.Diff.Value.ToString(), true);
            ConsoleOutput.WriteLight(repository.Results.Diff.Value.Summary, true);
            return 0;
        }

        if (!yes)
        {
            if (!showGitOutput) Console.WriteLine();

            int option;
            do
            {
                option = ConsoleApp.GetOptionInput([$"Merge in {branchOrCommit}", "Show diff summary", "Show diff details"]);
                if (option == 0) return 0;

                if (option == 2) ConsoleOutput.WriteLight(repository.Results.Diff.Value.Summary, true);
                else if (option == 3) ConsoleOutput.WriteLight(repository.Results.Diff.Value.ToString(), true);
            }
            while (option != 1);

            repository.ConsolePosition.Left = 0;
        }

        repository.Results.Merge = _git.Merge(repository.Path, branchOrCommit, context.CancellationToken);
        ConsoleOutput.WriteHeaderMerge(repository, dynamically: !showGitOutput && yes, onNewLine: showGitOutput || !yes, showGitOutput: showGitOutput);
        Console.WriteLine();

        if (showGitOutput && !string.IsNullOrWhiteSpace(repository.Results.Merge.Output)) ConsoleOutput.WriteLight(repository.Results.Merge.Output, true);

        return repository.Results.Merge.Success ? 0 : 1;
    }

    public static async Task<int> PullBranchAsync(ConsoleContext context)
    {
        if (!TryGetRepositories(context, out ExecuteResult<GitRepository>[] repositoriesExecResults)) return 1;

        var showGitOutput = context.HasSwitch(GitTasksArguments.ShowGitOutputSwitch.Name);
        var asynchronously = !showGitOutput;

        var cursorTop = asynchronously
            ? ConsoleOutput.WriteHeadersRepositories(repositoriesExecResults, withActivity: true)
            : -1;

        var tasksByRepo = repositoriesExecResults.Where(r => r.Success).ToDictionary(
            er => er.Value!,
            er => Task.Run(() =>
            {
                var repo = er.Value!;
                repo.Results.Pull = _git.Pull(repo.Path, context.CancellationToken);
                if (asynchronously) ConsoleOutput.WriteHeaderPull(repo, dynamically: true);
                if (repo.CurrentBranch is not null) repo.Results.CommitPosition = _git.GetCommitPosition(repo.Path, repo.CurrentBranch, context.CancellationToken);
                if (asynchronously) ConsoleOutput.WriteHeaderCommitPosition(repo, dynamically: true);
            },
            context.CancellationToken));

        if (showGitOutput)
        {
            foreach (var repoExecResult in repositoriesExecResults)
            {
                ConsoleOutput.WriteHeaderRepository(repoExecResult, newline: !repoExecResult.Success);

                if (!repoExecResult.Success) continue;
                var repo = repoExecResult.Value;

                var task = tasksByRepo[repo];
                if (!task.IsCompleted) await task;

                ConsoleOutput.WriteHeaderPull(repo, dynamically: false);
                ConsoleOutput.WriteHeaderCommitPosition(repo, dynamically: false);
                Console.WriteLine();

                if (!string.IsNullOrWhiteSpace(repo.Results.Pull?.Output)) ConsoleOutput.WriteLight(repo.Results.Pull.Output, true);
            }
        }
        else
        {
            await Task.WhenAll(tasksByRepo.Values);

            Console.SetCursorPosition(0, cursorTop);
        }

        return 0;
    }

    public static async Task<int> ResetAsync(ConsoleContext context)
    {
        if (!TryGetRepositories(context, out ExecuteResult<GitRepository>[] repositoriesExecResults)) return 1;

        var clean = context.HasSwitch(GitTasksArguments.ResetCleanSwitch.Name);
        var commit = context.GetOptionString(GitTasksArguments.CommitOption.Name);
        var moveBack = context.GetOptionInt(GitTasksArguments.MoveBackOption.Name);
        var resetMode = context.GetOptionEnum<GitResetMode>(GitTasksArguments.ResetModeOption.Name);
        var showGitOutput = context.HasSwitch(GitTasksArguments.ShowGitOutputSwitch.Name);

        var cursorTop = !showGitOutput
            ? ConsoleOutput.WriteHeadersRepositories(repositoriesExecResults, withActivity: true)
            : -1;

        var tasksByRepo = repositoriesExecResults.Where(r => r.Success).ToDictionary(
            er => er.Value!,
            er => Task.Run(() =>
            {
                var dynamically = showGitOutput;

                var repo = er.Value!;
                repo.Results.Reset = _git.Reset(repo.Path, commit, resetMode, moveBack, context.CancellationToken);
                if (dynamically)
                {
                    ConsoleOutput.WriteHeaderReset(repo, dynamically: true);
                    ConsoleOutput.WriteHeaderDynamicallyActivity(repo);
                }

                if (repo.Results.Reset.Success && clean)
                {
                    repo.Results.Clean = _git.Clean(repo.Path, recursively: true, force: true, ignoreIgnoreRules: false, cleanOnlyIgnored: false, dryRun: false, context.CancellationToken);
                    if (dynamically) ConsoleOutput.WriteHeaderClean(repo, dynamically: true);
                }
            },
            context.CancellationToken));

        if (showGitOutput)
        {
            foreach (var repoExecResult in repositoriesExecResults)
            {
                ConsoleOutput.WriteHeaderRepository(repoExecResult, newline: !repoExecResult.Success);

                if (!repoExecResult.Success) continue;
                var repo = repoExecResult.Value;

                var task = tasksByRepo[repo];
                if (!task.IsCompleted) await task;

                ConsoleOutput.WriteHeaderReset(repo, dynamically: false);
                if (repo.Results.Reset?.Success == true && clean) ConsoleOutput.WriteHeaderClean(repo, dynamically: false);
                Console.WriteLine();

                if (!string.IsNullOrWhiteSpace(repo.Results.Reset?.Output)) ConsoleOutput.WriteLight(repo.Results.Reset.Output, true);
                if (clean && !string.IsNullOrWhiteSpace(repo.Results.Clean?.Output)) ConsoleOutput.WriteLight(repo.Results.Clean.Output, true);
            }
        }
        else
        {
            await Task.WhenAll(tasksByRepo.Values);

            Console.SetCursorPosition(0, cursorTop);
        }

        return 0;
    }

    public static int SetSubmoduleIgnoreOption(ConsoleContext context)
    {
        if (!TryGetRepository(context, out GitRepository? repo)) return 1;

        var ignoreOption = context.GetEnumOrDefault<GitSubmoduleIgnoreOption>(GitTasksArguments.SubmoduleIgnoreArgument.Name);
        if (ignoreOption is null)
        {
            Console.WriteLine($"Invalid submodule ignore option: {context.GetString(GitTasksArguments.SubmoduleIgnoreArgument.Name)}");
            return 1;
        }

        var setIgnoreOptionExecResult = _git.SetSubmoduleIgnoreOption(repo.Path, ignoreOption.Value, context.CancellationToken);
        if (!setIgnoreOptionExecResult.Success)
        {
            Console.WriteLine($"Failed to set submodule ignore option: {setIgnoreOptionExecResult.Output}");
            return 1;
        }

        Console.WriteLine($"Set submodule ignore option to '{ignoreOption.Value}'");
        return 0;
    }

    public static int ShowLog(ConsoleContext context)
    {
        if (!TryGetRepositories(context, out ExecuteResult<GitRepository>[] repositoriesExecResults)) return 1;

        var path = _fileSystem.GetFullPath(context.GetStringOrDefault(GitTasksArguments.PathArgument.Name) ?? ".");

        var gitLogSettings = new GitLogSettings(context);

        var doNotFetch = context.HasSwitch(GitTasksArguments.LogDoNotFetchSwitch.Name);

        var branchMatcher = !string.IsNullOrWhiteSpace(gitLogSettings.BranchPattern)
            ? new Regex($"^{Regex.Escape(gitLogSettings.BranchPattern).Replace("\\*", ".*")}$", RegexOptions.IgnoreCase)
            : null;

        var userSettings = UserSettings.TryLoad(out UserSettings? loadedUserSettings)
            ? loadedUserSettings
            : null;

        List<GitLogEntry> allLogEntries = [];

        foreach (var repoExecResult in repositoriesExecResults)
        {
            if (!repoExecResult.Success) continue;
            var repo = repoExecResult.Value;

            ConsoleOutput.WriteNormal(repo.NameAndRelativePath, false);
            repo.ConsolePosition.Left = repo.NameAndRelativePath.Length;
            repo.ConsolePosition.Top = Console.CursorTop;
            ConsoleOutput.WriteHeaderDynamicallyActivity(repo);

            if (branchMatcher is not null)
            {
                var branchesExecResult = _git.GetBranches(repo.Path, context.CancellationToken);
                if (!branchesExecResult.Success)
                {
                    ConsoleOutput.ClearHeaderDynamicallyActivity(repo, resetPosition: true);
                    ConsoleOutput.WriteRedAndLight(" failed to get branches ", branchesExecResult.ToString(), true);
                    continue;
                }

                var matchingBranches = branchesExecResult.Success
                    ? branchesExecResult.Value.Where(b => branchMatcher.IsMatch(b.Name) && b.Name != "HEAD").DistinctBy(b => b.Name).ToArray().Select(b => b.Name).ToArray()
                    : [];

                if (matchingBranches.Length == 0)
                {
                    ConsoleOutput.ClearHeaderDynamicallyActivity(repo, resetPosition: true);
                    ConsoleOutput.WriteYellowAndLight(" no matching branches found ", $"no branches matching pattern '{gitLogSettings.BranchPattern}' were found", true);
                    continue;
                }
                else if (matchingBranches.Length > 1)
                {
                    ConsoleOutput.ClearHeaderDynamicallyActivity(repo, resetPosition: true);
                    ConsoleOutput.WriteYellowAndLight(" multiple matching branches found ", $"branches matching pattern '{gitLogSettings.BranchPattern}': {string.Join(", ", matchingBranches)} • specify a more specific pattern or the full branch name", true);
                    continue;
                }

                gitLogSettings.Branch = matchingBranches[0];
            }
            else
            {
                var currentBranchExecResult = _git.GetCurrentBranch(repo.Path, context.CancellationToken);
                if (!currentBranchExecResult.Success)
                {
                    ConsoleOutput.ClearHeaderDynamicallyActivity(repo, resetPosition: true);
                    ConsoleOutput.WriteRedAndLight(" failed to get current branch ", currentBranchExecResult.ToString(), true);
                    continue;
                }

                gitLogSettings.Branch = currentBranchExecResult.Value;
            }

            if (!doNotFetch)
            {
                var fetchExecResult = _git.Fetch(repo.Path, gitLogSettings.Branch, context.CancellationToken);
                if (!fetchExecResult.Success)
                {
                    ConsoleOutput.ClearHeaderDynamicallyActivity(repo, resetPosition: true);
                    ConsoleOutput.WriteRedAndLight(" failed to fetch ", fetchExecResult.ToString(), true);
                    continue;
                }
            }

            gitLogSettings.Path = repo.Path;

            var logExecResult = _git.GetLog(gitLogSettings, context.CancellationToken);

            ConsoleOutput.ClearHeaderDynamicallyActivity(repo, resetPosition: true);

            if (!logExecResult.Success)
            {
                ConsoleOutput.WriteRedAndLight(" failed to get log ", logExecResult.ToString(), true);
                continue;
            }

            var logEntries = logExecResult.Value;
            if (logEntries.Length == 0)
            {
                ConsoleOutput.WriteYellowAndLight(" no log entries found ", "no log entries matched the specified criteria", true);
                continue;
            }

            userSettings?.ChangeAuthorNames(logEntries);
            userSettings?.LinkAuthorEmails(logEntries);

            Console.WriteLine();
            ConsoleOutput.WriteLogEntries(repo, gitLogSettings, logEntries);

            allLogEntries.AddRange(logEntries);
        }

        if (repositoriesExecResults.Length > 1 && allLogEntries.Count > 0)
        {
            var repoAuthorStats = GitRepositoryAuthorStats.ToDictionary(allLogEntries);

            Console.WriteLine();
            ConsoleOutput.WriteRepositoryAuthorStats(gitLogSettings, repoAuthorStats);
        }

        return 0;
    }

    public static async Task<int> ShowStatusAsync(ConsoleContext context)
    {
        if (!TryGetRepositories(context, out ExecuteResult<GitRepository>[] repositoriesExecResults)) return 1;

        var fetchRemote = context.HasSwitch(GitTasksArguments.FetchSwitch.Name);
        var showFullStatus = context.HasSwitch(GitTasksArguments.ShowGitOutputSwitch.Name);
        var showPendingChanges = context.HasSwitch(GitTasksArguments.PendingChangesSwitch.Name);
        var asynchronously = !(showFullStatus || showPendingChanges);

        int cursorTop = asynchronously
            ? ConsoleOutput.WriteHeadersRepositories(repositoriesExecResults, withActivity: true)
            : -1;

        var tasksByRepo = repositoriesExecResults.Where(r => r.Success).ToDictionary(
            er => er.Value!,
            er => Task.Run(() =>
            {
                var repo = er.Value!;

                if (asynchronously)
                {
                    repo.Results.Status = _git.GetStatus(repo.Path, porcelain: true, context.CancellationToken);
                    ConsoleOutput.WriteHeaderDynamicallyStatus(repo, withActivity: true);
                }

                if (fetchRemote) repo.Results.Fetch = _git.Fetch(repo.Path, repo.CurrentBranch, context.CancellationToken);

                if (repo.CurrentBranch is not null)
                {
                    repo.Results.CommitPosition = _git.GetCommitPosition(repo.Path, repo.CurrentBranch, context.CancellationToken);
                    if (asynchronously) ConsoleOutput.WriteHeaderCommitPosition(repo, dynamically: true);
                }

                if (!asynchronously) repo.Results.FullStatus = _git.GetStatus(repo.Path, porcelain: false, context.CancellationToken);
            },
            context.CancellationToken));

        if (!asynchronously)
        {
            foreach (var repoExecResult in repositoriesExecResults)
            {
                var repo = repoExecResult.Value;

                if (repo is not null) repo.Results.Status = _git.GetStatus(repo.Path, porcelain: true, context.CancellationToken);
                ConsoleOutput.WriteHeaderRepository(repoExecResult, withStatus: repo?.Results.Status is not null, newline: !repoExecResult.Success);

                if (!repoExecResult.Success || repo is null) continue;

                var task = tasksByRepo[repo];
                if (!task.IsCompleted) await task;

                ConsoleOutput.WriteHeaderCommitPosition(repo, dynamically: false);
                Console.WriteLine();

                var statusOutput = showFullStatus && !string.IsNullOrWhiteSpace(repo.Results.FullStatus?.Output)
                    ? repo.Results.FullStatus.Output
                    : showPendingChanges && !string.IsNullOrWhiteSpace(repo.Results.Status?.Output)
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
            await Task.WhenAll(tasksByRepo.Values);

            Console.SetCursorPosition(0, cursorTop);
        }

        return 0;
    }

    public static int UpdateSubmoduleCommits(ConsoleContext context)
    {
        if (!TryGetRepository(context, out GitRepository? repo)) return 1;

        var commitMessage = context.GetOptionStringOrDefault(GitTasksArguments.CommitMessageOption.Name);
        var createBranchName = context.GetOptionStringOrDefault(GitTasksArguments.CreateBranchOption.Name);
        var pushChanges = context.HasSwitch(GitTasksArguments.PushSwitch.Name);
        var showGitOutput = context.HasSwitch(GitTasksArguments.ShowGitOutputSwitch.Name);

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
                ConsoleOutput.WriteYellow("Repository has uncommitted changes", newline: true);
                return 1;
            }
        }

        var gitModulesFileExecResult = _git.GetGitModulesFile(repo.Path);
        if (!gitModulesFileExecResult.Success)
        {
            ConsoleOutput.WriteRed(gitModulesFileExecResult.ToString(), newline: true);
            return 1;
        }

        var submodules = gitModulesFileExecResult.Value.Submodules;
        if (!submodules.Any())
        {
            ConsoleOutput.WriteYellow("No submodules found in repository", newline: true);
            return 0;
        }

        var groupedByIgnore = submodules.GroupBy(s => s.Ignore);
        if (groupedByIgnore.Count() > 1)
        {
            ConsoleOutput.WriteYellowAndLight("Submodules have different ignore options ", "Use 'submodule-set-ignore' command to set all submodules to the same ignore option", newline: true);
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
                ConsoleOutput.WriteYellow("No new commits found in submodules", newline: true);
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

            ConsoleOutput.WriteGreen("Committed submodule updates", newline: true);

            if (!pushChanges) return 0;

            repo.Results.Push = _git.Push(repo.Path, context.CancellationToken);
            if (!repo.Results.Push.Success)
            {
                ConsoleOutput.WriteRedAndLight("Failed to push submodule updates: ", repo.Results.Push.ToString(), newline: true);
                return 1;
            }
            else if (showGitOutput && !string.IsNullOrWhiteSpace(repo.Results.Push.Output))
                ConsoleOutput.WriteLight($"PUSH:{Environment.NewLine}" + repo.Results.Push.Output, newline: true);

            ConsoleOutput.WriteGreen("Pushed submodule updates to remote", newline: true);

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

    private static bool TryGetRepositories(ConsoleContext context, out ExecuteResult<GitRepository>[] repositoriesExecResults, string? remoteName = null)
    {
        var path = _fileSystem.GetFullPath(context.GetStringOrDefault(GitTasksArguments.PathArgument.Name) ?? ".");

        repositoriesExecResults = _git.GetRepositories(path, remoteName, context.CancellationToken);
        if (repositoriesExecResults.Length == 0)
        {
            Console.WriteLine($"No git repositories found: {path}");
            return false;
        }

        return true;
    }

    private static bool TryGetRepository(ConsoleContext context, [NotNullWhen(true)] out GitRepository? repository, string? remoteName = null, string? pathOverride = null)
    {
        repository = null;

        var path = _fileSystem.GetFullPath(pathOverride ?? context.GetStringOrDefault(GitTasksArguments.PathArgument.Name) ?? ".");

        var repositoryExecResult = _git.GetRepository(path, remoteName, context.CancellationToken);
        if (!repositoryExecResult.Success || repositoryExecResult.Value.Success is false)
        {
            Console.WriteLine(repositoryExecResult.Value?.Error ?? repositoryExecResult.Output ?? $"No git repository found: {path}");
            return false;
        }

        repository = repositoryExecResult.Value;
        repository.SetRelativePath(path);

        return true;
    }

    #endregion 
}