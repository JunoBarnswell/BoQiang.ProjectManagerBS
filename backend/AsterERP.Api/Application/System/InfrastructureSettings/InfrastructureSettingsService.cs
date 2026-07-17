using System.Diagnostics;
using AsterERP.Api.Infrastructure.Database;
using System.Text;
using AsterERP.Api.Application.Common;
using AsterERP.Api.Infrastructure.Abp.Settings;
using AsterERP.Api.Infrastructure.Files;
using AsterERP.Api.Infrastructure.Messaging;
using AsterERP.Api.Infrastructure.UnitOfWork;
using AsterERP.Api.Modules.System.Messaging;
using AsterERP.Api.Modules.System.Parameters;
using AsterERP.Contracts.System.InfrastructureSettings;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Volo.Abp.Emailing;

namespace AsterERP.Api.Application.System.InfrastructureSettings;

public sealed class InfrastructureSettingsService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IAsterErpMessagingService messagingService,
    IFileStorageService fileStorageService)
    : IInfrastructureSettingsService
{
    private static readonly IReadOnlySet<string> SmsProviders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Null",
        "Aliyun",
        "Tencent"
    };

    private static readonly IReadOnlySet<string> ObjectStorageProviders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "FileSystem",
        "Minio",
        "Aliyun"
    };

    private static readonly IReadOnlySet<string> CacheProviders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Memory",
        "Redis"
    };

    private static readonly IReadOnlyDictionary<string, Func<ISugarQueryable<SystemMessageSendLogEntity>, OrderByType, ISugarQueryable<SystemMessageSendLogEntity>>> LogSorters =
        new Dictionary<string, Func<ISugarQueryable<SystemMessageSendLogEntity>, OrderByType, ISugarQueryable<SystemMessageSendLogEntity>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["channel"] = (query, order) => query.OrderBy(item => item.Channel, order),
            ["provider"] = (query, order) => query.OrderBy(item => item.Provider, order),
            ["result"] = (query, order) => query.OrderBy(item => item.Result, order),
            ["traceId"] = (query, order) => query.OrderBy(item => item.TraceId, order),
            ["durationMs"] = (query, order) => query.OrderBy(item => item.DurationMs, order),
            ["createdTime"] = (query, order) => query.OrderBy(item => item.CreatedTime, order)
        };

    private static readonly IReadOnlyDictionary<string, Func<ISugarQueryable<SystemMessageSendLogEntity>, GridFilter, ISugarQueryable<SystemMessageSendLogEntity>>> LogFilterers =
        new Dictionary<string, Func<ISugarQueryable<SystemMessageSendLogEntity>, GridFilter, ISugarQueryable<SystemMessageSendLogEntity>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["channel"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.Channel),
            ["provider"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.Provider),
            ["result"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.Result),
            ["traceId"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.TraceId),
            ["maskedTarget"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.MaskedTarget),
            ["createdTime"] = (query, filter) => GridFilterApplier.ApplyDateTime(query, filter, item => item.CreatedTime)
        };

    public async Task<InfrastructureSettingsResponse> GetAsync(CancellationToken cancellationToken = default)
    {
        var values = await LoadValuesAsync(cancellationToken);
        return MapSettings(values);
    }

    public async Task<InfrastructureSettingsResponse> UpdateAsync(
        InfrastructureSettingsUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        await unitOfWork.ExecuteAsync(async () =>
        {
            await ApplyEmailAsync(request.Email, cancellationToken);
            await ApplySmsAsync(request.Sms, cancellationToken);
            await ApplyObjectStorageAsync(request.ObjectStorage, cancellationToken);
            await ApplyCacheAsync(request.Cache, cancellationToken);
            await ApplyJobsAsync(request.Jobs, cancellationToken);
            await ApplyAuditAsync(request.Audit, cancellationToken);
        }, cancellationToken);

        return await GetAsync(cancellationToken);
    }

    public async Task<InfrastructureTestResult> TestEmailAsync(
        InfrastructureEmailTestRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.To))
        {
            throw new ValidationException("测试邮件接收地址不能为空", ErrorCodes.ParameterInvalid);
        }

        var startedAt = Stopwatch.GetTimestamp();
        var result = await messagingService.SendEmailAsync(
            new AsterErpEmailMessage(
                request.To.Trim(),
                NormalizeOptional(request.Subject) ?? "AsterERP 邮件配置测试",
                NormalizeOptional(request.Body) ?? "AsterERP 邮件基础设施配置测试。",
                request.IsBodyHtml),
            cancellationToken);

        return ToTestResult(result, startedAt);
    }

    public async Task<InfrastructureTestResult> TestSmsAsync(
        InfrastructureSmsTestRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            throw new ValidationException("测试短信手机号不能为空", ErrorCodes.ParameterInvalid);
        }

        var startedAt = Stopwatch.GetTimestamp();
        var result = await messagingService.SendSmsAsync(
            new AsterErpSmsMessage(
                request.PhoneNumber.Trim(),
                NormalizeOptional(request.Text) ?? "AsterERP 短信配置测试"),
            cancellationToken);

        return ToTestResult(result, startedAt);
    }

    public async Task<InfrastructureTestResult> TestObjectStorageAsync(
        InfrastructureObjectStorageTestRequest request,
        CancellationToken cancellationToken = default)
    {
        var traceId = ResolveTraceId();
        var startedAt = Stopwatch.GetTimestamp();
        var provider = NormalizeProvider(
            request.Provider ?? await GetValueAsync(AsterErpSettingNames.ObjectStorageProvider, cancellationToken) ?? "FileSystem",
            ObjectStorageProviders,
            "对象存储 Provider 不受支持");

        if (!provider.Equals("FileSystem", StringComparison.OrdinalIgnoreCase))
        {
            var missingMessage = await ValidateExternalObjectStorageSettingsAsync(provider, cancellationToken);
            if (missingMessage is not null)
            {
                return BuildTestResult(false, provider, traceId, missingMessage, startedAt);
            }

            return BuildTestResult(
                false,
                provider,
                traceId,
                $"{provider} 配置已保存，但当前文件内容链路仍由 ABP FileSystem Blob provider 承载；切换外部 provider 需在部署配置中启用对应 ABP provider。",
                startedAt);
        }

        var fileName = $"object-storage-smoke-{Guid.NewGuid():N}.txt";
        var payload = Encoding.UTF8.GetBytes($"astererp object storage smoke {Guid.NewGuid():N}");
        string? storedPath = null;

        try
        {
            await using var writeStream = new MemoryStream(payload);
            storedPath = await fileStorageService.SaveAsync(writeStream, fileName, cancellationToken);
            using var readBack = new MemoryStream();
            await using (var readStream = await fileStorageService.OpenReadAsync(storedPath, cancellationToken))
            {
                await readStream.CopyToAsync(readBack, cancellationToken);
            }

            var matches = payload.SequenceEqual(readBack.ToArray());
            if (storedPath is not null)
            {
                await fileStorageService.DeleteAsync(storedPath, cancellationToken);
            }

            return BuildTestResult(
                matches,
                provider,
                traceId,
                matches ? "本地 ABP Blob 存储上传、读取、删除通过" : "本地 ABP Blob 存储读回内容不一致",
                startedAt);
        }
        catch (Exception ex)
        {
            return BuildTestResult(false, provider, traceId, ex.Message, startedAt);
        }
        finally
        {
            if (storedPath is not null)
            {
                try
                {
                    await fileStorageService.DeleteAsync(storedPath, cancellationToken);
                }
                catch
                {
                    // Best-effort cleanup after a failed smoke path.
                }
            }
        }
    }

    public async Task<GridPageResult<MessageSendLogResponse>> GetMessageLogsAsync(
        MessageSendLogQuery query,
        CancellationToken cancellationToken = default)
    {
        var pageIndex = Math.Max(query.PageIndex, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var dataQuery = ApplyMessageLogFilters(
            databaseAccessor.GetCurrentDb().Queryable<SystemMessageSendLogEntity>().Where(item => !item.IsDeleted),
            query);

        dataQuery = GridFilterApplier.Apply(dataQuery, query.Filters, LogFilterers);
        var total = await dataQuery.CountAsync(cancellationToken);
        var items = await GridSortApplier
            .Apply(dataQuery, query.Sorts, LogSorters, ApplyDefaultLogSort)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new GridPageResult<MessageSendLogResponse>
        {
            Total = total,
            Items = items.Select(MapLog).ToList()
        };
    }

    private async Task ApplyEmailAsync(EmailInfrastructureSettingsUpdate? update, CancellationToken cancellationToken)
    {
        if (update is null)
        {
            return;
        }

        await UpsertOptionalAsync(AsterErpSettingNames.EmailEnabled, ToString(update.Enabled), cancellationToken);
        await UpsertOptionalAsync(EmailSettingNames.Smtp.Host, update.SmtpHost, cancellationToken);
        await UpsertOptionalAsync(EmailSettingNames.Smtp.Port, ToString(update.SmtpPort), cancellationToken);
        await UpsertOptionalAsync(EmailSettingNames.Smtp.UserName, update.UserName, cancellationToken);
        await UpsertSecretAsync(EmailSettingNames.Smtp.Password, update.Password, cancellationToken);
        await UpsertOptionalAsync(EmailSettingNames.Smtp.EnableSsl, ToString(update.EnableSsl), cancellationToken);
        await UpsertOptionalAsync(EmailSettingNames.Smtp.UseDefaultCredentials, "false", cancellationToken);
        await UpsertOptionalAsync(EmailSettingNames.DefaultFromAddress, update.DefaultFromAddress, cancellationToken);
        await UpsertOptionalAsync(EmailSettingNames.DefaultFromDisplayName, update.DefaultFromDisplayName, cancellationToken);
    }

    private async Task ApplySmsAsync(SmsInfrastructureSettingsUpdate? update, CancellationToken cancellationToken)
    {
        if (update is null)
        {
            return;
        }

        var provider = update.Provider is null
            ? null
            : NormalizeProvider(update.Provider, SmsProviders, "短信 Provider 不受支持");

        await UpsertOptionalAsync(AsterErpSettingNames.SmsEnabled, ToString(update.Enabled), cancellationToken);
        await UpsertOptionalAsync(AsterErpSettingNames.SmsProvider, provider, cancellationToken);
        await UpsertOptionalAsync(AsterErpSettingNames.SmsAliyunAccessKeyId, update.AliyunAccessKeyId, cancellationToken);
        await UpsertSecretAsync(AsterErpSettingNames.SmsAliyunAccessKeySecret, update.AliyunAccessKeySecret, cancellationToken);
        await UpsertOptionalAsync(AsterErpSettingNames.SmsAliyunSignName, update.AliyunSignName, cancellationToken);
        await UpsertOptionalAsync(AsterErpSettingNames.SmsAliyunTemplateCode, update.AliyunTemplateCode, cancellationToken);
        await UpsertOptionalAsync(AsterErpSettingNames.SmsAliyunTemplateParamName, update.AliyunTemplateParamName, cancellationToken);
        await UpsertOptionalAsync(AsterErpSettingNames.SmsTencentSecretId, update.TencentSecretId, cancellationToken);
        await UpsertSecretAsync(AsterErpSettingNames.SmsTencentSecretKey, update.TencentSecretKey, cancellationToken);
        await UpsertOptionalAsync(AsterErpSettingNames.SmsTencentSdkAppId, update.TencentSdkAppId, cancellationToken);
        await UpsertOptionalAsync(AsterErpSettingNames.SmsTencentSignName, update.TencentSignName, cancellationToken);
        await UpsertOptionalAsync(AsterErpSettingNames.SmsTencentTemplateId, update.TencentTemplateId, cancellationToken);
        await UpsertOptionalAsync(AsterErpSettingNames.SmsTencentRegion, update.TencentRegion, cancellationToken);
    }

    private async Task ApplyObjectStorageAsync(ObjectStorageInfrastructureSettingsUpdate? update, CancellationToken cancellationToken)
    {
        if (update is null)
        {
            return;
        }

        var provider = update.Provider is null
            ? null
            : NormalizeProvider(update.Provider, ObjectStorageProviders, "对象存储 Provider 不受支持");

        await UpsertOptionalAsync(AsterErpSettingNames.ObjectStorageProvider, provider, cancellationToken);
        await UpsertOptionalAsync(AsterErpSettingNames.ObjectStorageFileSystemBasePath, update.FileSystemBasePath, cancellationToken);
        await UpsertOptionalAsync(AsterErpSettingNames.ObjectStorageFileSystemAppendContainerName, ToString(update.FileSystemAppendContainerName), cancellationToken);
        await UpsertOptionalAsync(AsterErpSettingNames.ObjectStorageAliyunEndpoint, update.AliyunEndpoint, cancellationToken);
        await UpsertOptionalAsync(AsterErpSettingNames.ObjectStorageAliyunBucketName, update.AliyunBucketName, cancellationToken);
        await UpsertOptionalAsync(AsterErpSettingNames.ObjectStorageAliyunAccessKeyId, update.AliyunAccessKeyId, cancellationToken);
        await UpsertSecretAsync(AsterErpSettingNames.ObjectStorageAliyunAccessKeySecret, update.AliyunAccessKeySecret, cancellationToken);
        await UpsertOptionalAsync(AsterErpSettingNames.ObjectStorageMinioEndpoint, update.MinioEndpoint, cancellationToken);
        await UpsertOptionalAsync(AsterErpSettingNames.ObjectStorageMinioBucketName, update.MinioBucketName, cancellationToken);
        await UpsertOptionalAsync(AsterErpSettingNames.ObjectStorageMinioAccessKey, update.MinioAccessKey, cancellationToken);
        await UpsertSecretAsync(AsterErpSettingNames.ObjectStorageMinioSecretKey, update.MinioSecretKey, cancellationToken);
        await UpsertOptionalAsync(AsterErpSettingNames.ObjectStorageMinioWithSsl, ToString(update.MinioWithSsl), cancellationToken);
    }

    private async Task ApplyCacheAsync(CacheInfrastructureSettingsUpdate? update, CancellationToken cancellationToken)
    {
        if (update is null)
        {
            return;
        }

        var provider = update.Provider is null
            ? null
            : NormalizeProvider(update.Provider, CacheProviders, "缓存 Provider 不受支持");

        await UpsertOptionalAsync(AsterErpSettingNames.CacheProvider, provider, cancellationToken);
        await UpsertSecretAsync(AsterErpSettingNames.CacheRedisConfiguration, update.RedisConfiguration, cancellationToken);
        await UpsertOptionalAsync(AsterErpSettingNames.CacheDefaultExpirationMinutes, ToString(update.DefaultExpirationMinutes, 1, 1440), cancellationToken);
    }

    private async Task ApplyJobsAsync(JobsInfrastructureSettingsUpdate? update, CancellationToken cancellationToken)
    {
        if (update is null)
        {
            return;
        }

        await UpsertOptionalAsync(AsterErpSettingNames.JobsAbpBackgroundJobsEnabled, ToString(update.AbpBackgroundJobsEnabled), cancellationToken);
        await UpsertOptionalAsync(AsterErpSettingNames.JobsMessagingJobsEnabled, ToString(update.MessagingJobsEnabled), cancellationToken);
        await UpsertOptionalAsync(AsterErpSettingNames.JobsTestTimeoutSeconds, ToString(update.TestTimeoutSeconds, 1, 120), cancellationToken);
    }

    private async Task ApplyAuditAsync(AuditInfrastructureSettingsUpdate? update, CancellationToken cancellationToken)
    {
        if (update is null)
        {
            return;
        }

        await UpsertOptionalAsync(AsterErpSettingNames.AuditOperationLogEnabled, ToString(update.OperationLogEnabled), cancellationToken);
        await UpsertOptionalAsync(AsterErpSettingNames.AuditCaptureQueryString, ToString(update.CaptureQueryString), cancellationToken);
        await UpsertOptionalAsync(AsterErpSettingNames.AuditQueueCapacity, ToString(update.QueueCapacity, 128, 100_000), cancellationToken);
    }

    private async Task<IReadOnlyDictionary<string, string>> LoadValuesAsync(CancellationToken cancellationToken)
    {
        var rows = await databaseAccessor.GetCurrentDb().Queryable<SystemParameterEntity>()
            .Where(item => !item.IsDeleted)
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(item => item.ParamKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.FirstOrDefault(item => item.IsEnabled)?.ParamValue ?? group.First().ParamValue,
                StringComparer.OrdinalIgnoreCase);
    }

    private async Task<string?> GetValueAsync(string key, CancellationToken cancellationToken)
    {
        var entity = await databaseAccessor.GetCurrentDb().Queryable<SystemParameterEntity>()
            .Where(item => !item.IsDeleted && item.ParamKey == key)
            .Take(1)
            .FirstAsync(cancellationToken);

        return entity?.ParamValue;
    }

    private async Task UpsertOptionalAsync(string key, string? value, CancellationToken cancellationToken)
    {
        if (value is null)
        {
            return;
        }

        await UpsertAsync(key, value.Trim(), cancellationToken);
    }

    private async Task UpsertSecretAsync(string key, SecretSettingUpdate? update, CancellationToken cancellationToken)
    {
        if (update is null)
        {
            return;
        }

        if (update.Clear)
        {
            await UpsertAsync(key, string.Empty, cancellationToken);
            return;
        }

        if (!string.IsNullOrWhiteSpace(update.Value))
        {
            await UpsertAsync(key, update.Value.Trim(), cancellationToken);
        }
    }

    private async Task UpsertAsync(string key, string value, CancellationToken cancellationToken)
    {
        var descriptor = InfrastructureSettingCatalog.Get(key);
        var existing = await databaseAccessor.GetCurrentDb().Queryable<SystemParameterEntity>()
            .Where(item => !item.IsDeleted && item.ParamKey == key)
            .Take(1)
            .FirstAsync(cancellationToken);

        if (existing is null)
        {
            await databaseAccessor.GetCurrentDb().Insertable(new SystemParameterEntity
            {
                ParamName = descriptor.Name,
                ParamKey = key,
                ParamValue = value,
                Category = descriptor.Category,
                IsEnabled = true,
                CreatedBy = currentUser.GetAsterErpUserId(),
                CreatedTime = DateTime.UtcNow
            }).ExecuteCommandAsync(cancellationToken);
            return;
        }

        existing.ParamName = descriptor.Name;
        existing.ParamValue = value;
        existing.Category = descriptor.Category;
        existing.IsEnabled = true;
        existing.UpdatedBy = currentUser.GetAsterErpUserId();
        existing.UpdatedTime = DateTime.UtcNow;
        await databaseAccessor.GetCurrentDb().Updateable(existing).ExecuteCommandAsync(cancellationToken);
    }

    private InfrastructureSettingsResponse MapSettings(IReadOnlyDictionary<string, string> values)
    {
        return new InfrastructureSettingsResponse(
            new EmailInfrastructureSettings(
                ReadBool(values, AsterErpSettingNames.EmailEnabled),
                Read(values, EmailSettingNames.Smtp.Host),
                ReadInt(values, EmailSettingNames.Smtp.Port),
                Read(values, EmailSettingNames.Smtp.UserName),
                SecretState(values, EmailSettingNames.Smtp.Password),
                Read(values, EmailSettingNames.DefaultFromAddress),
                Read(values, EmailSettingNames.DefaultFromDisplayName),
                ReadBool(values, EmailSettingNames.Smtp.EnableSsl)),
            new SmsInfrastructureSettings(
                ReadBool(values, AsterErpSettingNames.SmsEnabled),
                Read(values, AsterErpSettingNames.SmsProvider) ?? "Null",
                Read(values, AsterErpSettingNames.SmsAliyunAccessKeyId),
                SecretState(values, AsterErpSettingNames.SmsAliyunAccessKeySecret),
                Read(values, AsterErpSettingNames.SmsAliyunSignName),
                Read(values, AsterErpSettingNames.SmsAliyunTemplateCode),
                Read(values, AsterErpSettingNames.SmsAliyunTemplateParamName) ?? "content",
                Read(values, AsterErpSettingNames.SmsTencentSecretId),
                SecretState(values, AsterErpSettingNames.SmsTencentSecretKey),
                Read(values, AsterErpSettingNames.SmsTencentSdkAppId),
                Read(values, AsterErpSettingNames.SmsTencentSignName),
                Read(values, AsterErpSettingNames.SmsTencentTemplateId),
                Read(values, AsterErpSettingNames.SmsTencentRegion) ?? "ap-guangzhou"),
            new ObjectStorageInfrastructureSettings(
                Read(values, AsterErpSettingNames.ObjectStorageProvider) ?? "FileSystem",
                Read(values, AsterErpSettingNames.ObjectStorageFileSystemBasePath) ?? "./data/uploads",
                ReadBool(values, AsterErpSettingNames.ObjectStorageFileSystemAppendContainerName),
                Read(values, AsterErpSettingNames.ObjectStorageAliyunEndpoint),
                Read(values, AsterErpSettingNames.ObjectStorageAliyunBucketName),
                Read(values, AsterErpSettingNames.ObjectStorageAliyunAccessKeyId),
                SecretState(values, AsterErpSettingNames.ObjectStorageAliyunAccessKeySecret),
                Read(values, AsterErpSettingNames.ObjectStorageMinioEndpoint),
                Read(values, AsterErpSettingNames.ObjectStorageMinioBucketName),
                Read(values, AsterErpSettingNames.ObjectStorageMinioAccessKey),
                SecretState(values, AsterErpSettingNames.ObjectStorageMinioSecretKey),
                ReadBool(values, AsterErpSettingNames.ObjectStorageMinioWithSsl)),
            new CacheInfrastructureSettings(
                Read(values, AsterErpSettingNames.CacheProvider) ?? "Memory",
                SecretState(values, AsterErpSettingNames.CacheRedisConfiguration),
                ReadInt(values, AsterErpSettingNames.CacheDefaultExpirationMinutes) ?? 30),
            new JobsInfrastructureSettings(
                ReadBool(values, AsterErpSettingNames.JobsAbpBackgroundJobsEnabled, true),
                ReadBool(values, AsterErpSettingNames.JobsMessagingJobsEnabled),
                ReadInt(values, AsterErpSettingNames.JobsTestTimeoutSeconds) ?? 10),
            new AuditInfrastructureSettings(
                ReadBool(values, AsterErpSettingNames.AuditOperationLogEnabled, true),
                ReadBool(values, AsterErpSettingNames.AuditCaptureQueryString, true),
                ReadInt(values, AsterErpSettingNames.AuditQueueCapacity) ?? 2048));
    }

    private static ISugarQueryable<SystemMessageSendLogEntity> ApplyMessageLogFilters(
        ISugarQueryable<SystemMessageSendLogEntity> query,
        MessageSendLogQuery request)
    {
        if (request.StartTime.HasValue)
        {
            query = query.Where(item => item.CreatedTime >= request.StartTime.Value);
        }

        if (request.EndTime.HasValue)
        {
            query = query.Where(item => item.CreatedTime <= request.EndTime.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Channel))
        {
            var channel = request.Channel.Trim();
            query = query.Where(item => item.Channel == channel);
        }

        if (!string.IsNullOrWhiteSpace(request.Provider))
        {
            var provider = request.Provider.Trim();
            query = query.Where(item => item.Provider == provider);
        }

        if (!string.IsNullOrWhiteSpace(request.Result))
        {
            var result = request.Result.Trim();
            query = query.Where(item => item.Result == result);
        }

        if (!string.IsNullOrWhiteSpace(request.TraceId))
        {
            var traceId = request.TraceId.Trim();
            query = query.Where(item => item.TraceId.Contains(traceId));
        }

        return query;
    }

    private async Task<string?> ValidateExternalObjectStorageSettingsAsync(string provider, CancellationToken cancellationToken)
    {
        var values = await LoadValuesAsync(cancellationToken);
        if (provider.Equals("Minio", StringComparison.OrdinalIgnoreCase))
        {
            return MissingRequired(values, "MinIO",
                AsterErpSettingNames.ObjectStorageMinioEndpoint,
                AsterErpSettingNames.ObjectStorageMinioBucketName,
                AsterErpSettingNames.ObjectStorageMinioAccessKey,
                AsterErpSettingNames.ObjectStorageMinioSecretKey);
        }

        if (provider.Equals("Aliyun", StringComparison.OrdinalIgnoreCase))
        {
            return MissingRequired(values, "阿里云 OSS",
                AsterErpSettingNames.ObjectStorageAliyunEndpoint,
                AsterErpSettingNames.ObjectStorageAliyunBucketName,
                AsterErpSettingNames.ObjectStorageAliyunAccessKeyId,
                AsterErpSettingNames.ObjectStorageAliyunAccessKeySecret);
        }

        return null;
    }

    private static string? MissingRequired(
        IReadOnlyDictionary<string, string> values,
        string provider,
        params string[] keys)
    {
        var missing = keys.Where(key => string.IsNullOrWhiteSpace(Read(values, key))).ToList();
        return missing.Count == 0
            ? null
            : $"{provider} 对象存储配置不完整：{string.Join(", ", missing)}";
    }

    private static MessageSendLogResponse MapLog(SystemMessageSendLogEntity entity)
    {
        return new MessageSendLogResponse(
            entity.Id,
            entity.Channel,
            entity.Provider,
            entity.MaskedTarget,
            entity.TraceId,
            entity.CorrelationId,
            entity.Result,
            entity.ErrorSummary,
            entity.DurationMs,
            entity.CreatedTime);
    }

    private static ISugarQueryable<SystemMessageSendLogEntity> ApplyDefaultLogSort(ISugarQueryable<SystemMessageSendLogEntity> query) =>
        query.OrderBy(item => item.CreatedTime, OrderByType.Desc);

    private static InfrastructureTestResult ToTestResult(AsterErpMessageSendResult result, long startedAt)
    {
        return new InfrastructureTestResult(
            result.Succeeded,
            result.Provider,
            result.TraceId,
            result.Message ?? result.ErrorMessage ?? (result.Succeeded ? "测试成功" : "测试失败"),
            (long)Math.Round(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds));
    }

    private static InfrastructureTestResult BuildTestResult(bool success, string provider, string traceId, string message, long startedAt)
    {
        return new InfrastructureTestResult(
            success,
            provider,
            traceId,
            message,
            (long)Math.Round(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds));
    }

    private static string ResolveTraceId()
    {
        return Activity.Current?.Id ?? Guid.NewGuid().ToString("N");
    }

    private static SecretSettingState SecretState(IReadOnlyDictionary<string, string> values, string key) =>
        new(!string.IsNullOrWhiteSpace(Read(values, key)));

    private static string? Read(IReadOnlyDictionary<string, string> values, string key)
    {
        if (values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return InfrastructureSettingCatalog.Get(key).DefaultValue;
    }

    private static bool ReadBool(IReadOnlyDictionary<string, string> values, string key, bool defaultValue = false)
    {
        var value = Read(values, key);
        return bool.TryParse(value, out var result) ? result : defaultValue;
    }

    private static int? ReadInt(IReadOnlyDictionary<string, string> values, string key)
    {
        var value = Read(values, key);
        return int.TryParse(value, out var result) ? result : null;
    }

    private static string NormalizeProvider(string value, IReadOnlySet<string> allowedProviders, string errorMessage)
    {
        var provider = value.Trim();
        if (!allowedProviders.Contains(provider))
        {
            throw new ValidationException(errorMessage, ErrorCodes.ParameterInvalid);
        }

        return provider;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? ToString(bool? value) => value?.ToString().ToLowerInvariant();

    private static string? ToString(int? value) => value?.ToString();

    private static string? ToString(int? value, int min, int max)
    {
        if (!value.HasValue)
        {
            return null;
        }

        if (value.Value < min || value.Value > max)
        {
            throw new ValidationException($"数值必须在 {min} 到 {max} 之间", ErrorCodes.ParameterInvalid);
        }

        return value.Value.ToString();
    }
}

