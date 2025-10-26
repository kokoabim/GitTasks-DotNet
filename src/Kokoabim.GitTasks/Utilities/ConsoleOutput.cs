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
        var commitPosition = repository.Results.CommitPosition.Object;

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

    public static void WriteHeaderDynamicallyActivity(GitRepository repository)
    {
        lock (_positionLock)
        {
            Console.SetCursorPosition(repository.ConsolePosition.Left, repository.ConsolePosition.Top);
            WriteLight(" ...");
        }
    }

    public static void WriteHeaderDynamicallyStatus(GitRepository repository)
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
            branches = string.Join(", ", matchingBranches.Select(b => b.Name));
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

    public static void WriteHeaderRepository(ExecutorResult<GitRepository> repositoryExecResult, bool withStatus = false, bool newline = true)
    {
        if (!repositoryExecResult.Success)
        {
            WriteNormal(repositoryExecResult.Reference?.ToString() ?? "Unknown repository");
            WriteRed(" error");
            WriteLight($" ({repositoryExecResult})", newline: newline);

            // Object is null here so we can't update repo.ConsolePosition.Left
            return;
        }

        var repo = repositoryExecResult.Object!;

        WriteNormal(repo.RelativePath);
        repo.ConsolePosition.Left += repo.RelativePath.Length;

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

    public static int WriteHeadersRepositories(ExecutorResult<GitRepository>[] repositoryExecResults)
    {
        var offset = Console.CursorTop + repositoryExecResults.Length < Console.WindowHeight
            ? Console.CursorTop
            : Console.WindowHeight - repositoryExecResults.Length - 1;

        for (int i = 0; i < repositoryExecResults.Length; i++)
        {
            var repoExecResult = repositoryExecResults[i];
            if (repoExecResult.Object is not null) repoExecResult.Object.ConsolePosition.Top = i + offset;
        }

        foreach (var repoExecResult in repositoryExecResults) WriteHeaderRepository(repoExecResult, withStatus: false, newline: true);

        return Console.CursorTop;
    }

    public static void WriteItalic(string text) => Console.Write($"\x1b[3m{text}\x1b[0m");

    public static void WriteLight(string text, bool newline = false)
    {
        if (newline) Console.WriteLine($"\x1b[2m{text}\x1b[0m");
        else Console.Write($"\x1b[2m{text}\x1b[0m");
    }

    public static void WriteLogEntries(GitLogEntry[] logEntries, bool subjectOnly = false, bool doNotCompactMessages = false)
    {
        var lastIndex = logEntries.Length - 1;
        for (int i = 0; i < logEntries.Length; i++)
        {
            var entry = logEntries[i];

            WriteYellow(entry.Hash.Abbreviated);
            WriteBold($" {entry.AuthorName}");
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
                    string.Join(", ", entry.ParentHashes.Select(h => h.Abbreviated)),
                    entry.Approvers is not null && entry.Approvers.Length > 0 ? string.Join(", ", entry.Approvers) : null
                }.CombineNonNullOrWhiteSpace(" • ")!,
                newline: true);
            Console.WriteLine();

            WriteBold(entry.MessageSubject, newline: true);
            if (!subjectOnly && !string.IsNullOrWhiteSpace(entry.MessageBody))
            {
                if (doNotCompactMessages) Console.WriteLine();
                WriteNormal(entry.MessageBody, newline: true);
            }
            Console.WriteLine();

            if (i < lastIndex)
            {
                WriteLight(_singleLineDash, newline: true);
                Console.WriteLine();
            }
        }

        WriteLight(_doubleLineDash, newline: true);
        Console.WriteLine();

        var groupedByAuthor = logEntries.GroupBy(e => e.AuthorName.Replace(" ", "")).OrderByDescending(g => g.Count()).ToArray();
        var longestAuthorNameLength = groupedByAuthor.Max(g => g.First().AuthorName.Length);
        var longestCommitCountLength = groupedByAuthor.Max(g => g.Count().ToString().Length);
        var longestAddedLinesLength = groupedByAuthor.Max(g => g.Sum(e => e.NumStatsTotals.AddedLines).ToString().Length) + 3;
        var longestDeletedLinesLength = groupedByAuthor.Max(g => g.Sum(e => e.NumStatsTotals.DeletedLines).ToString().Length) + 3;
        var longestFilesChangedLength = groupedByAuthor.Max(g => g.Sum(e => e.NumStatsTotals.FilesChanged).ToString().Length);

        var maxLineLength = 0;
        for (int i = 0; i < groupedByAuthor.Length; i++)
        {
            var authorGroup = groupedByAuthor[i];

            var authorName = authorGroup.First().AuthorName;
            var commitCount = authorGroup.Count();
            var addedLines = authorGroup.Sum(e => e.NumStatsTotals.AddedLines);
            var deletedLines = authorGroup.Sum(e => e.NumStatsTotals.DeletedLines);
            var filesChanged = authorGroup.Sum(e => e.NumStatsTotals.FilesChanged);

            WriteBold(authorName.PadRight(longestAuthorNameLength));
            WriteNormal($"  {commitCount.ToString().PadLeft(longestCommitCountLength)} commit{(commitCount == 1 ? "" : "s"),-1}");
            WriteNormal($"  {filesChanged.ToString().PadLeft(longestFilesChangedLength)} file{(filesChanged == 1 ? "" : "s"),-1}");
            WriteGreen($"  +{addedLines}".PadLeft(longestAddedLinesLength));
            WriteRed($"  -{deletedLines}".PadLeft(longestDeletedLinesLength));

            if (Console.CursorLeft > maxLineLength) maxLineLength = Console.CursorLeft;
            Console.WriteLine();
        }

        if (logEntries.Length > 1)
        {
            WriteLight(new string('-', maxLineLength), newline: true);
            var totalCommits = logEntries.Length;
            var totalAddedLines = logEntries.Sum(e => e.NumStatsTotals.AddedLines);
            var totalDeletedLines = logEntries.Sum(e => e.NumStatsTotals.DeletedLines);
            var totalFilesChanged = logEntries.Sum(e => e.NumStatsTotals.FilesChanged);
            WriteNormal(groupedByAuthor.Count().ToString().PadRight(longestAuthorNameLength));
            WriteNormal(totalCommits.ToString().PadLeft(longestCommitCountLength + 2));
            WriteNormal(totalFilesChanged.ToString().PadLeft(longestFilesChangedLength + 10));
            WriteGreen($"+{totalAddedLines}".PadLeft(longestAddedLinesLength + 6));
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

public class ConsolePosition
{
    public int Left { get; set; }
    public int Top { get; set; }
}