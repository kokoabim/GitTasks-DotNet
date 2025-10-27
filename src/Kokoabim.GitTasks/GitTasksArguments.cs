using Kokoabim.CommandLineInterface;

namespace Kokoabim.GitTasks;

public static class GitTasksArguments
{
    #region properties

    public static ConsoleArgument CheckoutBranchOrCommitArgument => new(
        type: ArgumentType.Positional,
        name: "branch-or-commit",
        helpText: "Branch name or commit hash",
        isRequired: true
    );

    public static ConsoleArgument CheckoutCreateBranchSwitch => new(
        type: ArgumentType.Switch,
        identifier: "b",
        name: "create-branch",
        helpText: "Create and switch to a new branch"
    );

    public static ConsoleArgument MainPullSwitch => new(
        type: ArgumentType.Switch,
        identifier: "p",
        name: "pull",
        helpText: "Pull changes from remote"
    );

    public static ConsoleArgument CleanDryRunSwitch => new(
            type: ArgumentType.Switch,
            identifier: "n",
            name: "dry-run",
            helpText: "Dry-run",
            defaultValue: false
        );

    public static ConsoleArgument CleanForceSwitch => new(
        type: ArgumentType.Switch,
        identifier: "f",
        name: "force",
        helpText: "Force",
        defaultValue: false
    );

    public static ConsoleArgument CleanIgnoreIgnoredFilesSwitch => new(
        type: ArgumentType.Switch,
        identifier: "x",
        name: "ignore-rules",
        helpText: "Ignore .gitignore rules",
        defaultValue: false
    );

    public static ConsoleArgument CleanOnlyIgnoredSwitch => new(
        type: ArgumentType.Switch,
        identifier: "X",
        name: "only-ignored",
        helpText: "Remove only ignored files",
        defaultValue: false
    );

    public static ConsoleArgument CleanRecursivelySwitch => new(
        type: ArgumentType.Switch,
        identifier: "d",
        name: "recursive",
        helpText: "Recursively",
        defaultValue: false
    );

    public static ConsoleArgument CommitMessageOption => new(
        type: ArgumentType.Option,
        identifier: "m",
        name: "message",
        helpText: "Commit message"
    );

    public static ConsoleArgument CommitOption => new(
        type: ArgumentType.Option,
        identifier: "c",
        name: "commit",
        helpText: "Commit hash (default: HEAD)",
        defaultValue: "HEAD"
    );

    public static ConsoleArgument CreateBranchOption => new(
        type: ArgumentType.Option,
        identifier: "b",
        name: "branch",
        helpText: "Create a new branch and check it out"
    );

    public static ConsoleArgument FetchSwitch => new(
        type: ArgumentType.Switch,
        identifier: "f",
        name: "fetch",
        helpText: "Fetch changes from remote"
    );

    public static ConsoleArgument LogAfterOption => new(
        type: ArgumentType.Option,
        identifier: "a",
        name: "after",
        helpText: "Show commits after a specific date (default: 7 days ago)",
        defaultValue: "7 days ago"
    );

    public static ConsoleArgument LogAuthorOption => new(
        type: ArgumentType.Option,
        identifier: "c",
        name: "author",
        helpText: "Commits by a specific author"
    );

    public static ConsoleArgument LogBeforeOption => new(
        type: ArgumentType.Option,
        identifier: "b",
        name: "before",
        helpText: "Show commits before a specific date"
    );

    public static ConsoleArgument LogBranchPatternOption => new(
        type: ArgumentType.Option,
        identifier: "d",
        name: "branch",
        helpText: "Show commits with branch matching a specific pattern"
    );

    public static ConsoleArgument LogDoNotCompactMessageSwitch => new(
        type: ArgumentType.Switch,
        identifier: "c",
        name: "no-compact",
        helpText: "Do not compact commit messages"
    );

    public static ConsoleArgument LogDoNotFetchSwitch => new(
        type: ArgumentType.Switch,
        identifier: "F",
        name: "no-fetch",
        helpText: "Do not fetch from remote before showing log"
    );

    public static ConsoleArgument LogDoNotIncludeAllSwitch => new(
        type: ArgumentType.Switch,
        identifier: "l",
        name: "not-all",
        helpText: "Do not include all refs (default: include all refs)"
    );

    public static ConsoleArgument LogFilePatternOption => new(
        type: ArgumentType.Option,
        identifier: "f",
        name: "file",
        helpText: "Show commits affecting specific files or paths"
    );

    public static ConsoleArgument LogIncludeMergesSwitch => new(
        type: ArgumentType.Switch,
        identifier: "g",
        name: "merges",
        helpText: "Include merge commits"
    );

    public static ConsoleArgument LogMergesOnlySwitch => new(
        type: ArgumentType.Switch,
        identifier: "G",
        name: "merges-only",
        helpText: "Show only merge commits"
    );

    public static ConsoleArgument LogMessagePatternOption => new(
        type: ArgumentType.Option,
        identifier: "m",
        name: "message",
        helpText: "Show commits with message matching a specific pattern"
    );

    public static ConsoleArgument LogRemoteNameOption => new(
        type: ArgumentType.Option,
        identifier: "r",
        name: "remote",
        helpText: "Show commits from a specific remote (default: origin)",
        defaultValue: "origin"
    );

    public static ConsoleArgument LogSubjectOnlySwitch => new(
        type: ArgumentType.Switch,
        identifier: "s",
        name: "subject-only",
        helpText: "Show only commit subjects"
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

    public static ConsoleArgument PushSwitch => new(
        type: ArgumentType.Switch,
        identifier: "p",
        name: "push",
        helpText: "Push changes to remote"
    );

    public static ConsoleArgument ResetCleanSwitch => new(
        type: ArgumentType.Switch,
        identifier: "u",
        name: "clean",
        helpText: "Remove untracked files"
    );

    public static ConsoleArgument ResetModeOption => new(
        type: ArgumentType.Option,
        identifier: "m",
        name: "mode",
        helpText: "Reset mode (mixed, soft, hard; default: mixed)",
        defaultValue: "mixed",
        constraints: ArgumentConstraints.MustConvertToType,
        constraintType: typeof(GitResetMode)
    );

    public static ConsoleArgument ShowGitOutputSwitch => new(
        type: ArgumentType.Switch,
        identifier: "s",
        name: "show-output",
        helpText: "Show git output"
    );

    public static ConsoleArgument SubmoduleIgnoreArgument => new(
        type: ArgumentType.Positional,
        name: "ignore-option",
        helpText: "Submodule ignore option (none, untracked, dirty, all; default: none)",
        defaultValue: "none",
        constraints: ArgumentConstraints.MustConvertToType,
        constraintType: typeof(GitSubmoduleIgnoreOption)
    );

    #endregion 
}