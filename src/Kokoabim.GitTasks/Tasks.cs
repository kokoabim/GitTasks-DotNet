using Kokoabim.CommandLineInterface;

namespace Kokoabim.GitTasks;

public class Tasks
{
    public const string DefaultCommandName = "status";

    private readonly FileSystem _fileSystem = new();
    private readonly Git _git = new();

    public async Task<int> CheckoutMainAsync(ConsoleContext context)
    {
        var path = _fileSystem.GetFullPath(context.GetString(Arguments.PathArgument.Name));
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
                repo.Results.Checkout = _git.Checkout(repo.Path, repo.DefaultBranch, context.CancellationToken);
                ConsoleOutput.WriteHeaderDynamicallyCheckout(repo);
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

    public async Task<int> FixReferenceAsync(ConsoleContext context)
    {
        var path = _fileSystem.GetFullPath(context.GetString(Arguments.PathArgument.Name));
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
        var path = _fileSystem.GetFullPath(context.GetString(Arguments.PathArgument.Name));
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
        var path = _fileSystem.GetFullPath(context.GetString(Arguments.PathArgument.Name));
        var repositoryExecResults = _git.GetRepositories(path, context.CancellationToken);
        if (repositoryExecResults.Length == 0)
        {
            Console.WriteLine($"No git repositories found: {path}");
            return 0;
        }

        var showGitOutput = context.HasSwitch(Arguments.ShowGitOutputSwitch.Name);
        var commit = context.GetOptionString(Arguments.CommitOption.Name);
        var moveBack = context.GetOptionInt(Arguments.MoveBackOption.Name);
        var resetMode = context.GetOptionEnum<GitResetMode>(Arguments.ResetModeOption.Name);

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

                Console.WriteLine();

                if (!string.IsNullOrWhiteSpace(repo.Results.Reset.Output)) ConsoleOutput.WriteLight(repo.Results.Reset.Output, true);
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
                },
                context.CancellationToken)
            ));

            Console.SetCursorPosition(0, cursorTop);
        }

        return 0;
    }

    public async Task<int> ShowStatusAsync(ConsoleContext context)
    {
        var path = _fileSystem.GetFullPath(context.GetString(Arguments.PathArgument.Name));
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