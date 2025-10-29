namespace Kokoabim.GitTasks;

public class GitLogEntryNumStat
{
    public int AddedLines { get; set; }
    public int DeletedLines { get; set; }
    public string FilePath { get; set; }
    public string? PreviousFilePath { get; set; }
    public bool IsBinaryFile { get; set; }

    public GitLogEntryNumStat(string filePath, string? previousFilePath = null)
    {
        FilePath = filePath;
        PreviousFilePath = previousFilePath;
    }
}