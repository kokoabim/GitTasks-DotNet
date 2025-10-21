using Kokoabim.CommandLineInterface;

namespace Kokoabim.GitTasks;

public class Commands
{
    private readonly Tasks _tasks;

    public Commands() : this(new Tasks()) { }

    public Commands(Tasks tasks)
    {
        _tasks = tasks;
    }

    public ConsoleCommand[] Generate()
    {
        return [FixRefCommand(), MainCommand(), PullCommand(), ResetCommand(), StatusCommand()];
    }

    private ConsoleCommand FixRefCommand() => new(
        name: "fix-ref",
        titleText: "Fix git reference (e.g. \"ref is not a symbolic ref\")",
        arguments: [
            Arguments.PathArgument,
            Arguments.ShowGitOutputSwitch
        ],
        asyncFunction: _tasks.FixReferenceAsync
    );

    private ConsoleCommand MainCommand() => new(
        name: "main",
        titleText: "Checkout default branch",
        arguments: [
            Arguments.PathArgument,
            Arguments.PullSwitch,
            Arguments.ShowGitOutputSwitch
        ],
        asyncFunction: _tasks.CheckoutMainAsync
    );

    private ConsoleCommand PullCommand() => new(
        name: "pull",
        titleText: "Pull changes from remote",
        arguments: [
            Arguments.PathArgument,
            Arguments.ShowGitOutputSwitch
        ],
        asyncFunction: _tasks.PullBranchAsync
    );

    private ConsoleCommand ResetCommand() => new(
        name: "reset",
        titleText: "Reset git repository to a specific commit",
        arguments: [
            Arguments.PathArgument,
            Arguments.CommitOption,
            Arguments.ResetModeOption,
            Arguments.MoveBackOption,
            Arguments.ShowGitOutputSwitch
        ],
        asyncFunction: _tasks.ResetAsync
    );

    private ConsoleCommand StatusCommand() => new(
        name: "status",
        titleText: "Show status of git repository",
        arguments: [
            Arguments.PathArgument,
            Arguments.FetchSwitch,
            Arguments.PendingChangesSwitch,
            Arguments.ShowGitOutputSwitch
        ],
        asyncFunction: _tasks.ShowStatusAsync
    );
}