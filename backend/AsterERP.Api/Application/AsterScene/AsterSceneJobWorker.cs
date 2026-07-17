using System.Text.Json;
using AsterERP.Api.Modules.AsterScene;
using SqlSugar;

namespace AsterERP.Api.Application.AsterScene;

public sealed class AsterSceneJobWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<AsterSceneJobWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessNextBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "AsterScene job worker tick failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task ProcessNextBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();
        var jobs = await db.Queryable<AsterSceneJobEntity>()
            .Where(item => !item.IsDeleted && item.Status == "Pending")
            .OrderBy(item => item.CreatedTime)
            .Take(5)
            .ToListAsync(cancellationToken);

        foreach (var job in jobs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ProcessJobAsync(db, job, cancellationToken);
        }
    }

    private static async Task ProcessJobAsync(
        ISqlSugarClient db,
        AsterSceneJobEntity job,
        CancellationToken cancellationToken)
    {
        job.Status = "Running";
        job.ProgressPercent = 10;
        job.Attempts += 1;
        job.StartedTime = DateTime.UtcNow;
        job.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(job).ExecuteCommandAsync(cancellationToken);

        if (job.JobType == "AiSceneGenerate")
        {
            await FailAiJobAndRefundAsync(db, job, cancellationToken);
            return;
        }

        job.Status = "Failed";
        job.ProgressPercent = 100;
        job.ErrorCode = "UnsupportedJobType";
        job.ErrorMessage = $"AsterScene job type {job.JobType} is not supported by the worker.";
        job.FinishedTime = DateTime.UtcNow;
        job.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(job).ExecuteCommandAsync(cancellationToken);
    }

    private static async Task FailAiJobAndRefundAsync(
        ISqlSugarClient db,
        AsterSceneJobEntity job,
        CancellationToken cancellationToken)
    {
        var prompt = ReadPrompt(job.InputJson);
        var refundKey = $"ai-job-refund:{job.Id}";
        var hasRefund = await db.Queryable<AsterSceneAiCreditLedgerEntity>()
            .AnyAsync(item => !item.IsDeleted && item.IdempotencyKey == refundKey, cancellationToken);
        var debit = await db.Queryable<AsterSceneAiCreditLedgerEntity>()
            .FirstAsync(item => !item.IsDeleted && item.JobId == job.Id && item.Direction == "Debit", cancellationToken);

        var ownsTransaction = db.Ado.Transaction is null;
        try
        {
            if (ownsTransaction)
            {
                await db.Ado.BeginTranAsync();
            }

            job.Status = "Failed";
            job.ProgressPercent = 100;
            job.ErrorCode = "AiProviderNotConfigured";
            job.ErrorMessage = "AsterScene AI provider is not configured; credits were refunded and no document changes were applied.";
            job.OutputJson = JsonSerializer.Serialize(new
            {
                gate = "Blocked",
                prompt,
                safety = "no-provider-no-apply"
            });
            job.FinishedTime = DateTime.UtcNow;
            job.UpdatedTime = DateTime.UtcNow;
            await db.Updateable(job).ExecuteCommandAsync(cancellationToken);

            if (!hasRefund && debit is not null)
            {
                await db.Insertable(new AsterSceneAiCreditLedgerEntity
                {
                    TenantId = job.TenantId,
                    AppCode = job.AppCode,
                    OwnerUserId = job.OwnerUserId,
                    JobId = job.Id,
                    Credits = debit.Credits,
                    Direction = "Credit",
                    IdempotencyKey = refundKey,
                    CreatedBy = "asterscene-worker"
                }).ExecuteCommandAsync(cancellationToken);
            }

            if (ownsTransaction)
            {
                await db.Ado.CommitTranAsync();
            }
        }
        catch
        {
            if (ownsTransaction)
            {
                await db.Ado.RollbackTranAsync();
            }

            throw;
        }
    }

    private static string ReadPrompt(string? inputJson)
    {
        if (string.IsNullOrWhiteSpace(inputJson))
        {
            return string.Empty;
        }

        using var document = JsonDocument.Parse(inputJson);
        return document.RootElement.TryGetProperty("prompt", out var prompt)
            ? prompt.GetString() ?? string.Empty
            : string.Empty;
    }
}
