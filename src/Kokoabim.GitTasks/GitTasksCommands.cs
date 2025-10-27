using Kokoabim.CommandLineInterface;

namespace Kokoabim.GitTasks;

public static class GitTasksCommands
{
    #region methods

    public static ConsoleCommand[] Create() =>
    [
        CheckoutCommand(),
        CleanCommand(),
        FixRefCommand(),
        LogCommand(),
        MainCommand(),
        PullCommand(),
        ResetCommand(),
        SetSubmoduleIgnoreOptionCommand(),
        StatusCommand(),
        UpdateSubmoduleCommitsCommand()
    ];

    private static ConsoleCommand CheckoutCommand() => new(
        name: "checkout",
        titleText: "Checkout a branch or commit",
        arguments: [
            GitTasksArguments.CheckoutBranchOrCommitArgument,
            GitTasksArguments.PathArgument,
            GitTasksArguments.CheckoutCreateBranchSwitch,
            GitTasksArguments.ShowGitOutputSwitch
        ],
        asyncFunction: GitTasksCommandOperations.CheckoutAsync
    );

    private static ConsoleCommand CleanCommand() => new(
        name: "clean",
        titleText: "Remove untracked files",
        arguments: [
            GitTasksArguments.PathArgument,
            GitTasksArguments.CleanRecursivelySwitch,
            GitTasksArguments.CleanForceSwitch,
            GitTasksArguments.CleanIgnoreIgnoredFilesSwitch,
            GitTasksArguments.CleanOnlyIgnoredSwitch,
            GitTasksArguments.CleanDryRunSwitch,
            GitTasksArguments.ShowGitOutputSwitch
        ],
        asyncFunction: GitTasksCommandOperations.CleanAsync
    );

    private static ConsoleCommand FixRefCommand() => new(
        name: "fix-ref",
        titleText: "Fix git reference (e.g. \"ref is not a symbolic ref\")",
        arguments: [
            GitTasksArguments.PathArgument,
            GitTasksArguments.ShowGitOutputSwitch
        ],
        syncFunction: GitTasksCommandOperations.FixReference
    );

    private static ConsoleCommand LogCommand() => new(
        name: "log",
        titleText: "Show git log",
        arguments: [
            GitTasksArguments.PathArgument,
            GitTasksArguments.LogAfterOption,
            GitTasksArguments.LogAuthorOption,
            GitTasksArguments.LogBeforeOption,
            GitTasksArguments.LogBranchPatternOption,
            GitTasksArguments.LogDoNotCompactMessageSwitch,
            GitTasksArguments.LogDoNotFetchSwitch,
            GitTasksArguments.LogFilePatternOption,
            GitTasksArguments.LogIncludeMergesSwitch,
            GitTasksArguments.LogMergesOnlySwitch,
            GitTasksArguments.LogMessagePatternOption,
            GitTasksArguments.LogRemoteNameOption,
            GitTasksArguments.LogSubjectOnlySwitch
        ],
        syncFunction: GitTasksCommandOperations.ShowLog
    );

    private static ConsoleCommand MainCommand() => new(
        name: "main",
        titleText: "Checkout default branch",
        arguments: [
            GitTasksArguments.PathArgument,
            GitTasksArguments.MainPullSwitch,
            GitTasksArguments.ShowGitOutputSwitch
        ],
        asyncFunction: GitTasksCommandOperations.CheckoutMainAsync
    );

    private static ConsoleCommand PullCommand() => new(
        name: "pull",
        titleText: "Pull changes from remote",
        arguments: [
            GitTasksArguments.PathArgument,
            GitTasksArguments.ShowGitOutputSwitch
        ],
        asyncFunction: GitTasksCommandOperations.PullBranchAsync
    );

    private static ConsoleCommand ResetCommand() => new(
        name: "reset",
        titleText: "Reset git repository to a commit",
        arguments: [
            GitTasksArguments.PathArgument,
            GitTasksArguments.CommitOption,
            GitTasksArguments.ResetModeOption,
            GitTasksArguments.MoveBackOption,
            GitTasksArguments.ResetCleanSwitch,
            GitTasksArguments.ShowGitOutputSwitch
        ],
        asyncFunction: GitTasksCommandOperations.ResetAsync
    );

    private static ConsoleCommand SetSubmoduleIgnoreOptionCommand() => new(
        name: "submodule-set-ignore",
        titleText: "Set submodule ignore option",
        arguments: [
            GitTasksArguments.SubmoduleIgnoreArgument,
            GitTasksArguments.PathArgument,
            GitTasksArguments.ShowGitOutputSwitch
        ],
        syncFunction: GitTasksCommandOperations.SetSubmoduleIgnoreOption
    );

    private static ConsoleCommand StatusCommand() => new(
        name: "status",
        titleText: "Show status of git repository",
        arguments: [
            GitTasksArguments.PathArgument,
            GitTasksArguments.FetchSwitch,
            GitTasksArguments.PendingChangesSwitch,
            GitTasksArguments.ShowGitOutputSwitch
        ],
        asyncFunction: GitTasksCommandOperations.ShowStatusAsync
    );

    private static ConsoleCommand UpdateSubmoduleCommitsCommand() => new(
        name: "submodule-commits",
        titleText: "Update submodule commits",
        arguments: [
            GitTasksArguments.PathArgument,
            GitTasksArguments.CreateBranchOption,
            GitTasksArguments.CommitMessageOption,
            GitTasksArguments.PushSwitch,
            GitTasksArguments.ShowGitOutputSwitch
        ],
        syncFunction: GitTasksCommandOperations.UpdateSubmoduleCommits
    );

    #endregion 
}