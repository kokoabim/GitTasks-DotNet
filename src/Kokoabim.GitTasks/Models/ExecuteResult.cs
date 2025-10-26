using System.Diagnostics.CodeAnalysis;

namespace Kokoabim.GitTasks;

public class ExecuteResult
{
    public Exception? Exception { get; set; }
    public int ExitCode { get; set; } = -1;
    public bool Killed { get; set; }
    public string? Output { get; set; }
    public object? Reference { get; set; }

    [MemberNotNullWhen(true, nameof(Output))]
    public bool Success => ExitCode == 0 && !Killed && Exception is null && Output is not null;

    public static ExecuteResult<T?> CreateWithNull<T>(object? reference = null) => new()
    {
        Reference = reference
    };

    public static ExecuteResult<T> CreateWithObject<T>(T? obj, object? reference = null) => new()
    {
        ExitCode = 0,
        Value = obj,
        Output = string.Empty,
        Reference = reference
    };

    public override string ToString() => Output ?? Exception?.Message ?? (Killed ? "Process was killed" : $"Exit code {ExitCode}");

    public ExecuteResult<T> WithNull<T>(object? reference = null) => new()
    {
        Exception = Exception,
        ExitCode = ExitCode,
        Killed = Killed,
        Output = Output,
        Reference = reference ?? Reference
    };

    public ExecuteResult WithReference(object? reference)
    {
        Reference = reference;
        return this;
    }

    public ExecuteResult<T> WithValue<T>(T? value, object? reference = null) => new()
    {
        Exception = Exception,
        ExitCode = ExitCode,
        Killed = Killed,
        Value = value,
        Output = Output,
        Reference = reference ?? Reference
    };
}

public class ExecuteResult<T> : ExecuteResult
{
    [MemberNotNullWhen(true, nameof(Value))]
    public new bool Success => base.Success && Value is not null;

    public T? Value { get; set; }

    public ExecuteResult<T?> AsNullable()
    {
        return new ExecuteResult<T?>
        {
            Exception = Exception,
            ExitCode = ExitCode,
            Killed = Killed,
            Output = Output,
            Reference = Reference,
            Value = Value
        };
    }

    public new ExecuteResult<T> WithReference(object? reference)
    {
        Reference = reference;
        return this;
    }
}