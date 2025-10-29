using System.Text.RegularExpressions;

namespace Kokoabim.GitTasks;

public static class ConsoleOutput
{
    private static readonly Regex _alreadyOnBranchRegex = new(@"^already on '(?<branch>[^']+)'", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly string _doubleLineDash = new('=', 60);
    private static readonly Lock _positionLock = new();
    private static readonly Regex _resetCommitRegex = new(@" is now at (?<commit>[0-9a-f]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly string _singleLineDash = new('—', 60);
    private static readonly Regex _switchedToBranchRegex = new(@"^switched to( a new)? branch '(?<branch>[^']+)'", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    #region methods

    public static void ClearHeaderDynamicallyActivity(GitRepository repository, bool resetPosition = false)
    {
        lock (_positionLock)
        {
            Console.SetCursorPosition(repository.ConsolePosition.Left, repository.ConsolePosition.Top);
            WriteNormal("    ");
            if (resetPosition) Console.SetCursorPosition(repository.ConsolePosition.Left, repository.ConsolePosition.Top);
        }
    }

    public static void WriteBlue(string text, bool newline = false)
    {
        Console.ForegroundColor = ConsoleColor.Blue;
        if (newline) Console.WriteLine(text);
        else Console.Write(text);
        Console.ResetColor();
    }

    public static void WriteBold(string text, bool newline = false)
    {
        if (newline) Console.WriteLine($"\x1b[1m{text}\x1b[0m");
        else Console.Write($"\x1b[1m{text}\x1b[0m");
    }

    public static void WriteGreen(string text, bool newline = false)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        if (newline) Console.WriteLine(text);
        else Console.Write(text);
        Console.ResetColor();
    }

    public static void WriteHeaderCheckout(GitRepository repository, bool dynamically)
    {
        if (dynamically) ClearHeaderDynamicallyActivity(repository);

        if (repository.Results.Checkout is null) return;

        var output = repository.Results.Checkout.Output ?? "";
        var hasError = !repository.Results.Checkout.Success;
        var didSwitch = false;

        string message;
        if (output.Contains("already on", StringComparison.OrdinalIgnoreCase))
        {
            var branch = _alreadyOnBranchRegex.Match(output) is { } m && m.Success ? m.Groups["branch"].Value : null;
            if (branch is not null) message = $"already on {branch}";
            else message = "already on branch";
        }
        else if (output.Contains("switched to branch", StringComparison.OrdinalIgnoreCase))
        {
            var branch = _switchedToBranchRegex.Match(output) is { } m && m.Success ? m.Groups["branch"].Value : null;
            if (branch is not null) message = $"switched to {branch}";
            else message = "switched";
            didSwitch = true;
        }
        else if (output.Contains("switched to a new branch", StringComparison.OrdinalIgnoreCase))
        {
            var branch = _switchedToBranchRegex.Match(output) is { } m && m.Success ? m.Groups["branch"].Value : null;
            if (branch is not null) message = $"switched to new branch {branch}";
            else message = "switched to new branch";
            didSwitch = true;
        }
        else if (output.Contains("would be overwritten", StringComparison.OrdinalIgnoreCase)) { message = "local changes would be overwritten"; hasError = true; }
        else if (output.Contains("permission denied", StringComparison.OrdinalIgnoreCase)) { message = "permission denied"; hasError = true; }
        else if (output.Contains("did not match", StringComparison.OrdinalIgnoreCase)) { message = "no match"; hasError = true; }
        else if (output.Contains("not a symbolic ref", StringComparison.OrdinalIgnoreCase)) { message = "not a symbolic ref (use fix-ref to fix)"; hasError = true; }
        else if (output.Contains("error: ", StringComparison.OrdinalIgnoreCase)) { message = "error"; hasError = true; }
        else if (output.Contains("fatal: ", StringComparison.OrdinalIgnoreCase)) { message = "fatal"; hasError = true; }
        else { message = "unknown"; hasError = true; }

        lock (_positionLock)
        {
            if (dynamically) Console.SetCursorPosition(repository.ConsolePosition.Left, repository.ConsolePosition.Top);

            WriteLight(" ");
            if (hasError) WriteRed(message);
            else if (didSwitch) WriteGreen(message);
            else WriteLight(message);
        }

        repository.ConsolePosition.Left += message.Length + 1;
    }

    public static void WriteHeaderClean(GitRepository repository, bool dynamically)
    {
        if (dynamically) ClearHeaderDynamicallyActivity(repository);

        if (repository.Results.Clean is null) return;

        var output = repository.Results.Clean.Output ?? "";
        var hasError = !repository.Results.Clean.Success;
        var didClean = false;

        string message;
        if (!hasError && string.IsNullOrWhiteSpace(output)) { message = "nothing to clean"; }
        else if (!hasError)
        {
            var files = 0;
            var dirs = 0;
            var lines = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line.StartsWith("Removing ", StringComparison.OrdinalIgnoreCase))
                {
                    if (line.EndsWith('/')) dirs++;
                    else files++;
                }
            }

            message = "cleaned";
            if (dirs > 0) message += $" {dirs} dir{(dirs == 1 ? "" : "s")}";
            if (files > 0) message += $" {files} file{(files == 1 ? "" : "s")}";

            didClean = true;
        }
        else if (output.Contains("error: ", StringComparison.OrdinalIgnoreCase)) { message = "error"; hasError = true; }
        else if (output.Contains("fatal: ", StringComparison.OrdinalIgnoreCase)) { message = "fatal"; hasError = true; }
        else { message = "unknown"; hasError = true; }

        lock (_positionLock)
        {
            if (dynamically) Console.SetCursorPosition(repository.ConsolePosition.Left, repository.ConsolePosition.Top);

            WriteLight(" ");
            if (hasError) WriteRed(message);
            else if (didClean) WriteGreen(message);
            else WriteLight(message);
        }

        repository.ConsolePosition.Left += message.Length + 1;
    }

