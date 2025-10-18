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
        var repositories = _git.GetRepositories(path, context.CancellationToken);
        if (repositories.Length == 0)
        {
            Console.WriteLine($"No git repositories found: {path}");
            return 0;
        }

        var pullSwitch = context.HasSwitch(Arguments.PullSwitch.Name);

        var pullTasks = pullSwitch
            ? repositories.ToDictionary(
                r => r,
                r => _git.PullAsync(r.Path, context.CancellationToken))
            : [];

        var cursorTop = ConsoleOutput.WriteHeadersRepositories(repositories);

        foreach (var repo in repositories)
        {
            repo.Results.Status = _git.GetStatus(repo.Path, porcelain: true, context.CancellationToken);
            ConsoleOutput.WriteHeaderDynamicallyStatus(repo);

            repo.Results.Checkout = _git.Checkout(repo.Path, repo.DefaultBranch, context.CancellationToken);
            ConsoleOutput.WriteHeaderDynamicallyCheckout(repo);

            if (!pullSwitch)
            {
                repo.Results.CommitPosition = _git.GetCommitPosition(repo.Path, repo.CurrentBranch, context.CancellationToken);
                ConsoleOutput.WriteHeaderCommitPosition(repo, dynamically: true);
            }
        }

        if (pullSwitch)
        {
            foreach (var repo in repositories)
            {
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

    public async Task<int> PullBranchAsync(ConsoleContext context)
    {
        var path = _fileSystem.GetFullPath(context.GetString(Arguments.PathArgument.Name));
        var repositories = _git.GetRepositories(path, context.CancellationToken);
        if (repositories.Length == 0)
        {
            Console.WriteLine($"No git repositories found: {path}");
            return 0;
        }

        var showGitOutput = context.HasSwitch(Arguments.ShowGitOutputSwitch.Name);

        if (showGitOutput)
        {
            var pullTasks = repositories.ToDictionary(
                r => r,
                r => _git.PullAsync(r.Path, context.CancellationToken));

            foreach (var repo in repositories)
            {
                repo.Results.Status = _git.GetStatus(repo.Path, porcelain: true, context.CancellationToken);
                ConsoleOutput.WriteHeaderRepository(repo, withStatus: true, newline: false);

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
            var cursorTop = ConsoleOutput.WriteHeadersRepositories(repositories);

            foreach (var repo in repositories)
            {
                repo.Results.Status = _git.GetStatus(repo.Path, porcelain: true, context.CancellationToken);
                ConsoleOutput.WriteHeaderDynamicallyStatus(repo);
                ConsoleOutput.WriteHeaderDynamicallyActivity(repo);
            }

            await Task.WhenAll(repositories.Select(r => _git.PullAsync(r.Path, context.CancellationToken)
                .ContinueWith(t =>
                {
                    r.Results.Pull = t.Result;
                    ConsoleOutput.WriteHeaderPull(r, dynamically: true);

                    r.Results.CommitPosition = _git.GetCommitPosition(r.Path, r.CurrentBranch, context.CancellationToken);
                    ConsoleOutput.WriteHeaderCommitPosition(r, dynamically: true);
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
        var repositories = _git.GetRepositories(path, context.CancellationToken);
        if (repositories.Length == 0)
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
                ? repositories.ToDictionary(
                    r => r,
                    r => _git.FetchAsync(r.Path, r.CurrentBranch, context.CancellationToken))
                : [];

            foreach (var repo in repositories)
            {
                repo.Results.Status = _git.GetStatus(repo.Path, porcelain: true, context.CancellationToken);
                ConsoleOutput.WriteHeaderRepository(repo, withStatus: true, newline: false);

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
                    : showPendingChanges && !string.IsNullOrWhiteSpace(repo.Results.Status.Output)
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
            var cursorTop = ConsoleOutput.WriteHeadersRepositories(repositories);

            foreach (var repo in repositories)
            {
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
                await Task.WhenAll(repositories.Select(r => _git.FetchAsync(r.Path, r.CurrentBranch, context.CancellationToken).ContinueWith(t =>
                {
                    r.Results.Fetch = t.Result;

                    r.Results.CommitPosition = _git.GetCommitPosition(r.Path, r.CurrentBranch, context.CancellationToken);
                    ConsoleOutput.WriteHeaderCommitPosition(r, dynamically: true);
                },
                context.CancellationToken)));
            }

            Console.SetCursorPosition(0, cursorTop);
        }

        return 0;
    }
}