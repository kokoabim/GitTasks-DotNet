namespace Kokoabim.GitTasks;

public static class ConsoleOutput
{
    private static readonly Lock _positionLock = new();

    #region methods

    public static void ClearHeaderDynamicallyActivity(GitRepository repository)
    {
        lock (_positionLock)
        {
            Console.SetCursorPosition(repository.ConsolePosition.Left, repository.ConsolePosition.Top);
            WriteNormal("    ");
        }
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

    public static void WriteHeaderDynamicallyCheckout(GitRepository repository)
    {
        if (repository.Results.Checkout is null) return;

        var output = repository.Results.Checkout.Output ?? "";
        var hasError = !repository.Results.Checkout.Success;
        var didSwitch = false;

        string message;
        if (output.Contains("already on", StringComparison.OrdinalIgnoreCase)) message = "already on branch";
        else if (output.Contains("switched to branch", StringComparison.OrdinalIgnoreCase)) { message = "switched"; didSwitch = true; }
        else if (output.Contains("switched to a new branch", StringComparison.OrdinalIgnoreCase)) { message = "switched to new branch"; didSwitch = true; }
        else if (output.Contains("did not match", StringComparison.OrdinalIgnoreCase)) { message = "no match"; hasError = true; }
        else if (output.Contains("not a symbolic ref", StringComparison.OrdinalIgnoreCase)) { message = "not a symbolic ref (use fix-ref to fix)"; hasError = true; }
        else if (output.Contains("error: ", StringComparison.OrdinalIgnoreCase)) { message = "error"; hasError = true; }
        else if (output.Contains("fatal: ", StringComparison.OrdinalIgnoreCase)) { message = "fatal"; hasError = true; }
        else { message = "unknown"; hasError = true; }

        lock (_positionLock)
        {
            Console.SetCursorPosition(repository.ConsolePosition.Left, repository.ConsolePosition.Top);

            WriteLight(" ");
            if (hasError) WriteRed(message);
            else if (didSwitch) WriteGreen(message);
            else WriteLight(message);
        }

        repository.ConsolePosition.Left += message.Length + 1;
    }

    public static void WriteHeaderDynamicallyStatus(GitRepository repository)
    {
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

    public static void WriteLight(string text, bool newline = false)
    {
        if (newline) Console.WriteLine($"\x1b[2m{text}\x1b[0m");
        else Console.Write($"\x1b[2m{text}\x1b[0m");
    }

    private static void WriteBlue(string text)
    {
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.Write(text);
        Console.ResetColor();
    }

    private static void WriteBold(string text) => Console.Write($"\x1b[1m{text}\x1b[0m");

    private static void WriteGreen(string text)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write(text);
        Console.ResetColor();
    }

    private static void WriteItalic(string text) => Console.Write($"\x1b[3m{text}\x1b[0m");

    private static void WriteNormal(string text)
    {
        Console.ResetColor();
        Console.Write($"\x1b[0m{text}");
    }

    private static void WriteRed(string text)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write(text);
        Console.ResetColor();
    }

    private static void WriteUnderline(string text) => Console.Write($"\x1b[4m{text}\x1b[0m");

    private static void WriteYellow(string text)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write(text);
        Console.ResetColor();
    }

    #endregion 
}

public class ConsolePosition
{
    public int Left { get; set; }
    public int Top { get; set; }
}