    public static void WriteHeaderCommitPosition(GitRepository repository, bool dynamically)
    {
        if (dynamically) ClearHeaderDynamicallyActivity(repository);

        if (repository.Results.CommitPosition is null) return;

        var hasError = !repository.Results.CommitPosition.Success;
        var hasPositions = false;
        var commitPosition = repository.Results.CommitPosition.Value;

        var output = repository.Results.CommitPosition.Output ?? "";
        string? aheadBy = null;
        string? behindBy = null;
        string? errorMessage = null;
        if (!hasError && commitPosition is not null)
        {
            hasPositions = commitPosition.AheadBy > 0 || commitPosition.BehindBy > 0;
            if (commitPosition.AheadBy > 0) aheadBy = $" ↑{commitPosition.AheadBy}";
            if (commitPosition.BehindBy > 0) behindBy = $" ↓{commitPosition.BehindBy}";
        }
        else
        {
            hasError = true;
            if (output.Contains(" unknown revision ", StringComparison.OrdinalIgnoreCase)) errorMessage = "no remote";
            else if (output.Contains("error: ", StringComparison.OrdinalIgnoreCase) || output.Contains("fatal: ", StringComparison.OrdinalIgnoreCase)) errorMessage = output.ReplaceLineEndings().Replace('\n', ' ').Trim();
        }

        lock (_positionLock)
        {
            if (dynamically) Console.SetCursorPosition(repository.ConsolePosition.Left, repository.ConsolePosition.Top);

            if (hasError)
            {
                WriteRed(" position error");
                repository.ConsolePosition.Left += 15;
                if (errorMessage is not null)
                {
                    WriteLight($" ({errorMessage})");
                    repository.ConsolePosition.Left += errorMessage.Length + 3;
                }
            }
            else if (hasPositions)
            {
                if (aheadBy is not null) { WriteGreen(aheadBy); repository.ConsolePosition.Left += aheadBy.Length; }
                if (behindBy is not null) { WriteYellow(behindBy); repository.ConsolePosition.Left += behindBy.Length; }
            }
        }
    }

    public static void WriteHeaderDynamicallyActivity(GitRepository repository, bool doNotSetPosition = false)
    {
        lock (_positionLock)
        {
            if (!doNotSetPosition) Console.SetCursorPosition(repository.ConsolePosition.Left, repository.ConsolePosition.Top);
            WriteLight(" ...");
        }
    }

