namespace Kokoabim.GitTasks;

public struct DateRange
{
    public DateTime FromDate { get; set; }
    public readonly TimeSpan Span => ToDate - FromDate;
    public DateTime ToDate { get; set; }
    public readonly int TotalDays => (int)Span.TotalDays + 1;
}