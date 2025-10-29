using Kokoabim.CommandLineInterface;

namespace Kokoabim.GitTasks;

public class GitLogSettings
{
    #region properties
    public string? AfterRelativeDate { get; set; }
    public string? AuthorName { get; set; }
    public string? BeforeRelativeDate { get; set; }
    public string? Branch { get; set; } // set externally
    public bool DoNotCompactMessages { get; set; }
    public string? BranchPattern { get; set; }
    public bool DoNotIncludeAll { get; set; }
    public string? FilePattern { get; set; }
    public bool IncludeMerges { get; set; }
    public bool ListFiles { get; set; }
    public bool MergesOnly { get; set; }
    public string? MessagePattern { get; set; }
    public string? Path { get; set; } // set externally
    public string? RemoteName { get; set; } // if null, set externally
    public bool SubjectOnly { get; set; } // set externally
    #endregion 

    public GitLogSettings(ConsoleContext context)
    {
        AfterRelativeDate = context.GetOptionStringOrDefault(GitTasksArguments.LogAfterOption.Name);
        AuthorName = context.GetOptionStringOrDefault(GitTasksArguments.LogAuthorOption.Name);
        BeforeRelativeDate = context.GetOptionStringOrDefault(GitTasksArguments.LogBeforeOption.Name);
        BranchPattern = context.GetOptionStringOrDefault(GitTasksArguments.LogBranchPatternOption.Name);
        DoNotCompactMessages = context.HasSwitch(GitTasksArguments.LogDoNotCompactMessageSwitch.Name);
        DoNotIncludeAll = context.HasSwitch(GitTasksArguments.LogDoNotIncludeAllSwitch.Name);
        FilePattern = context.GetOptionStringOrDefault(GitTasksArguments.LogFilePatternOption.Name);
        IncludeMerges = context.HasSwitch(GitTasksArguments.LogIncludeMergesSwitch.Name);
        ListFiles = context.HasSwitch(GitTasksArguments.LogListFilesSwitch.Name);
        MergesOnly = context.HasSwitch(GitTasksArguments.LogMergesOnlySwitch.Name);
        MessagePattern = context.GetOptionStringOrDefault(GitTasksArguments.LogMessagePatternOption.Name);
        RemoteName = context.GetOptionStringOrDefault(GitTasksArguments.LogRemoteNameOption.Name);
        SubjectOnly = context.HasSwitch(GitTasksArguments.LogSubjectOnlySwitch.Name);
    }
}