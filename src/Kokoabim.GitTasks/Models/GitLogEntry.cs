using System.Text.RegularExpressions;

namespace Kokoabim.GitTasks;

public class GitLogEntry
{
    #region properties
    public string[] Approvers { get; set; } = [];
    public DateTime AuthorDate { get; }
    public string AuthorEmail { get; set; }
    public string AuthorName { get; set; }
    public string? Branch { get; set; }
    public DateTime CommitDate { get; }
    public string CommitterName { get; }
    public string? Decorations { get; }
    public GitHash Hash { get; }
    public bool IsMerge { get; }
    public string Message { get; private set; }
    public string MessageBody { get; private set; }
    public string MessageSubject { get; private set; }
    public IReadOnlyCollection<GitLogEntryNumStat> NumStats { get; private set; } = [];
    public GitLogNumStatsTotals NumStatsTotals { get; private set; } = new();
    public IReadOnlyCollection<GitHash> ParentHashes { get; private set; } = [];
    public string? Repository { get; set; }
    #endregion 

    private static readonly Regex _approvedByMatcher = new(@"^Approved-by: (?<ApprovedBy>.+)$", RegexOptions.Multiline);
    private static readonly Regex _entriesMatcher = new(@"AuthorDate=(?<AuthorDate>[^\n]*)\nAuthorName=(?<AuthorName>[^\n]*)\nAuthorEmail=(?<AuthorEmail>[^\n]*)\nCommitDate=(?<CommitDate>[^\n]*)\nCommitterName=(?<CommitterName>[^\n]*)\nDecorations=(?<Decorations>.*?)\nHash=(?<Hash>[0-9a-f]+)\nMessageBody=(?<MessageBody>.*?)\nMessageSubject=(?<MessageSubject>[^\n]*)\nParentHashes=(?<ParentHashes>[^\n]*)\nNumStats=\n(?<NumStats>(?:[\d-]+[ \t]+[\d-]+[ \t]+[^\n]+\n?)*)", RegexOptions.Singleline | RegexOptions.Multiline | RegexOptions.IgnoreCase);
    private static readonly Regex _threeOrMoreNewLinesMatcher = new(@"\n{3,}");
    private static readonly Regex _twoOrMoreNewLinesMatcher = new(@"\n{2,}");
    private static readonly Regex _numStatLineMatcher = new(@"^(?<AddedLines>\d+|-)\s+(?<DeletedLines>\d+|-)\s+((?<FileMove>(?<PathPrefix>.+?)?\{(?<PreviousPath>.*?)[\s]+=>[\s]+(?<NewPath>.+?)\}(?<PathSuffix>.+?)?)?|(?<FilePath>.+)?)$", RegexOptions.Compiled);

    private GitLogEntry(
        DateTime authorDate,
        string authorEmail,
        string authorName,
        DateTime commitDate,
        string committerName,
        string decorations,
        GitHash hash,
        string messageBody,
        string messageSubject,
        IEnumerable<GitHash> parentHashes)
    {
        AuthorDate = authorDate;
        AuthorEmail = authorEmail.ToLower();
        AuthorName = authorName;
        CommitDate = commitDate;
        CommitterName = committerName;
        Decorations = string.IsNullOrWhiteSpace(decorations) ? null : decorations;
        Hash = hash;
        MessageBody = messageBody ?? string.Empty;
        MessageSubject = messageSubject ?? string.Empty;
        ParentHashes = [.. parentHashes];

        if (!string.IsNullOrWhiteSpace(MessageSubject)) Message = !string.IsNullOrWhiteSpace(MessageBody) ? $"{MessageSubject}\n\n{MessageBody}" : MessageSubject;
        else Message = MessageBody;

        IsMerge = ParentHashes.Count > 1;
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
                match.Groups["MessageSubject"].Value.Trim(),
                match.Groups["ParentHashes"].Value.Split([' '], StringSplitOptions.RemoveEmptyEntries).Select(h => new GitHash(h))
            );

            CleanUpMessageProperties(entry, doNotCompactMessages);
            SetNumStatsProperties(entry, match.Groups["NumStats"].Value);

            entries.Add(entry);
        }
        return entries;
    }

    private static void CleanUpMessageProperties(GitLogEntry entry, bool doNotCompactMessages)
    {
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
    }

    private static void SetNumStatsProperties(GitLogEntry entry, string numStatsText)
    {
        var numStatLines = numStatsText.Split(['\n'], StringSplitOptions.RemoveEmptyEntries);
        var numStats = new List<GitLogEntryNumStat>();
        foreach (var line in numStatLines)
        {
            var match = _numStatLineMatcher.Match(line);
            if (!match.Success) continue;

            var fileMoved = match.Groups["FileMove"].Success;
            string? previousFilePath = null;

            var filePath = match.Groups["FilePath"].Success
                ? match.Groups["FilePath"].Value
                : fileMoved
                    ? match.Groups["NewPath"].Value
                    : null;

            if (fileMoved && (match.Groups["PathPrefix"].Success || match.Groups["PathSuffix"].Success))
            {
                var pathPrefix = match.Groups["PathPrefix"].Value;
                var pathSuffix = match.Groups["PathSuffix"].Value;
                filePath = $"{pathPrefix}{filePath}{pathSuffix}".Replace("//", "/").Replace(@"\\", @"\");
                previousFilePath = $"{pathPrefix}{match.Groups["PreviousPath"].Value}{pathSuffix}".Replace("//", "/").Replace(@"\\", @"\");
            }

            if (filePath == null) continue;

            int addedLines, deletedLines = 0;
            var isBinary = !(int.TryParse(match.Groups["AddedLines"].Value, out addedLines) && int.TryParse(match.Groups["DeletedLines"].Value, out deletedLines)); // will be "-" if binary file

            numStats.Add(new GitLogEntryNumStat(filePath, fileMoved ? previousFilePath : null)
            {
                AddedLines = isBinary ? 0 : addedLines,
                DeletedLines = isBinary ? 0 : deletedLines,
                IsBinaryFile = isBinary
            });
        }
        entry.NumStats = numStats;

        entry.NumStatsTotals = new GitLogNumStatsTotals
        {
            AddedLines = numStats.Sum(ns => ns.AddedLines),
            DeletedLines = numStats.Sum(ns => ns.DeletedLines),
            FilesChanged = numStats.Count
        };
    }
}