    public static void WriteHeaderDynamicallyStatus(GitRepository repository, bool withActivity = false)
    {
        ClearHeaderDynamicallyActivity(repository);

        if (repository.Results.Status is null) return;

        var output = repository.Results.Status.Output;
        var hasError = !repository.Results.Status.Success;
        var hasChanges = false;

        string message;
        if (!hasError)
        {
            if (string.IsNullOrWhiteSpace(output)) message = " clean";
            else { message = " local changes"; hasChanges = true; }
        }
        else message = " status error";

        lock (_positionLock)
        {
            Console.SetCursorPosition(repository.ConsolePosition.Left, repository.ConsolePosition.Top);

            if (hasError) WriteRed(message);
            else if (hasChanges) WriteYellow(message);
            else WriteGreen(message);
        }

        repository.ConsolePosition.Left += message.Length;

        if (withActivity) WriteHeaderDynamicallyActivity(repository);
    }

    public static void WriteHeaderMatchBranch(GitRepository r, GitBranch[]? matchingBranches, bool dynamically)
    {
        if (dynamically) ClearHeaderDynamicallyActivity(r);

        string? message = null;
        string? branches = null;
        if (matchingBranches is null) message = " failed to get branches";
        else if (matchingBranches.Length == 0) message = " no matches";
        else if (matchingBranches.Length > 1)
        {
            message = " multiple matches ";
            branches = string.Join(", ", matchingBranches.Select(static b => b.Name));
        }

        lock (_positionLock)
        {
            if (dynamically) Console.SetCursorPosition(r.ConsolePosition.Left, r.ConsolePosition.Top);

            if (message is not null) WriteRed(message);
            if (branches is not null) WriteLight(branches);
        }

        r.ConsolePosition.Left += (message?.Length ?? 0) + (branches?.Length ?? 0);
    }

    public static void WriteHeaderPull(GitRepository repository, bool dynamically)
    {
        if (dynamically) ClearHeaderDynamicallyActivity(repository);

        if (repository.Results.Pull is null) return;

        var output = repository.Results.Pull.Output ?? "";
        var hasError = !repository.Results.Pull.Success;
        var hasChanges = false;

        string message;
        if (output.Contains("already up to date", StringComparison.OrdinalIgnoreCase)) message = "up to date";
        else if (output.Contains("CONFLICT ") || output.Contains("merge conflict", StringComparison.OrdinalIgnoreCase)) { message = "conflict"; hasError = true; }
        else if (output.Contains("divergent branches", StringComparison.OrdinalIgnoreCase)) { message = "divergent branches"; hasError = true; }
        else if (output.Contains("permission denied", StringComparison.OrdinalIgnoreCase)) { message = "permission denied"; hasError = true; }
        else if (output.Contains("would be overwritten", StringComparison.OrdinalIgnoreCase)) { message = "local changes would be overwritten"; hasError = true; }
        else if (output.Contains("no tracking information for the current branch", StringComparison.OrdinalIgnoreCase)) { message = "no tracking information"; hasError = true; }
        else if (output.Contains("no such ref was fetched", StringComparison.OrdinalIgnoreCase)) { message = "no remote"; hasError = true; }
        else if (output.Contains("Aborting")) { message = "aborted"; hasError = true; }
        else if (output.Contains("error: ", StringComparison.OrdinalIgnoreCase)) { message = "error"; hasError = true; }
        else if (output.Contains("fatal: ", StringComparison.OrdinalIgnoreCase)) { message = "fatal"; hasError = true; }
        else if (!hasError &&
            (output.Contains("fast-forward", StringComparison.OrdinalIgnoreCase)
            || output.Contains("merge made", StringComparison.OrdinalIgnoreCase)
            || output.Contains("applying: ", StringComparison.OrdinalIgnoreCase))) { message = "pulled"; hasChanges = true; }
        else { message = "unknown"; hasError = true; }

        lock (_positionLock)
        {
            if (dynamically) Console.SetCursorPosition(repository.ConsolePosition.Left, repository.ConsolePosition.Top);

            WriteLight(" ");
            if (hasError) WriteRed(message);
            else if (hasChanges) WriteGreen(message);
            else WriteLight(message);
        }

        repository.ConsolePosition.Left += message.Length + 1;
    }

