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
        return [MainCommand(), PullCommand(), StatusCommand()];
    }

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