namespace Kokoabim.GitTasks;

public class GitBranch
{
    public string FullName => IsRemote && Remote is not null ? $"{Remote}/{Name}" : Name;
    public bool IsCurrent { get; set; }
    public bool IsRemote { get; set; }
    public string Name { get; set; }
    public string? Remote { get; set; }

    public GitBranch(string name)
    {
        Name = name;
    }

    public static GitBranch Parse(string text)
    {
        text = text.Trim();
        string name;
        string? remote = null;

        var isCurrent = text.StartsWith("* ");
        var isRemote = text.StartsWith("remotes/");

        if (isCurrent) name = text[2..];
        else if (isRemote)
        {
            if (text.Contains(" -> ")) text = text.Split(" -> ")[0];

            var split = text[8..].Split('/', 2);
            if (split.Length > 1)
            {
                remote = split[0];
                name = split[1];
            }
            else name = split[0];
        }
        else name = text;

        return new GitBranch(name)
        {
            IsCurrent = isCurrent,
            IsRemote = isRemote,
            Remote = remote
        };
    }
}