    public static void WriteHeaderRepository(ExecuteResult<GitRepository> repositoryExecResult, bool withStatus = false, bool newline = true, bool withActivity = false)
    {
        if (!repositoryExecResult.Success)
        {
            WriteNormal(repositoryExecResult.Reference?.ToString() ?? "Unknown repository");
            WriteRed(" error");
            WriteLight($" ({repositoryExecResult})", newline: newline);

            // Object is null here so we can't update repo.ConsolePosition.Left
            return;
        }

        var repo = repositoryExecResult.Value!;

        WriteNormal(repo.NameAndRelativePath);
        repo.ConsolePosition.Left += repo.NameAndRelativePath.Length;

        if (repo.CurrentBranch is not null)
        {
            if (repo.CurrentBranch == repo.DefaultBranch || repo.DefaultBranch is null) WriteLight($" {repo.CurrentBranch}");
            else WriteBlue($" {repo.CurrentBranch}");

            repo.ConsolePosition.Left += repo.CurrentBranch.Length + 1;
        }

        if (!repo.Success)
        {
            WriteRed(" error");
            WriteLight($" ({repo.Error})", newline: false);

            repo.ConsolePosition.Left += 6 + repo.Error.Length + 3;
        }

        if (withStatus && repo.Results.Status is not null)
        {
            var output = repo.Results.Status.Output ?? "";
            var hasError = !repo.Results.Status.Success;
            var hasChanges = false;

            string message;
            if (hasError) { message = " status error"; }
            else if (string.IsNullOrWhiteSpace(output)) message = " clean";
            else { message = " local changes"; hasChanges = true; }

            if (hasError) WriteRed(message);
            else if (hasChanges) WriteYellow(message);
            else WriteGreen(message);

            repo.ConsolePosition.Left += message.Length;
        }

        if (withActivity) WriteHeaderDynamicallyActivity(repo, doNotSetPosition: newline);

        if (newline) Console.WriteLine();
    }

    public static void WriteHeaderReset(GitRepository repo, bool dynamically)
    {
        if (dynamically) ClearHeaderDynamicallyActivity(repo);

        if (repo.Results.Reset is null) return;

        var output = repo.Results.Reset.Output ?? "";
        var hasError = !repo.Results.Reset.Success;
        var didReset = false;

        string message;
        if (!hasError && output == "") { message = "unchanged"; }
        else if (!hasError && output.Contains(" is now at ", StringComparison.OrdinalIgnoreCase))
        {
            var commitHash = _resetCommitRegex.Match(output) is { } m && m.Success ? m.Groups["commit"].Value : null;
            if (commitHash is not null) message = $"reset to {commitHash}";
            else message = "reset";

            didReset = true;
        }
        else if (!hasError && output.Contains("Unstaged changes after reset", StringComparison.OrdinalIgnoreCase)) { message = "reset with changes"; didReset = true; }
        else if (output.Contains(" unknown revision or path not ", StringComparison.OrdinalIgnoreCase)) { message = "no such commit"; hasError = true; }
        else if (output.Contains("error: ", StringComparison.OrdinalIgnoreCase)) { message = "error"; hasError = true; }
        else if (output.Contains("fatal: ", StringComparison.OrdinalIgnoreCase)) { message = "fatal"; hasError = true; }
        else { message = "unknown"; hasError = true; }

        lock (_positionLock)
        {
            if (dynamically) Console.SetCursorPosition(repo.ConsolePosition.Left, repo.ConsolePosition.Top);

            WriteLight(" ");
            if (hasError) WriteRed(message);
            else if (didReset) WriteGreen(message);
            else WriteLight(message);
        }

        repo.ConsolePosition.Left += message.Length + 1;
    }

    public static void WriteHeaderSetHead(GitRepository repo, bool dynamically)
    {
        if (dynamically) ClearHeaderDynamicallyActivity(repo);

        if (repo.Results.SetHead is null) return;

        var output = repo.Results.SetHead.Output ?? "";
        var hasError = !repo.Results.SetHead.Success;
        var didSet = false;

        string message;
        if (output.Contains("HEAD is now at", StringComparison.OrdinalIgnoreCase)) { message = "set HEAD"; didSet = true; }
        else if (output.Contains(" is now created ", StringComparison.OrdinalIgnoreCase)) { message = "set HEAD"; didSet = true; }
        else if (output.Contains(" is unchanged ", StringComparison.OrdinalIgnoreCase)) { message = "unchanged"; }
        else if (output.Contains("error: ", StringComparison.OrdinalIgnoreCase)) { message = "error"; hasError = true; }
        else if (output.Contains("fatal: ", StringComparison.OrdinalIgnoreCase)) { message = "fatal"; hasError = true; }
        else { message = "unknown"; hasError = true; }

        lock (_positionLock)
        {
            if (dynamically) Console.SetCursorPosition(repo.ConsolePosition.Left, repo.ConsolePosition.Top);

            WriteLight(" ");
            if (hasError) WriteRed(message);
            else if (didSet) WriteGreen(message);
            else WriteLight(message);
        }

        repo.ConsolePosition.Left += message.Length + 1;
    }

