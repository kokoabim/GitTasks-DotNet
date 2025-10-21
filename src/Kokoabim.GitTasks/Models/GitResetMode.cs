namespace Kokoabim.GitTasks;

public enum GitResetMode
{
    /// <summary>
    /// Performs a mixed reset. The HEAD is moved to the specified commit, and the index is updated to match, but the working directory is not changed.
    /// </summary>
    Mixed,

    /// <summary>
    /// Performs a soft reset. The HEAD is moved to the specified commit, but the index and working directory are not changed.
    /// </summary>
    Soft,

    /// <summary>
    /// Performs a hard reset. The HEAD, index, and working directory are all updated to match the specified commit.
    /// </summary>
    Hard
}
