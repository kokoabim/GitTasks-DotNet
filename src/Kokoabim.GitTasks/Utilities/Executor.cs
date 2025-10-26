using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Kokoabim.GitTasks;

public class Executor
{
    public ExecuteResult Execute(string fileName, string? arguments = null, string? workingDirectory = null, CancellationToken cancellationToken = default)
    {
        var execResult = new ExecuteResult();
        var output = new StringBuilder();

        using Process process = new();

        if (cancellationToken != default) _ = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(true);
                    execResult.Killed = true;
                }
            }
            catch { }
        });

        process.StartInfo = new()
        {
            Arguments = arguments,
            CreateNoWindow = true,
            FileName = fileName,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            WorkingDirectory = workingDirectory,
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data is not null) output.AppendLine(e.Data);
        };

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data is not null) output.AppendLine(e.Data);
        };

        try
        {

            if (!process.Start())
            {
                execResult.Exception = new InvalidOperationException($"Failed to start process: {fileName}{(string.IsNullOrWhiteSpace(workingDirectory) ? null : $" ({workingDirectory})")}");
                return execResult;
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();
            execResult.ExitCode = process.ExitCode;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 0x00000002)
        {
            execResult.Exception = new FileNotFoundException($"File not found: {fileName}{(string.IsNullOrWhiteSpace(workingDirectory) ? null : $" ({workingDirectory})")}", ex);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 0x00000005)
        {
            execResult.Exception = new UnauthorizedAccessException($"Access denied: {fileName}{(string.IsNullOrWhiteSpace(workingDirectory) ? null : $" ({workingDirectory})")}", ex);
        }
        catch (Exception ex)
        {
            execResult.Exception = ex;
        }

        execResult.Output = output.ToString().TrimEnd();
        return execResult;
    }

    public async Task<ExecuteResult> ExecuteAsync(string fileName, string? arguments = null, string? workingDirectory = null, CancellationToken cancellationToken = default) =>
        await Task.Run(() => Execute(fileName, arguments, workingDirectory, cancellationToken));
}