namespace Kokoabim.GitTasks;

public class GitRepositoryAuthorStats
{
    public int AddedLines { get; set; }
    public string AuthorName { get; }
    public string AuthorEmail { get; }
    public int CommitCount { get; set; }
    public DateRange Dates { get; set; }
    public int DeletedLines { get; set; }
    public int FilesChanged { get; set; }
    public int MergeCount { get; set; }
    public string Repository { get; }

    public GitRepositoryAuthorStats(string authorName, string authorEmail, string repository)
    {
        AuthorName = authorName;
        AuthorEmail = authorEmail.ToLower();
        Repository = repository;
    }

    /// <summary>
    /// Converts a collection of GitLogEntry objects into an array of GitRepositoryAuthorStats, grouped by author email and repository.
    /// </summary>
    /// <returns>An array of GitRepositoryAuthorStats objects, ordered by commit count descending and then by author name, with initial GitLogEntry objects ordered by repository.</returns>
    public static GitRepositoryAuthorStats[] ToArray(IEnumerable<GitLogEntry> gitLogEntries)
    {
        return [.. gitLogEntries.GroupBy(e => new { AuthorEmail = e.AuthorEmail.ToLower(), Repository = e.Repository! })
            .Select(g =>
            {
                var first = g.First();
                var authorName = first.AuthorName;
                var authorEmail = first.AuthorEmail;

                var minDate = g.Min(e => e.CommitDate);
                var maxDate = g.Max(e => e.CommitDate);
                var dates = new DateRange { FromDate = minDate, ToDate = maxDate };

                var stats = new GitRepositoryAuthorStats(authorName, authorEmail, first.Repository!)
                {
                    AddedLines = g.Where(e => !e.IsMerge).Sum(e => e.NumStatsTotals.AddedLines),
                    CommitCount = g.Count(e => !e.IsMerge),
                    Dates = dates,
                    DeletedLines = g.Where(e => !e.IsMerge).Sum(e => e.NumStatsTotals.DeletedLines),
                    FilesChanged = g.Where(e => !e.IsMerge).Sum(e => e.NumStatsTotals.FilesChanged),
                    MergeCount = g.Count(e => e.IsMerge),
                };

                return stats;
            })];
    }

    /// <summary>
    /// Converts a collection of GitLogEntry objects into a dictionary mapping author names to arrays of GitRepositoryAuthorStats.
    /// </summary>
    /// <returns>A dictionary where each key is an author name and the corresponding value is an array of GitRepositoryAuthorStats for that author.</returns>
    public static Dictionary<string, GitRepositoryAuthorStats[]> ToDictionary(IEnumerable<GitLogEntry> gitLogEntries) =>
        ToArray(gitLogEntries)
            .GroupBy(e => e.AuthorEmail.ToLower())
            .ToDictionary(g => g.Key, g => g.ToArray());
}