    public static int WriteHeadersRepositories(ExecuteResult<GitRepository>[] repositoryExecResults, bool withActivity = false)
    {
        var offset = Console.CursorTop + repositoryExecResults.Length < Console.WindowHeight
            ? Console.CursorTop
            : Console.WindowHeight - repositoryExecResults.Length - 1;

        for (int i = 0; i < repositoryExecResults.Length; i++)
        {
            var repoExecResult = repositoryExecResults[i];
            if (repoExecResult.Value is not null) repoExecResult.Value.ConsolePosition.Top = i + offset;
        }

        foreach (var repoExecResult in repositoryExecResults) WriteHeaderRepository(repoExecResult, withStatus: false, newline: true, withActivity: withActivity);

        return Console.CursorTop;
    }

    public static void WriteItalic(string text) => Console.Write($"\x1b[3m{text}\x1b[0m");

    public static void WriteLight(string text, bool newline = false)
    {
        if (newline) Console.WriteLine($"\x1b[2m{text}\x1b[0m");
        else Console.Write($"\x1b[2m{text}\x1b[0m");
    }

    public static void WriteLogEntries(GitRepository repo, GitLogSettings gitLogSettings, GitLogEntry[] logEntries)
    {
        int longestAddedLinesLength;
        int longestDeletedLinesLength;

        var lastIndex = logEntries.Length - 1;
        for (int i = 0; i < logEntries.Length; i++)
        {
            var entry = logEntries[i];

            WriteYellow(entry.Hash.Abbreviated);
            if (entry.IsMerge) WriteNormal(" 🔀");
            WriteBold($" {entry.AuthorName}");
            WriteLight($" {entry.AuthorEmail}");
            if (entry.AuthorName != entry.CommitterName) WriteLight($" ({entry.CommitterName})");
            WriteLight(" @");
            WriteNormal($" {entry.AuthorDate:M/d/yy h:mm tt}");
            if (entry.AuthorDate != entry.CommitDate) WriteLight($" ({entry.CommitDate:M/d/yy h:mm tt})");
            WriteLight(" •");
            WriteNormal($" {entry.NumStatsTotals.FilesChanged} file{(entry.NumStatsTotals.FilesChanged == 1 ? "" : "s")}");
            WriteGreen($" +{entry.NumStatsTotals.AddedLines}");
            WriteRed($" -{entry.NumStatsTotals.DeletedLines}", newline: true);

            WriteLight(new string?[]
                {
                    entry.Repository,
                    entry.Branch,
                    entry.Decorations,
                    string.Join(", ", entry.ParentHashes.Select(static h => h.Abbreviated)),
                    entry.Approvers.Length > 0 ? string.Join(", ", entry.Approvers) : null
                }.CombineNonNullOrWhiteSpace(" • ")!,
                newline: true);
            Console.WriteLine();

            WriteBold(entry.MessageSubject, newline: true);
            if (!gitLogSettings.SubjectOnly && !string.IsNullOrWhiteSpace(entry.MessageBody))
            {
                if (gitLogSettings.DoNotCompactMessages) Console.WriteLine();
                WriteNormal(entry.MessageBody, newline: true);
            }
            Console.WriteLine();

            if (gitLogSettings.ListFiles && entry.NumStatsTotals.FilesChanged > 0)
            {
                var longestFilePathLength = entry.NumStats.Max(static ns => ns.FilePath.Length);
                longestAddedLinesLength = entry.NumStats.Max(static ns => ns.AddedLines.ToString().Length) + 2;
                longestDeletedLinesLength = entry.NumStats.Max(static ns => ns.DeletedLines.ToString().Length + 2);

                foreach (var numStat in entry.NumStats)
                {
                    WriteLight(numStat.FilePath.PadRight(longestFilePathLength));
                    if (!numStat.IsBinaryFile)
                    {
                        WriteGreen($"+{numStat.AddedLines}".PadLeft(longestAddedLinesLength));
                        WriteRed($"-{numStat.DeletedLines}".PadLeft(longestDeletedLinesLength));
                    }
                    else WriteLight(" (binary file)");

                    if (numStat.PreviousFilePath is not null) WriteLight($" (from {numStat.PreviousFilePath})");

                    Console.WriteLine();
                }

                Console.WriteLine();
            }

            if (i < lastIndex)
            {
                WriteLight(_singleLineDash, newline: true);
                Console.WriteLine();
            }
        }

        var minDate = logEntries.Min(static e => e.AuthorDate);
        var maxDate = logEntries.Max(static e => e.AuthorDate);
        var totalDays = (maxDate - minDate).TotalDays + 1;

        WriteLight(_doubleLineDash, newline: true);
        WriteLight($"Repository: {repo.NameAndRelativePath}", newline: true);
        WriteLight($"Dates: {minDate:ddd M/d/yy h:mm tt} – {maxDate:ddd M/d/yy h:mm tt} ({totalDays:N0} days)", newline: true);
        if (!gitLogSettings.IncludeMerges) WriteLight("Excluding merge commits", newline: true);
        if (gitLogSettings.MergesOnly) WriteLight("Including only merge commits", newline: true);
        if (gitLogSettings.DoNotIncludeAll) WriteLight("Excluding commits that are in all branches", newline: true);
        if (gitLogSettings.BranchPattern is not null) WriteLight($"Branch pattern: {gitLogSettings.BranchPattern}", newline: true);
        if (gitLogSettings.FilePattern is not null) WriteLight($"File pattern: {gitLogSettings.FilePattern}", newline: true);
        if (gitLogSettings.AuthorName is not null) WriteLight($"Author pattern: {gitLogSettings.AuthorName}", newline: true);
        Console.WriteLine();

        var groupedByAuthor = logEntries.GroupBy(static e => e.AuthorEmail.ToLower())
            .OrderByDescending(static g => g.Count(le => !le.IsMerge))
            .ThenByDescending(static g => g.Count(le => le.IsMerge))
            .ToArray();

        // var longestAuthorLength = groupedByAuthor.Max(static g => g.First().AuthorName.Length + g.First().AuthorEmail.Length + 1);
        var longestAuthorLength = groupedByAuthor.Max(static g => g.First().AuthorName.Length);
        var longestMergeCountLength = groupedByAuthor.Max(static g => g.Count(le => le.IsMerge).ToString().Length) + 2;
        var longestCommitCountLength = groupedByAuthor.Max(static g => g.Count(le => !le.IsMerge).ToString().Length) + 2;
        var longestFilesChangedLength = groupedByAuthor.Max(static g => g.Where(le => !le.IsMerge).Sum(static e => e.NumStatsTotals.FilesChanged).ToString().Length) + 2;
        longestAddedLinesLength = groupedByAuthor.Max(static g => g.Where(le => !le.IsMerge).Sum(static e => e.NumStatsTotals.AddedLines).ToString().Length) + 3;
        longestDeletedLinesLength = groupedByAuthor.Max(static g => g.Where(le => !le.IsMerge).Sum(static e => e.NumStatsTotals.DeletedLines).ToString().Length) + 3;

        var maxLineLength = 0;
        for (int i = 0; i < groupedByAuthor.Length; i++)
        {
            var authorGroup = groupedByAuthor[i];

            var authorName = authorGroup.First().AuthorName;
            var authorEmail = authorGroup.First().AuthorEmail;
            var mergeCount = authorGroup.Count(le => le.IsMerge);
            var commitCount = authorGroup.Count(le => !le.IsMerge);
            var filesChanged = authorGroup.Where(le => !le.IsMerge).Sum(static e => e.NumStatsTotals.FilesChanged);
            var addedLines = authorGroup.Where(le => !le.IsMerge).Sum(static e => e.NumStatsTotals.AddedLines);
            var deletedLines = authorGroup.Where(le => !le.IsMerge).Sum(static e => e.NumStatsTotals.DeletedLines);

            // WriteBold(authorName);
            // WriteLight($" {authorEmail}".PadRight(longestAuthorLength - authorName.Length));
            WriteBold(authorName.PadRight(longestAuthorLength));
            WriteNormal($"{mergeCount.ToString().PadLeft(longestMergeCountLength)} merge{(mergeCount == 1 ? "" : "s"),-1}");
            WriteNormal($"{commitCount.ToString().PadLeft(longestCommitCountLength)} commit{(commitCount == 1 ? "" : "s"),-1}");
            WriteNormal($"{filesChanged.ToString().PadLeft(longestFilesChangedLength)} file{(filesChanged == 1 ? "" : "s"),-1}");
            WriteGreen($"+{addedLines}".PadLeft(longestAddedLinesLength));
            WriteRed($"-{deletedLines}".PadLeft(longestDeletedLinesLength));

            if (Console.CursorLeft > maxLineLength) maxLineLength = Console.CursorLeft;
            Console.WriteLine();
        }

        var authorTotalLength = groupedByAuthor.Length.ToString().Length;

        if (logEntries.Length > 1)
        {
            var totalMerges = logEntries.Count(le => le.IsMerge);
            var totalCommits = logEntries.Count(le => !le.IsMerge);
            var totalFilesChanged = logEntries.Where(le => !le.IsMerge).Sum(static e => e.NumStatsTotals.FilesChanged);
            var totalAddedLines = logEntries.Where(le => !le.IsMerge).Sum(static e => e.NumStatsTotals.AddedLines);
            var totalDeletedLines = logEntries.Where(le => !le.IsMerge).Sum(static e => e.NumStatsTotals.DeletedLines);

            WriteLight(new string('-', maxLineLength), newline: true);

            WriteNormal(groupedByAuthor.Length.ToString());
            WriteNormal(totalMerges.ToString().PadLeft(longestAuthorLength - authorTotalLength + longestMergeCountLength)); // " merge(s)"
            WriteNormal(totalCommits.ToString().PadLeft(longestCommitCountLength + 7)); // " commit(s)"
            WriteNormal(totalFilesChanged.ToString().PadLeft(longestFilesChangedLength + 8)); // " file(s)"
            WriteGreen($"+{totalAddedLines}".PadLeft(longestAddedLinesLength + 6)); // "+<num>"
            WriteRed($"-{totalDeletedLines}".PadLeft(longestDeletedLinesLength));
        }

        Console.WriteLine();
    }

