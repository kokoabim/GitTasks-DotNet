using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Kokoabim.GitTasks;

public class Executor
{
    public ExecutorResult Execute(string fileName, string? arguments = null, string? workingDirectory = null, CancellationToken cancellationToken = default)
    {
        var outputText = new StringBuilder();
        var result = new ExecutorResult();

        using Process process = new();

        if (cancellationToken != default) _ = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(true);
                    result.Killed = true;
                }
            }
            catch { }
        });

        process.StartInfo = new()
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data is not null) outputText.AppendLine(e.Data);
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data is not null) outputText.AppendLine(e.Data);
        };

        try
        {
            if (!process.Start())
            {
                result.Exception = new InvalidOperationException($"Failed to start process: {(string.IsNullOrWhiteSpace(workingDirectory) ? null : $"({workingDirectory}) ")}{fileName}");
                return result;
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();
            result.ExitCode = process.ExitCode;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 0x00000002)
        {
            result.Exception = new FileNotFoundException($"File not found: {(string.IsNullOrWhiteSpace(workingDirectory) ? null : $"({workingDirectory}) ")}{fileName}", ex);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 0x00000005)
        {
            result.Exception = new UnauthorizedAccessException($"Access denied: {(string.IsNullOrWhiteSpace(workingDirectory) ? null : $"({workingDirectory}) ")}{fileName}", ex);
        }
        catch (Exception ex)
        {
            result.Exception = ex;
        }

        result.Output = outputText.ToString().TrimEnd();
        return result;
    }

    public async Task<ExecutorResult> ExecuteAsync(string fileName, string? arguments = null, string? workingDirectory = null, CancellationToken cancellationToken = default) =>
        await Task.Run(() => Execute(fileName, arguments, workingDirectory, cancellationToken));
}