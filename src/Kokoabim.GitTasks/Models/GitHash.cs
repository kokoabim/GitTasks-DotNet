namespace Kokoabim.GitTasks;

public class GitHash
{
    public string Value { get; set; }
    public string Abbreviated { get; set; }

    public GitHash(string value) : this(value, value[..8]) { }

    public GitHash(string value, string abbreviated)
    {
        Value = value;
        Abbreviated = abbreviated;
    }
}