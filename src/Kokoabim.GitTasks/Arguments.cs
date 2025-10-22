using Kokoabim.CommandLineInterface;

namespace Kokoabim.GitTasks;

public static class Arguments
{
    #region properties

    public static ConsoleArgument BranchOrCommitArgument => new(
        type: ArgumentType.Positional,
        name: "branch-or-commit",
        helpText: "Branch name or commit hash",
        isRequired: true
    );

    public static ConsoleArgument CleanDryRunSwitch => new(
            type: ArgumentType.Switch,
            identifier: "n",
            name: "dry-run",
            helpText: "Dry-run",
            defaultValue: false
        );

    public static ConsoleArgument CleanOnlyIgnoredSwitch => new(
        type: ArgumentType.Switch,
        identifier: "X",
        name: "only-ignored",
        helpText: "Remove only ignored files",
        defaultValue: false
    );

    public static ConsoleArgument CommitOption => new(
        type: ArgumentType.Option,
        identifier: "c",
        name: "commit",
        helpText: "Commit hash (default: HEAD)",
        defaultValue: "HEAD"
    );

    public static ConsoleArgument CreateBranchSwitch => new(
        type: ArgumentType.Switch,
        identifier: "b",
        name: "create-branch",
        helpText: "Create and switch to a new branch"
    );

    public static ConsoleArgument FetchSwitch => new(
        type: ArgumentType.Switch,
        identifier: "f",
        name: "fetch",
        helpText: "Fetch changes from remote"
    );

    public static ConsoleArgument ForceSwitch => new(
        type: ArgumentType.Switch,
        identifier: "f",
        name: "force",
        helpText: "Force",
        defaultValue: false
    );

    public static ConsoleArgument IgnoreIgnoredFilesSwitch => new(
        type: ArgumentType.Switch,
        identifier: "x",
        name: "ignore-rules",
        helpText: "Ignore .gitignore rules",
        defaultValue: false
    );

    public static ConsoleArgument MoveBackOption => new(
        type: ArgumentType.Option,
        identifier: "b",
        name: "move-back",
        helpText: "Move back N commits from specified commit",
        defaultValue: (uint)0,
        constraints: ArgumentConstraints.MustBeUInteger
    );

    public static ConsoleArgument PathArgument => new(
        type: ArgumentType.Positional,
        name: "path",
        helpText: "Git repository path",
        defaultValue: ".",
        constraints: ArgumentConstraints.DirectoryMustExist
    );

    public static ConsoleArgument PendingChangesSwitch => new(
        type: ArgumentType.Switch,
        identifier: "p",
        name: "pending",
        helpText: "Show pending changes"
    );

    #endregion 

    public static readonly ConsoleArgument PullSwitch = new(
        type: ArgumentType.Switch,
        identifier: "p",
        name: "pull",
        helpText: "Pull changes from remote"
    );

    public static readonly ConsoleArgument RecursivelySwitch = new(
        type: ArgumentType.Switch,
        identifier: "d",
        name: "recursive",
        helpText: "Recursively",
        defaultValue: false
    );

    public static readonly ConsoleArgument ResetCleanSwitch = new(
        type: ArgumentType.Switch,
        identifier: "u",
        name: "clean",
        helpText: "Remove untracked files"
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