using System.Diagnostics;

namespace AsterERP.Api.Infrastructure.Publishing;

public sealed record ApplicationPublishProcessRequest(
    string FileName,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    IDictionary<string, string?>? EnvironmentVariables = null);

public sealed record ApplicationPublishProcessResult(int ExitCode, TimeSpan Duration);

public interface IApplicationPublishProcessRunner
{
    Task<ApplicationPublishProcessResult> RunAsync(
        ApplicationPublishProcessRequest request,
        Func<string, CancellationToken, Task> onOutput,
        CancellationToken cancellationToken);
}

public sealed class ApplicationPublishProcessRunner : IApplicationPublishProcessRunner
{
    private static readonly HashSet<string> AllowedExecutables = new(StringComparer.OrdinalIgnoreCase)
    {
        "dotnet",
        "dotnet.exe",
        "node",
        "node.exe",
        "npm",
        "npm.cmd"
    };

    public async Task<ApplicationPublishProcessResult> RunAsync(
        ApplicationPublishProcessRequest request,
        Func<string, CancellationToken, Task> onOutput,
        CancellationToken cancellationToken)
    {
        if (!AllowedExecutables.Contains(request.FileName))
        {
            throw new InvalidOperationException("Executable is not allowed for application publishing.");
        }

        var command = ResolveCommand(request);
        var startInfo = new ProcessStartInfo
        {
            FileName = command.FileName,
            WorkingDirectory = request.WorkingDirectory,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        foreach (var argument in command.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (request.EnvironmentVariables is not null)
        {
            foreach (var pair in request.EnvironmentVariables)
            {
                if (pair.Value is null)
                {
                    startInfo.Environment.Remove(pair.Key);
                    continue;
                }

                startInfo.Environment[pair.Key] = pair.Value;
            }
        }

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        var startedAt = DateTime.UtcNow;
        process.Start();

        var stdout = PumpAsync(process.StandardOutput, onOutput, cancellationToken);
        var stderr = PumpAsync(process.StandardError, onOutput, cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        await Task.WhenAll(stdout, stderr);
        return new ApplicationPublishProcessResult(process.ExitCode, DateTime.UtcNow - startedAt);
    }

    private static ResolvedCommand ResolveCommand(ApplicationPublishProcessRequest request)
    {
        var executableName = Path.GetFileName(request.FileName);
        if (!string.Equals(executableName, request.FileName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Executable path is not allowed for application publishing.");
        }

        if (!AllowedExecutables.Contains(executableName))
        {
            throw new InvalidOperationException("Executable is not allowed for application publishing.");
        }

        if (IsNpm(executableName))
        {
            var nodePath = ResolveExecutable(OperatingSystem.IsWindows() ? "node.exe" : "node");
            var npmCliPath = ResolveNpmCli(nodePath);
            return new ResolvedCommand(nodePath, [npmCliPath, .. request.Arguments]);
        }

        return new ResolvedCommand(ResolveExecutable(executableName), request.Arguments);
    }

    private static string ResolveExecutable(string executableName)
    {
        var candidateNames = GetExecutableCandidateNames(executableName);
        var paths = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var directory in paths)
        {
            foreach (var candidateName in candidateNames)
            {
                var candidate = Path.Combine(directory, candidateName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        throw new InvalidOperationException($"Required executable '{executableName}' was not found in PATH.");
    }

    private static IReadOnlyList<string> GetExecutableCandidateNames(string executableName)
    {
        if (!OperatingSystem.IsWindows() || Path.HasExtension(executableName))
        {
            return [executableName];
        }

        var extensions = (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return [executableName, .. extensions.Select(extension => executableName + extension.ToLowerInvariant())];
    }

    private static string ResolveNpmCli(string nodePath)
    {
        var nodeDirectory = Path.GetDirectoryName(nodePath)
            ?? throw new InvalidOperationException("Unable to resolve Node.js installation directory.");
        var npmCliPath = Path.Combine(nodeDirectory, "node_modules", "npm", "bin", "npm-cli.js");
        if (!File.Exists(npmCliPath))
        {
            throw new InvalidOperationException("npm CLI script was not found in the Node.js installation directory.");
        }

        return npmCliPath;
    }

    private static bool IsNpm(string executableName) =>
        string.Equals(executableName, "npm", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(executableName, "npm.cmd", StringComparison.OrdinalIgnoreCase);

    private static async Task PumpAsync(
        TextReader reader,
        Func<string, CancellationToken, Task> onOutput,
        CancellationToken cancellationToken)
    {
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            await onOutput(line, cancellationToken);
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best-effort child process cleanup on cancellation.
        }
    }

    private sealed record ResolvedCommand(string FileName, IReadOnlyList<string> Arguments);
}
