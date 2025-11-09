using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Kokoabim.GitTasks;

public class DiffStat
{
    #region properties
    public bool Added => Status == "A";
    public bool Broken => Status == "B";
    public bool Copied => Status.StartsWith('C');
    public bool Deleted => Status == "D";
    public int Deletions { get; private set; }
    public string FilePath { get; private set; } = null!;
    public int Insertions { get; private set; }
    public bool Modified => Status == "M";
    public bool Renamed => Status.StartsWith('R');
    public string Status { get; set; } = string.Empty;
    public bool TypeChanged => Status == "T";
    public bool Unknown => Status == "X";
    public bool Unmerged => Status == "U";
    #endregion 

    public static bool TryParse(string statLine, string[]? nameStatusLines, [NotNullWhen(true)] out DiffStat? diffStat)
    {
        diffStat = null;

        var parts = statLine.Trim().Split(['\t', ' '], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3) return false;

        diffStat = new DiffStat
        {
            Deletions = int.TryParse(parts[1], out var del) ? del : 0,
            FilePath = parts[2],
            Insertions = int.TryParse(parts[0], out var ins) ? ins : 0,
        };

        if (nameStatusLines is not null) diffStat.Status = nameStatusLines
            .FirstOrDefault(ns => ns.EndsWith(parts[2]))?
            .Split(['\t', ' '], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? string.Empty;

        return true;
    }
}

public class DiffStats
{
    public int Deletions { get; private set; }
    public int FilesChanged { get; private set; }
    public IReadOnlyCollection<DiffStat> FileStats { get; private set; } = [];
    public int Insertions { get; private set; }
    public string Summary { get; private set; } = string.Empty;

    public override string ToString()
    {
        if (FileStats.Count == 0) return "";

        var longestStatusLength = FileStats.Max(fs => fs.Status.Length);
        var longestInsertionsLength = FileStats.Max(fs => fs.Insertions.ToString().Length);
        var longestDeletionsLength = FileStats.Max(fs => fs.Deletions.ToString().Length);

        var sb = new StringBuilder();

        foreach (var fileStat in FileStats)
        {
            sb.AppendLine($"{fileStat.Status.PadRight(longestStatusLength)}  " +
                $"{fileStat.Insertions.ToString().PadLeft(longestInsertionsLength)}  " +
                $"{fileStat.Deletions.ToString().PadLeft(longestDeletionsLength)}  " +
                $"{fileStat.FilePath}");
        }

        return sb.ToString().TrimEnd();
    }

    public static bool TryParse(string statsOutput, string nameStatusesOutput, [NotNullWhen(true)] out DiffStats? diffStats)
    {
        diffStats = null;

        if (string.IsNullOrWhiteSpace(statsOutput)) return false;

        var lines = statsOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2) return false;

        var summary = lines[^1].Trim();

        var statsLine = summary.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (statsLine.Length != 7) return false;

        var nameStatusLines = nameStatusesOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        diffStats = new DiffStats
        {
            Deletions = int.TryParse(statsLine[5], out var del) ? del : 0,
            FileStats = [.. lines[..^1].Select(ds => DiffStat.TryParse(ds, nameStatusLines, out var diffStat) ? diffStat : null).Where(ds => ds is not null)!],
            FilesChanged = int.TryParse(statsLine[0], out var fc) ? fc : 0,
            Insertions = int.TryParse(statsLine[3], out var ins) ? ins : 0,
            Summary = summary
        };

        return true;
    }
}