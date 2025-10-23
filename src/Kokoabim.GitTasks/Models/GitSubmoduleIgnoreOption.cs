namespace Kokoabim.GitTasks;

public enum GitSubmoduleIgnoreOption
{
    /// <summary>
    /// No modifications to submodules are ignored, all of committed differences, and modifications to tracked and untracked files are shown. This is the default option.
    /// </summary>
    None,

    /// <summary>
    /// Only untracked files in submodules will be ignored. Committed differences and modifications to tracked files will show up.
    /// </summary>
    Untracked,

    /// <summary>
    /// All changes to the submodule’s work tree will be ignored, only committed differences between the HEAD of the submodule and its recorded state in the superproject are taken into account.
    /// </summary>
    Dirty,

    /// <summary>
    /// The submodule will never be considered modified (but will nonetheless show up in the output of status and commit when it has been staged).
    /// </summary>
    All
}