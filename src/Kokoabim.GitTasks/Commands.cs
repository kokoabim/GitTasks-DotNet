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
        return [
            CheckoutCommand(),
            CleanCommand(),
            FixRefCommand(),
            MainCommand(),
            PullCommand(),
            ResetCommand(),
            StatusCommand(),
            SetSubmoduleIgnoreOptionCommand(),
            UpdateSubmoduleCommitsCommand()
        ];
    }

    private ConsoleCommand CheckoutCommand() => new(
        name: "checkout",
        titleText: "Checkout a specific branch or commit",
        arguments: [
            Arguments.BranchOrCommitArgument,
            Arguments.PathArgument,
            Arguments.CreateBranchSwitch,
            Arguments.ShowGitOutputSwitch
        ],
        asyncFunction: _tasks.CheckoutAsync
    );

    private ConsoleCommand CleanCommand() => new(
        name: "clean",
        titleText: "Remove untracked files",
        arguments: [
            Arguments.PathArgument,
            Arguments.RecursivelySwitch,
            Arguments.ForceSwitch,
            Arguments.IgnoreIgnoredFilesSwitch,
            Arguments.CleanOnlyIgnoredSwitch,
            Arguments.CleanDryRunSwitch,
            Arguments.ShowGitOutputSwitch
        ],
        asyncFunction: _tasks.CleanAsync
    );

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
            Arguments.ResetCleanSwitch,
            Arguments.ShowGitOutputSwitch
        ],
        asyncFunction: _tasks.ResetAsync
    );

    private ConsoleCommand SetSubmoduleIgnoreOptionCommand() => new(
        name: "submodule-set-ignore",
        titleText: "Set submodule ignore option",
        arguments: [
            Arguments.SubmoduleIgnoreArgument,
            Arguments.PathArgument,
            Arguments.ShowGitOutputSwitch
        ],
        asyncFunction: _tasks.SetSubmoduleIgnoreOptionAsync
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

    private ConsoleCommand UpdateSubmoduleCommitsCommand() => new(
        name: "submodule-commits",
        titleText: "Update submodule commits",
        arguments: [
            Arguments.PathArgument,
            Arguments.ShowGitOutputSwitch
        ],
        asyncFunction: _tasks.UpdateSubmoduleCommitsAsync
    );
}