    public static void WriteNormal(string text, bool newline = false)
    {
        Console.ResetColor();
        if (newline) Console.WriteLine($"\x1b[0m{text}");
        else Console.Write($"\x1b[0m{text}");
    }

    public static void WriteRed(string text, bool newline = false)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        if (newline) Console.WriteLine(text);
        else Console.Write(text);
        Console.ResetColor();
    }

    public static void WriteRedAndLight(string redText, string lightText, bool newline = false)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write(redText);
        Console.ResetColor();

        WriteLight(lightText, newline);
    }

    public static void WriteRepositoryAuthorStats(GitLogSettings gitLogSettings, Dictionary<string, GitRepositoryAuthorStats[]> repoAuthorStats)
    {
        var allStats = repoAuthorStats.Values.SelectMany(static e => e).ToArray();

        var minDate = allStats.Min(static e => e.Dates.FromDate);
        var maxDate = allStats.Max(static e => e.Dates.ToDate);
        var totalDays = (maxDate - minDate).TotalDays + 1;

        WriteLight(new string('=', 72), true);
        WriteLight("Combined Repository Totals", true);
        WriteLight($"Dates: {minDate:ddd M/d/yy h:mm tt} – {maxDate:ddd M/d/yy h:mm tt} ({totalDays:N0} days)", true);
        if (!gitLogSettings.IncludeMerges) WriteLight("Excluding merge commits", true);
        if (gitLogSettings.MergesOnly) WriteLight("Including only merge commits", true);
        if (gitLogSettings.DoNotIncludeAll) WriteLight("Excluding commits that are in all branches", true);
        if (gitLogSettings.BranchPattern is not null) WriteLight($"Branch pattern: {gitLogSettings.BranchPattern}", true);
        if (gitLogSettings.FilePattern is not null) WriteLight($"File pattern: {gitLogSettings.FilePattern}", true);
        if (gitLogSettings.AuthorName is not null) WriteLight($"Author pattern: {gitLogSettings.AuthorName}", true);
        Console.WriteLine();

        // var longestAuthorLength = repoAuthorStats.Max(static k => k.Value.First().AuthorEmail.Length + k.Value.First().AuthorName.Length) + 1;
        var longestAuthorLength = repoAuthorStats.Max(static k => k.Value.First().AuthorName.Length);
        var longestMergeCountLength = allStats.Max(static g => g.MergeCount.ToString().Length) + 2;
        var longestCommitCountLength = allStats.Max(static g => g.CommitCount.ToString().Length) + 2;
        var longestFilesChangedLength = allStats.Max(static g => g.FilesChanged.ToString().Length) + 2;
        var longestAddedLinesLength = allStats.Max(static g => g.AddedLines.ToString().Length) + 3;
        var longestDeletedLinesLength = allStats.Max(static g => g.DeletedLines.ToString().Length) + 3;
        var longestRepoCountLength = repoAuthorStats.Max(static k => k.Value.Length.ToString().Length) + 2;

        var maxLineLength = 0;
        foreach (var kvp in repoAuthorStats.OrderByDescending(static k => k.Value.Sum(static s => s.CommitCount)).ThenByDescending(static k => k.Value.Sum(static s => s.MergeCount)))
        {
            var stats = kvp.Value;
            var authorEmail = stats.First().AuthorEmail;
            var authorName = stats.First().AuthorName;

            var totalMerges = stats.Sum(static s => s.MergeCount);
            var totalFilesChanged = stats.Sum(static s => s.FilesChanged);
            var totalCommits = stats.Sum(static s => s.CommitCount);
            var totalAddedLines = stats.Sum(static s => s.AddedLines);
            var totalDeletedLines = stats.Sum(static s => s.DeletedLines);
            var totalRepos = stats.Length;

            // WriteBold(authorName);
            // WriteLight($" {authorEmail}".PadRight(longestAuthorLength - authorName.Length));
            WriteBold(authorName.PadRight(longestAuthorLength));
            WriteNormal($"{totalMerges.ToString().PadLeft(longestMergeCountLength)} merge{(totalMerges == 1 ? "" : "s"),-1}");
            WriteNormal($"{totalCommits.ToString().PadLeft(longestCommitCountLength)} commit{(totalCommits == 1 ? "" : "s"),-1}");
            WriteNormal($"{totalFilesChanged.ToString().PadLeft(longestFilesChangedLength)} file{(totalFilesChanged == 1 ? "" : "s"),-1}");
            WriteGreen($"+{totalAddedLines}".PadLeft(longestAddedLinesLength));
            WriteRed($"-{totalDeletedLines}".PadLeft(longestDeletedLinesLength));
            WriteNormal($"{totalRepos.ToString().PadLeft(longestRepoCountLength)} repo{(totalRepos == 1 ? "" : "s"),-1}");

            if (Console.CursorLeft > maxLineLength) maxLineLength = Console.CursorLeft;
            Console.WriteLine();
        }

        var authorTotalLength = repoAuthorStats.Count.ToString().Length;

        if (repoAuthorStats.Count > 1)
        {
            var grandTotalCommits = allStats.Sum(static s => s.CommitCount);
            var grandTotalMerges = allStats.Sum(static s => s.MergeCount);
            var grandTotalAddedLines = allStats.Sum(static s => s.AddedLines);
            var grandTotalDeletedLines = allStats.Sum(static s => s.DeletedLines);
            var grandTotalFilesChanged = allStats.Sum(static s => s.FilesChanged);
            var grandTotalRepos = allStats.Sum(static s => 1);

            WriteLight(new string('-', maxLineLength), newline: true);

            WriteNormal(repoAuthorStats.Count.ToString());
            WriteNormal(grandTotalMerges.ToString().PadLeft(longestAuthorLength - authorTotalLength + longestMergeCountLength)); // " merge(s)"
            WriteNormal(grandTotalCommits.ToString().PadLeft(longestCommitCountLength + 7)); // " commit(s)"
            WriteNormal(grandTotalFilesChanged.ToString().PadLeft(longestFilesChangedLength + 8)); // " file(s)"
            WriteGreen($"+{grandTotalAddedLines}".PadLeft(longestAddedLinesLength + 6)); // "+<num>"
            WriteRed($"-{grandTotalDeletedLines}".PadLeft(longestDeletedLinesLength));
            WriteNormal(grandTotalRepos.ToString().PadLeft(longestRepoCountLength + 1)); // " repo(s)"
        }

        Console.WriteLine();
    }

    public static void WriteUnderline(string text) => Console.Write($"\x1b[4m{text}\x1b[0m");

    public static void WriteYellow(string text, bool newline = false)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        if (newline) Console.WriteLine(text);
        else Console.Write(text);
        Console.ResetColor();
    }

    public static void WriteYellowAndLight(string yellowText, string lightText, bool newline = false)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write(yellowText);
        Console.ResetColor();

        WriteLight(lightText, newline);
    }

    #endregion 
}