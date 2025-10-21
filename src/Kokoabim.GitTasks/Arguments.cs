using Kokoabim.CommandLineInterface;

namespace Kokoabim.GitTasks;

public static class Arguments
{
    public static readonly ConsoleArgument CommitOption = new(
        type: ArgumentType.Option,
        identifier: "c",
        name: "commit",
        helpText: "Commit hash (default: HEAD)",
        defaultValue: "HEAD"
    );

    public static readonly ConsoleArgument FetchSwitch = new(
        type: ArgumentType.Switch,
        identifier: "f",
        name: "fetch",
        helpText: "Fetch changes from remote"
    );

    public static readonly ConsoleArgument MoveBackOption = new(
        type: ArgumentType.Option,
        identifier: "b",
        name: "move-back",
        helpText: "Move back N commits from specified commit",
        defaultValue: (uint)0,
        constraints: ArgumentConstraints.MustBeUInteger
    );

    public static readonly ConsoleArgument PathArgument = new(
        type: ArgumentType.Positional,
        name: "path",
        helpText: "Path to git repository",
        defaultValue: ".",
        constraints: ArgumentConstraints.DirectoryMustExist
    );

    public static readonly ConsoleArgument PendingChangesSwitch = new(
        type: ArgumentType.Switch,
        identifier: "p",
        name: "pending",
        helpText: "Show pending changes"
    );

    public static readonly ConsoleArgument PullSwitch = new(
        type: ArgumentType.Switch,
        identifier: "p",
        name: "pull",
        helpText: "Pull changes from remote"
    );

    public static readonly ConsoleArgument ResetModeOption = new(
        type: ArgumentType.Option,
        identifier: "m",
        name: "mode",
        helpText: "Reset mode (mixed, soft, hard; default: mixed)",
        defaultValue: "mixed",
        constraints: ArgumentConstraints.MustConvertToType,
        constraintType: typeof(GitResetMode)
    );

    public static readonly ConsoleArgument ShowGitOutputSwitch = new(
        type: ArgumentType.Switch,
        identifier: "s",
        name: "show-output",
        helpText: "Show git output"
    );
}