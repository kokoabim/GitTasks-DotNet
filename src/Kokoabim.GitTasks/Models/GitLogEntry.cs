using System.Text.RegularExpressions;

namespace Kokoabim.GitTasks;

public class GitLogEntry
{
    private static readonly Regex _approvedByMatcher = new(@"^Approved-by: (?<ApprovedBy>.+)$", RegexOptions.Multiline);
    private static readonly Regex _entriesMatcher = new(@"AuthorDate=(?<AuthorDate>[^\n]*)\nAuthorName=(?<AuthorName>[^\n]*)\nCommitDate=(?<CommitDate>[^\n]*)\nCommitterName=(?<CommitterName>[^\n]*)\nDecorations=(?<Decorations>.*?)\nHash=(?<Hash>[0-9a-f]+)\nMessageBody=(?<MessageBody>.*?)\nMessageSubject=(?<MessageSubject>[^\n]*)\nParentHashes=(?<ParentHashes>[^\n]*)\nNumStats=\n(?<NumStats>(?:\d+\t\d+\t[^\n]+\n?)*)", RegexOptions.Singleline | RegexOptions.Multiline | RegexOptions.IgnoreCase);
    private static readonly Regex _threeOrMoreNewLinesMatcher = new(@"\n{3,}");
    private static readonly Regex _twoOrMoreNewLinesMatcher = new(@"\n{2,}");

    public string[]? Approvers { get; set; }
    public DateTime AuthorDate { get; set; } // %ad
    public string AuthorName { get; set; } = null!; // %an
    public string Branch { get; set; } = null!;
    public DateTime CommitDate { get; set; } // %cd
    public string CommitterName { get; set; } = null!; // %cn
    public string Decorations { get; set; } = null!; // %d
    public GitHash Hash { get; set; } = null!; // %H
    public string Message { get; set; } = null!;
    public string MessageBody { get; set; } = null!; // %B
    public string MessageSubject { get; set; } = null!; // %s
    public IReadOnlyCollection<GitLogEntryNumStat> NumStats { get; set; } = []; // --numstat
    public GitLogNumStatsTotals NumStatsTotals { get; set; } = null!;
    public IReadOnlyCollection<GitHash> ParentHashes { get; set; } = null!; // %P
    public string Repository { get; set; } = null!;

    public static IReadOnlyCollection<GitLogEntry> ParseMany(string gitLogText, bool doNotCompactMessages = false)
    {
        var matches = _entriesMatcher.Matches(gitLogText.Trim());
        if (matches.Count == 0) return [];

        var entries = new List<GitLogEntry>();
        foreach (Match match in matches)
        {
            var entry = new GitLogEntry
            {
                AuthorDate = DateTime.Parse(match.Groups["AuthorDate"].Value),
                AuthorName = match.Groups["AuthorName"].Value,
                CommitDate = DateTime.Parse(match.Groups["CommitDate"].Value),
                CommitterName = match.Groups["CommitterName"].Value,
                Decorations = match.Groups["Decorations"].Value.Trim(' ', '(', ')'),
                Hash = new GitHash(match.Groups["Hash"].Value),
                MessageBody = match.Groups["MessageBody"].Value.Trim(),
                MessageSubject = match.Groups["MessageSubject"].Value.Trim()
            };

            if (entry.MessageBody.StartsWith(entry.MessageSubject)) entry.MessageBody = entry.MessageBody[entry.MessageSubject.Length..].Trim();

            while (entry.MessageBody.StartsWith("* " + entry.MessageSubject)) entry.MessageBody = entry.MessageBody[(entry.MessageSubject.Length + 2)..].Trim();

            var messageBodyLines = entry.MessageBody.Split('\n');
            if (messageBodyLines.Length >= 2 && entry.MessageSubject == $"{messageBodyLines[0].TrimStart('*', ' ')} {messageBodyLines[1]}")
            {
                entry.MessageBody = string.Join('\n', messageBodyLines.Skip(2)).Trim();
            }

            entry.MessageBody = doNotCompactMessages
                ? _threeOrMoreNewLinesMatcher.Replace(entry.MessageBody, "\n\n")
                : _twoOrMoreNewLinesMatcher.Replace(entry.MessageBody, "\n");

            var approvedByMatches = _approvedByMatcher.Matches(entry.MessageBody);
            if (approvedByMatches.Count > 0)
            {
                entry.Approvers = [.. approvedByMatches.Select(m => m.Groups["ApprovedBy"].Value.Trim()).Distinct()];
                entry.Approvers.ForEach(a => entry.MessageBody = entry.MessageBody.Replace($"Approved-by: {a}", "").Trim());
            }

            messageBodyLines = entry.MessageBody.Split('\n');
            while (messageBodyLines.Length >= 2 && "* " + messageBodyLines[0] == messageBodyLines[1])
            {
                messageBodyLines = [messageBodyLines[0], .. messageBodyLines.Skip(2)];
                entry.MessageBody = string.Join('\n', messageBodyLines).Trim();
            }

            entry.Message = $"{entry.MessageSubject}\n{(doNotCompactMessages ? "\n" : "")}{entry.MessageBody}";

            entry.ParentHashes = [.. match.Groups["ParentHashes"].Value.Split([' '], StringSplitOptions.RemoveEmptyEntries).Select(h => new GitHash(h))];

            var numStatsText = match.Groups["NumStats"].Value;
            var numStatLines = numStatsText.Split(['\n'], StringSplitOptions.RemoveEmptyEntries);
            var numStats = new List<GitLogEntryNumStat>();
            foreach (var line in numStatLines)
            {
                var parts = line.Split('\t');
                if (parts.Length == 3 &&
                    int.TryParse(parts[0], out int addedLines) &&
                    int.TryParse(parts[1], out int deletedLines))
                {
                    numStats.Add(new GitLogEntryNumStat
                    {
                        AddedLines = addedLines,
                        DeletedLines = deletedLines,
                        FilePath = parts[2]
                    });
                }
            }
            entry.NumStats = numStats;

            entry.NumStatsTotals = new GitLogNumStatsTotals
            {
                AddedLines = numStats.Sum(ns => ns.AddedLines),
                DeletedLines = numStats.Sum(ns => ns.DeletedLines),
                FilesChanged = numStats.Count
            };

            entries.Add(entry);
        }
        return entries;
    }
}

public class GitLogEntryNumStat
{
    public int AddedLines { get; set; }
    public int DeletedLines { get; set; }
    public string FilePath { get; set; } = null!;
}

public class GitLogNumStatsTotals
{
    public int AddedLines { get; set; }
    public int DeletedLines { get; set; }
    public int FilesChanged { get; set; }
}
