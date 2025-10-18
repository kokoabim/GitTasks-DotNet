using System.Diagnostics.CodeAnalysis;

namespace Kokoabim.GitTasks;

public class ExecutorResult
{
    public Exception? Exception { get; set; }
    public int ExitCode { get; set; } = -1;
    public bool Killed { get; set; }
    public string? Output { get; set; }

    [MemberNotNullWhen(true, nameof(Output))]
    public bool Success => ExitCode == 0 && !Killed && Exception is null && Output is not null;

    public override string ToString() => Output ?? Exception?.Message ?? (Killed ? "Process was killed" : $"Exit code {ExitCode}");

    public ExecutorResult<T> WithObject<T>(T? obj) => new()
    {
        Exception = Exception,
        ExitCode = ExitCode,
        Killed = Killed,
        Object = obj,
        Output = Output,
    };
}

public class ExecutorResult<T> : ExecutorResult
{
    public T? Object { get; set; }

    [MemberNotNullWhen(true, nameof(Object))]
    public new bool Success => base.Success && Object is not null;
}