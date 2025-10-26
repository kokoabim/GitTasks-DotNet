namespace Kokoabim.GitTasks;

public class GitLogEntryNumStat
{
    public int AddedLines { get; set; }
    public int DeletedLines { get; set; }
    public string FilePath { get; set; }

    public GitLogEntryNumStat(int addedLines, int deletedLines, string filePath)
    {
        AddedLines = addedLines;
        DeletedLines = deletedLines;
        FilePath = filePath;
    }
}
