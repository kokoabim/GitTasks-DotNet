using System.Text.RegularExpressions;

namespace Kokoabim.GitTasks;

public class GitLogEntry
{
    #region properties
    public string[] Approvers { get; set; } = [];
    public DateTime AuthorDate { get; set; }
    public string AuthorEmail { get; set; }
    public string AuthorName { get; set; }
    public string? Branch { get; set; }
    public DateTime CommitDate { get; set; }
    public string CommitterName { get; set; }
    public string Decorations { get; set; }
    public GitHash Hash { get; set; }
    public string Message { get; set; }
    public string MessageBody { get; set; }
    public string MessageSubject { get; set; }
    public IReadOnlyCollection<GitLogEntryNumStat> NumStats { get; set; } = [];
    public GitLogNumStatsTotals NumStatsTotals { get; set; } = new();
    public IReadOnlyCollection<GitHash> ParentHashes { get; set; } = [];
    public string? Repository { get; set; }
    #endregion 

    private static readonly Regex _approvedByMatcher = new(@"^Approved-by: (?<ApprovedBy>.+)$", RegexOptions.Multiline);
    private static readonly Regex _entriesMatcher = new(@"AuthorDate=(?<AuthorDate>[^\n]*)\nAuthorName=(?<AuthorName>[^\n]*)\nAuthorEmail=(?<AuthorEmail>[^\n]*)\nCommitDate=(?<CommitDate>[^\n]*)\nCommitterName=(?<CommitterName>[^\n]*)\nDecorations=(?<Decorations>.*?)\nHash=(?<Hash>[0-9a-f]+)\nMessageBody=(?<MessageBody>.*?)\nMessageSubject=(?<MessageSubject>[^\n]*)\nParentHashes=(?<ParentHashes>[^\n]*)\nNumStats=\n(?<NumStats>(?:\d+\t\d+\t[^\n]+\n?)*)", RegexOptions.Singleline | RegexOptions.Multiline | RegexOptions.IgnoreCase);
    private static readonly Regex _threeOrMoreNewLinesMatcher = new(@"\n{3,}");
    private static readonly Regex _twoOrMoreNewLinesMatcher = new(@"\n{2,}");

    public GitLogEntry(
        DateTime authorDate,
        string authorEmail,
        string authorName,
        DateTime commitDate,
        string committerName,
        string decorations,
        GitHash hash,
        string messageBody,
        string messageSubject)
    {
        AuthorDate = authorDate;
        AuthorEmail = authorEmail;
        AuthorName = authorName;
        CommitDate = commitDate;
        CommitterName = committerName;
        Decorations = decorations;
        Hash = hash;
        MessageBody = messageBody ?? string.Empty;
        MessageSubject = messageSubject ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(MessageSubject) && !string.IsNullOrWhiteSpace(MessageBody)) Message = $"{MessageSubject}\n\n{MessageBody}";
        else if (!string.IsNullOrWhiteSpace(MessageSubject)) Message = MessageSubject;
        else Message = MessageBody;
    }

    public static IReadOnlyCollection<GitLogEntry> ParseMany(string gitLogText, bool doNotCompactMessages = false)
    {
        var matches = _entriesMatcher.Matches(gitLogText.Trim());
        if (matches.Count == 0) return [];

        var entries = new List<GitLogEntry>();
        foreach (Match match in matches)
        {
            var entry = new GitLogEntry(
                DateTime.Parse(match.Groups["AuthorDate"].Value),
                match.Groups["AuthorEmail"].Value,
                match.Groups["AuthorName"].Value,
                DateTime.Parse(match.Groups["CommitDate"].Value),
                match.Groups["CommitterName"].Value,
                match.Groups["Decorations"].Value.Trim(' ', '(', ')'),
                new GitHash(match.Groups["Hash"].Value),
                match.Groups["MessageBody"].Value.Trim(),
                match.Groups["MessageSubject"].Value.Trim()
            );

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
                    numStats.Add(new GitLogEntryNumStat(
                        addedLines,
                        deletedLines,
                        parts[2]
                    ));
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