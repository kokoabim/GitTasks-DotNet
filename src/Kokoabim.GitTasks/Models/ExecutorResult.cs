using System.Diagnostics.CodeAnalysis;

namespace Kokoabim.GitTasks;

public class ExecutorResult
{
    public Exception? Exception { get; set; }
    public int ExitCode { get; set; } = -1;
    public bool Killed { get; set; }
    public string? Output { get; set; }
    public object? Reference { get; set; }

    [MemberNotNullWhen(true, nameof(Output))]
    public bool Success => ExitCode == 0 && !Killed && Exception is null && Output is not null;

    public static ExecutorResult<T?> CreateWithNull<T>(object? reference = null) => new()
    {
        Reference = reference
    };

    public static ExecutorResult<T> CreateWithObject<T>(T? obj, object? reference = null) => new()
    {
        ExitCode = 0,
        Object = obj,
        Output = string.Empty,
        Reference = reference
    };

    public override string ToString() => Output ?? Exception?.Message ?? (Killed ? "Process was killed" : $"Exit code {ExitCode}");

    public ExecutorResult<T> WithNull<T>(object? reference = null) => new()
    {
        Exception = Exception,
        ExitCode = ExitCode,
        Killed = Killed,
        Output = Output,
        Object = default,
        Reference = reference ?? Reference
    };

    public ExecutorResult<T> WithObject<T>(T? obj, object? reference = null) => new()
    {
        Exception = Exception,
        ExitCode = ExitCode,
        Killed = Killed,
        Object = obj,
        Output = Output,
        Reference = reference ?? Reference
    };

    public ExecutorResult WithReference(object? reference)
    {
        Reference = reference;
        return this;
    }
}

public class ExecutorResult<T> : ExecutorResult
{
    public T? Object { get; set; }

    [MemberNotNullWhen(true, nameof(Object))]
    public new bool Success => base.Success && Object is not null;

    public ExecutorResult<T?> AsNullable()
    {
        return new ExecutorResult<T?>
        {
            Exception = Exception,
            ExitCode = ExitCode,
            Killed = Killed,
            Output = Output,
            Reference = Reference,
            Object = Object
        };
    }

    public new ExecutorResult<T> WithReference(object? reference)
    {
        Reference = reference;
        return this;
    }
}