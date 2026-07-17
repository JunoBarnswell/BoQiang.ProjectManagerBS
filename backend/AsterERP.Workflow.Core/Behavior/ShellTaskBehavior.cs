using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Execution;
using AsterERP.Workflow.Core.Expression;

namespace AsterERP.Workflow.Core.Behavior;

public interface IShellCommandExecutor
{
    Task<ShellExecutionResult> ExecuteAsync(ShellExecutionContext context, ExecutionEntity execution, CancellationToken cancellationToken = default);
}

public class ShellExecutionContext
{
    public string? Command { get; set; }
    public List<string> Arguments { get; set; } = new();
    public bool WaitForCompletion { get; set; } = true;
    public bool CleanEnvironment { get; set; }
    public bool RedirectError { get; set; }
    public string? WorkingDirectory { get; set; }
    public string? ResultVariable { get; set; }
    public string? ErrorCodeVariable { get; set; }
    public Dictionary<string, string> EnvironmentVariables { get; set; } = new();
}

public class ShellExecutionResult
{
    public int ExitCode { get; set; }
    public string? Output { get; set; }
    public string? Error { get; set; }
    public bool TimedOut { get; set; }
}

public class DefaultShellCommandExecutor : IShellCommandExecutor
{
    public async Task<ShellExecutionResult> ExecuteAsync(ShellExecutionContext context, ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = context.Command,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        if (context.RedirectError)
        {
            startInfo.RedirectStandardError = true;
            startInfo.StandardErrorEncoding = System.Text.Encoding.UTF8;
        }

        if (context.CleanEnvironment)
        {
            startInfo.Environment.Clear();
        }

        foreach (var arg in context.Arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        if (!string.IsNullOrEmpty(context.WorkingDirectory))
        {
            startInfo.WorkingDirectory = context.WorkingDirectory;
        }

        foreach (var envVar in context.EnvironmentVariables)
        {
            startInfo.Environment[envVar.Key] = envVar.Value;
        }

        var result = new ShellExecutionResult();

        if (context.WaitForCompletion)
        {
            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            Task<string?> errorTask = startInfo.RedirectStandardError
                ? process.StandardError.ReadToEndAsync(cancellationToken)
                : Task.FromResult<string?>(null);

            if (!process.WaitForExit(30000))
            {
                result.TimedOut = true;
                try { process.Kill(true); } catch { }
                result.Output = await outputTask;
                result.Error = "Process timed out";
                return result;
            }

            result.Output = await outputTask;
            result.Error = await errorTask;
            result.ExitCode = process.ExitCode;
        }
        else
        {
            using var process = new Process { StartInfo = startInfo };
            process.Start();
            result.ExitCode = 0;
        }

        return result;
    }
}

public class ShellActivityBehavior : FlowNodeActivityBehavior
{
    protected IExpressionManager? ExpressionManager { get; set; }
    protected IShellCommandExecutor? CommandExecutor { get; set; }

    public string? Command { get; set; }
    public string? Arg1 { get; set; }
    public string? Arg2 { get; set; }
    public string? Arg3 { get; set; }
    public string? Arg4 { get; set; }
    public string? Arg5 { get; set; }
    public string? Wait { get; set; }
    public string? ResultVariable { get; set; }
    public string? ErrorCodeVariable { get; set; }
    public string? RedirectError { get; set; }
    public string? CleanEnv { get; set; }
    public string? Directory { get; set; }

    public ShellActivityBehavior() { }

    public ShellActivityBehavior(
        IExpressionManager? expressionManager = null,
        IShellCommandExecutor? commandExecutor = null)
    {
        ExpressionManager = expressionManager;
        CommandExecutor = commandExecutor;
    }

    public override async Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        var commandStr = ResolveField(Command, execution);
        var arg1Str = ResolveField(Arg1, execution);
        var arg2Str = ResolveField(Arg2, execution);
        var arg3Str = ResolveField(Arg3, execution);
        var arg4Str = ResolveField(Arg4, execution);
        var arg5Str = ResolveField(Arg5, execution);
        var waitStr = ResolveField(Wait, execution);
        var resultVariableStr = ResolveField(ResultVariable, execution);
        var errorCodeVariableStr = ResolveField(ErrorCodeVariable, execution);
        var redirectErrorStr = ResolveField(RedirectError, execution);
        var cleanEnvStr = ResolveField(CleanEnv, execution);
        var directoryStr = ResolveField(Directory, execution);

        if (string.IsNullOrEmpty(commandStr))
        {
            throw new WorkflowEngineException("Shell task command is null");
        }

        var waitFlag = waitStr == null || waitStr.Equals("true", StringComparison.OrdinalIgnoreCase);
        var redirectErrorFlag = "true".Equals(redirectErrorStr, StringComparison.OrdinalIgnoreCase);
        var cleanEnvBoolean = "true".Equals(cleanEnvStr, StringComparison.OrdinalIgnoreCase);

        var argList = new List<string> { commandStr };
        if (arg1Str != null) argList.Add(arg1Str);
        if (arg2Str != null) argList.Add(arg2Str);
        if (arg3Str != null) argList.Add(arg3Str);
        if (arg4Str != null) argList.Add(arg4Str);
        if (arg5Str != null) argList.Add(arg5Str);

        var context = new ShellExecutionContext
        {
            Command = commandStr,
            Arguments = argList,
            WaitForCompletion = waitFlag,
            CleanEnvironment = cleanEnvBoolean,
            RedirectError = redirectErrorFlag,
            WorkingDirectory = directoryStr,
            ResultVariable = resultVariableStr,
            ErrorCodeVariable = errorCodeVariableStr
        };

        var executor = CommandExecutor ?? new DefaultShellCommandExecutor();

        try
        {
            var result = await executor.ExecuteAsync(context, execution, cancellationToken);

            if (!string.IsNullOrEmpty(resultVariableStr) && result.Output != null)
            {
                execution.SetVariable(resultVariableStr, result.Output);
            }

            if (!string.IsNullOrEmpty(errorCodeVariableStr) && result.ExitCode != 0)
            {
                execution.SetVariable(errorCodeVariableStr, result.ExitCode);
            }

            if (result.ExitCode != 0)
            {
                execution.SetVariable("_shellExitCode", result.ExitCode);
                if (!string.IsNullOrEmpty(result.Error))
                {
                    execution.SetVariable("_shellError", result.Error);
                }
            }
        }
        catch (Exception ex)
        {
            throw new WorkflowEngineException("Could not execute shell command", ex);
        }

        execution.IsActive = false;
        await LeaveAsync(execution, cancellationToken);
    }

    protected virtual string? ResolveField(string? field, ExecutionEntity execution)
    {
        if (string.IsNullOrEmpty(field)) return field;

        if (ExpressionManager != null && (field.StartsWith("${") || field.StartsWith("#{")))
        {
            var result = ExpressionManager.Evaluate(field, execution.Variables);
            return result?.ToString();
        }

        return field;
    }
}
