using System.Text.Json;
using AsterERP.Api.Application.Im;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Messaging;
using AsterERP.Api.Modules.System.Organizations;
using AsterERP.Api.Modules.System.Roles;
using AsterERP.Api.Modules.System.Users;
using AsterERP.Api.Modules.Workflows;
using AsterERP.Contracts.Workflows;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Volo.Abp.Timing;

namespace AsterERP.Api.Application.Workflows;

public sealed class WorkflowNotificationAppService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    IClock clock,
    IImConversationService imConversationService,
    IAsterErpMessagingService messagingService) : IWorkflowNotificationAppService
{
    private const int MaxPageSize = 100;
    private const string PendingStatus = "Pending";
    private const string SendingStatus = "Sending";
    private const string SentStatus = "Sent";
    private const string FailedStatus = "Failed";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<GridPageResult<WorkflowNotificationChannelResponse>> GetChannelsAsync(
        WorkflowNotificationQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = NormalizePage(query);
        var total = new RefAsync<int>();
        var items = await databaseAccessor.GetCurrentDb().Queryable<WorkflowNotificationChannelEntity>()
            .Where(item => !item.IsDeleted)
            .WhereIF(!string.IsNullOrWhiteSpace(query.TenantId), item => item.TenantId == query.TenantId)
            .WhereIF(!string.IsNullOrWhiteSpace(query.AppCode), item => item.AppCode == query.AppCode)
            .WhereIF(!string.IsNullOrWhiteSpace(query.Keyword), item =>
                item.ChannelName.Contains(query.Keyword!) || item.ChannelCode.Contains(query.Keyword!) || item.ChannelType.Contains(query.Keyword!))
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .ToPageListAsync(page.PageIndex, page.PageSize, total, cancellationToken);

        return new GridPageResult<WorkflowNotificationChannelResponse>
        {
            Total = total.Value,
            Items = items.Select(MapChannel).ToList()
        };
    }

    public async Task<WorkflowNotificationChannelResponse> SaveChannelAsync(
        WorkflowNotificationChannelUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = string.IsNullOrWhiteSpace(request.Id)
            ? new WorkflowNotificationChannelEntity()
            : await RequireChannelAsync(request.Id, cancellationToken);

        entity.TenantId = Normalize(request.TenantId, currentUser.GetAsterErpTenantId(), "租户不能为空");
        entity.AppCode = Normalize(request.AppCode, currentUser.GetAsterErpAppCode(), "应用不能为空").ToUpperInvariant();
        entity.ChannelCode = Normalize(request.ChannelCode, null, "渠道编码不能为空");
        entity.ChannelName = Normalize(request.ChannelName, null, "渠道名称不能为空");
        entity.ChannelType = Normalize(request.ChannelType, null, "渠道类型不能为空");
        entity.IsEnabled = request.IsEnabled;
        entity.ConfigJson = NormalizeOptionalJson(request.ConfigJson, "渠道配置 JSON 格式不正确");
        entity.FailurePolicy = NormalizeOptional(request.FailurePolicy, "ignore");
        Touch(entity);

        if (string.IsNullOrWhiteSpace(request.Id))
        {
            await databaseAccessor.GetCurrentDb().Insertable(entity).ExecuteCommandAsync(cancellationToken);
        }
        else
        {
            await databaseAccessor.GetCurrentDb().Updateable(entity).ExecuteCommandAsync(cancellationToken);
        }

        return MapChannel(entity);
    }

    public async Task DeleteChannelAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await RequireChannelAsync(id, cancellationToken);
        SoftDelete(entity);
        await databaseAccessor.GetCurrentDb().Updateable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task<GridPageResult<WorkflowMessageTemplateResponse>> GetTemplatesAsync(
        WorkflowNotificationQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = NormalizePage(query);
        var total = new RefAsync<int>();
        var items = await databaseAccessor.GetCurrentDb().Queryable<WorkflowMessageTemplateEntity>()
            .Where(item => !item.IsDeleted)
            .WhereIF(!string.IsNullOrWhiteSpace(query.TenantId), item => item.TenantId == query.TenantId)
            .WhereIF(!string.IsNullOrWhiteSpace(query.AppCode), item => item.AppCode == query.AppCode)
            .WhereIF(!string.IsNullOrWhiteSpace(query.Keyword), item =>
                item.TemplateName.Contains(query.Keyword!) || item.TemplateCode.Contains(query.Keyword!) || item.ChannelType.Contains(query.Keyword!))
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .ToPageListAsync(page.PageIndex, page.PageSize, total, cancellationToken);

        return new GridPageResult<WorkflowMessageTemplateResponse>
        {
            Total = total.Value,
            Items = items.Select(MapTemplate).ToList()
        };
    }

    public async Task<WorkflowMessageTemplateResponse> SaveTemplateAsync(
        WorkflowMessageTemplateUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = string.IsNullOrWhiteSpace(request.Id)
            ? new WorkflowMessageTemplateEntity()
            : await RequireTemplateAsync(request.Id, cancellationToken);

        entity.TenantId = Normalize(request.TenantId, currentUser.GetAsterErpTenantId(), "租户不能为空");
        entity.AppCode = Normalize(request.AppCode, currentUser.GetAsterErpAppCode(), "应用不能为空").ToUpperInvariant();
        entity.TemplateCode = Normalize(request.TemplateCode, null, "模板编码不能为空");
        entity.TemplateName = Normalize(request.TemplateName, null, "模板名称不能为空");
        entity.ChannelType = Normalize(request.ChannelType, null, "渠道类型不能为空");
        entity.SubjectTemplate = NormalizeNullable(request.SubjectTemplate);
        entity.BodyTemplate = Normalize(request.BodyTemplate, null, "模板内容不能为空");
        entity.VariablesJson = NormalizeOptionalJson(request.VariablesJson, "模板变量 JSON 格式不正确");
        entity.IsEnabled = request.IsEnabled;
        Touch(entity);

        if (string.IsNullOrWhiteSpace(request.Id))
        {
            await databaseAccessor.GetCurrentDb().Insertable(entity).ExecuteCommandAsync(cancellationToken);
        }
        else
        {
            await databaseAccessor.GetCurrentDb().Updateable(entity).ExecuteCommandAsync(cancellationToken);
        }

        return MapTemplate(entity);
    }

    public async Task DeleteTemplateAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await RequireTemplateAsync(id, cancellationToken);
        SoftDelete(entity);
        await databaseAccessor.GetCurrentDb().Updateable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task<GridPageResult<WorkflowNodeNotificationRuleResponse>> GetRulesAsync(
        WorkflowNotificationQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = NormalizePage(query);
        var total = new RefAsync<int>();
        var items = await databaseAccessor.GetCurrentDb().Queryable<WorkflowNodeNotificationRuleEntity>()
            .Where(item => !item.IsDeleted)
            .WhereIF(!string.IsNullOrWhiteSpace(query.TenantId), item => item.TenantId == query.TenantId)
            .WhereIF(!string.IsNullOrWhiteSpace(query.AppCode), item => item.AppCode == query.AppCode)
            .WhereIF(!string.IsNullOrWhiteSpace(query.Status), item => item.Trigger == query.Status)
            .WhereIF(!string.IsNullOrWhiteSpace(query.Keyword), item =>
                item.NodeId.Contains(query.Keyword!) ||
                item.Trigger.Contains(query.Keyword!) ||
                item.TemplateCode.Contains(query.Keyword!) ||
                item.ProcessDefinitionKey!.Contains(query.Keyword!))
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .ToPageListAsync(page.PageIndex, page.PageSize, total, cancellationToken);

        return new GridPageResult<WorkflowNodeNotificationRuleResponse>
        {
            Total = total.Value,
            Items = items.Select(MapRule).ToList()
        };
    }

    public async Task<WorkflowNodeNotificationRuleResponse> SaveRuleAsync(
        WorkflowNodeNotificationRuleUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = string.IsNullOrWhiteSpace(request.Id)
            ? new WorkflowNodeNotificationRuleEntity()
            : await RequireRuleAsync(request.Id, cancellationToken);

        var channelCodes = request.ChannelCodes?.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? [];
        if (channelCodes.Count == 0)
        {
            throw new ValidationException("通知渠道不能为空", ErrorCodes.ParameterInvalid);
        }

        entity.TenantId = Normalize(request.TenantId, currentUser.GetAsterErpTenantId(), "租户不能为空");
        entity.AppCode = Normalize(request.AppCode, currentUser.GetAsterErpAppCode(), "应用不能为空").ToUpperInvariant();
        entity.ModelId = NormalizeNullable(request.ModelId);
        entity.ProcessDefinitionId = NormalizeNullable(request.ProcessDefinitionId);
        entity.ProcessDefinitionKey = NormalizeNullable(request.ProcessDefinitionKey);
        entity.NodeId = Normalize(request.NodeId, null, "节点不能为空");
        entity.Trigger = Normalize(request.Trigger, null, "触发时机不能为空");
        entity.ReceiverType = Normalize(request.ReceiverType, null, "接收人类型不能为空");
        entity.ReceiverValue = NormalizeNullable(request.ReceiverValue);
        entity.ChannelCodesJson = JsonSerializer.Serialize(channelCodes, JsonOptions);
        entity.TemplateCode = Normalize(request.TemplateCode, null, "模板不能为空");
        entity.ConditionJson = NormalizeOptionalJson(request.ConditionJson, "通知条件 JSON 格式不正确");
        entity.FailurePolicy = NormalizeOptional(request.FailurePolicy, "ignore");
        entity.IsEnabled = request.IsEnabled;
        Touch(entity);

        if (string.IsNullOrWhiteSpace(request.Id))
        {
            await databaseAccessor.GetCurrentDb().Insertable(entity).ExecuteCommandAsync(cancellationToken);
        }
        else
        {
            await databaseAccessor.GetCurrentDb().Updateable(entity).ExecuteCommandAsync(cancellationToken);
        }

        return MapRule(entity);
    }

    public async Task DeleteRuleAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await RequireRuleAsync(id, cancellationToken);
        SoftDelete(entity);
        await databaseAccessor.GetCurrentDb().Updateable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task<GridPageResult<WorkflowNotificationTaskResponse>> GetTasksAsync(
        WorkflowNotificationQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = NormalizePage(query);
        var total = new RefAsync<int>();
        var items = await BuildTaskQuery(query)
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .ToPageListAsync(page.PageIndex, page.PageSize, total, cancellationToken);

        return new GridPageResult<WorkflowNotificationTaskResponse>
        {
            Total = total.Value,
            Items = items.Select(MapTask).ToList()
        };
    }

    public async Task<GridPageResult<WorkflowNotificationLogResponse>> GetLogsAsync(
        WorkflowNotificationQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = NormalizePage(query);
        var total = new RefAsync<int>();
        var items = await databaseAccessor.GetCurrentDb().Queryable<WorkflowNotificationLogEntity>()
            .Where(item => !item.IsDeleted)
            .WhereIF(!string.IsNullOrWhiteSpace(query.Status), item => item.Result == query.Status)
            .WhereIF(!string.IsNullOrWhiteSpace(query.ProcessInstanceId), item => item.ProcessInstanceId == query.ProcessInstanceId)
            .WhereIF(!string.IsNullOrWhiteSpace(query.WorkflowTaskId), item => item.WorkflowTaskId == query.WorkflowTaskId)
            .WhereIF(!string.IsNullOrWhiteSpace(query.Keyword), item =>
                item.ChannelCode.Contains(query.Keyword!) || item.ReceiverUserId!.Contains(query.Keyword!) || item.EventName.Contains(query.Keyword!))
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .ToPageListAsync(page.PageIndex, page.PageSize, total, cancellationToken);

        return new GridPageResult<WorkflowNotificationLogResponse>
        {
            Total = total.Value,
            Items = items.Select(MapLog).ToList()
        };
    }

    public async Task<WorkflowNotificationPreviewResponse> PreviewReceiversAsync(
        WorkflowNotificationPreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        var userIds = await ResolveReceiversAsync(
            request.ReceiverType,
            request.ReceiverValue,
            request.ProcessInstanceId,
            request.WorkflowTaskId,
            request.TenantId,
            request.AppCode,
            cancellationToken);
        var names = await ResolveUserNamesAsync(userIds, cancellationToken);

        return new WorkflowNotificationPreviewResponse(
            userIds,
            userIds.Select(item => names.TryGetValue(item, out var name) ? name : item).ToList());
    }

    public async Task<WorkflowTemplatePreviewResponse> PreviewTemplateAsync(
        WorkflowTemplatePreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        var template = await databaseAccessor.GetCurrentDb().Queryable<WorkflowMessageTemplateEntity>()
            .FirstAsync(item => !item.IsDeleted && item.IsEnabled && item.TemplateCode == request.TemplateCode, cancellationToken)
            ?? throw new NotFoundException("通知模板不存在", ErrorCodes.ParameterInvalid);

        return RenderTemplate(template, request.Variables);
    }

    public async Task<WorkflowNotificationTaskResponse> TestSendAsync(
        WorkflowNotificationTestSendRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = Normalize(request.TenantId, currentUser.GetAsterErpTenantId(), "租户不能为空");
        var appCode = Normalize(request.AppCode, currentUser.GetAsterErpAppCode(), "应用不能为空").ToUpperInvariant();
        var channel = await RequireChannelByCodeAsync(tenantId, appCode, request.ChannelCode, cancellationToken);
        var template = await RequireTemplateByCodeAsync(tenantId, appCode, request.TemplateCode, cancellationToken);
        var rendered = RenderTemplate(template, request.Variables);
        var entity = new WorkflowNotificationTaskEntity
        {
            TenantId = tenantId,
            AppCode = appCode,
            Trigger = "test-send",
            ChannelCode = channel.ChannelCode,
            TemplateCode = template.TemplateCode,
            ReceiverUserId = Normalize(request.ReceiverUserId, null, "接收用户不能为空"),
            Subject = rendered.Subject,
            Content = rendered.Content,
            VariablesJson = JsonSerializer.Serialize(request.Variables, JsonOptions),
            CreatedBy = currentUser.GetAsterErpUserId()
        };
        await databaseAccessor.GetCurrentDb().Insertable(entity).ExecuteCommandAsync(cancellationToken);
        await SendTaskAsync(entity, channel, cancellationToken);
        return MapTask(entity);
    }

    public async Task QueueAsync(WorkflowNotificationTriggerContext context, CancellationToken cancellationToken = default)
    {
        var trigger = Normalize(context.Trigger, null, "触发时机不能为空");
        var tenantId = Normalize(context.TenantId, currentUser.GetAsterErpTenantId(), "租户不能为空");
        var appCode = Normalize(context.AppCode, currentUser.GetAsterErpAppCode(), "应用不能为空").ToUpperInvariant();

        var rules = await databaseAccessor.GetCurrentDb().Queryable<WorkflowNodeNotificationRuleEntity>()
            .Where(item =>
                !item.IsDeleted &&
                item.IsEnabled &&
                item.TenantId == tenantId &&
                item.AppCode == appCode &&
                item.Trigger == trigger)
            .WhereIF(!string.IsNullOrWhiteSpace(context.NodeId), item => item.NodeId == context.NodeId || item.NodeId == "*")
            .WhereIF(!string.IsNullOrWhiteSpace(context.ModelId), item => item.ModelId == null || item.ModelId == context.ModelId)
            .WhereIF(!string.IsNullOrWhiteSpace(context.ProcessDefinitionId), item => item.ProcessDefinitionId == null || item.ProcessDefinitionId == context.ProcessDefinitionId)
            .WhereIF(!string.IsNullOrWhiteSpace(context.ProcessDefinitionKey), item => item.ProcessDefinitionKey == null || item.ProcessDefinitionKey == context.ProcessDefinitionKey)
            .ToListAsync(cancellationToken);
        if (rules.Count == 0)
        {
            return;
        }

        var templateCodes = rules.Select(item => item.TemplateCode).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var channelCodes = rules.SelectMany(item => ParseStringArray(item.ChannelCodesJson)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var templates = await databaseAccessor.GetCurrentDb().Queryable<WorkflowMessageTemplateEntity>()
            .Where(item => !item.IsDeleted && item.IsEnabled && item.TenantId == tenantId && item.AppCode == appCode && templateCodes.Contains(item.TemplateCode))
            .ToListAsync(cancellationToken);
        var channels = await databaseAccessor.GetCurrentDb().Queryable<WorkflowNotificationChannelEntity>()
            .Where(item => !item.IsDeleted && item.IsEnabled && item.TenantId == tenantId && item.AppCode == appCode && channelCodes.Contains(item.ChannelCode))
            .ToListAsync(cancellationToken);
        var templateMap = templates.ToDictionary(item => item.TemplateCode, StringComparer.OrdinalIgnoreCase);
        var channelMap = channels.ToDictionary(item => item.ChannelCode, StringComparer.OrdinalIgnoreCase);
        var tasks = new List<WorkflowNotificationTaskEntity>();

        foreach (var rule in rules)
        {
            if (!templateMap.TryGetValue(rule.TemplateCode, out var template))
            {
                continue;
            }

            var receiverIds = await ResolveReceiversAsync(
                rule.ReceiverType,
                rule.ReceiverValue,
                context.ProcessInstanceId,
                context.WorkflowTaskId,
                tenantId,
                appCode,
                cancellationToken);
            foreach (var channelCode in ParseStringArray(rule.ChannelCodesJson))
            {
                if (!channelMap.TryGetValue(channelCode, out var channel))
                {
                    continue;
                }

                var rendered = RenderTemplate(template, MergeVariables(context, rule, channel));
                tasks.AddRange(receiverIds.Select(receiverId => new WorkflowNotificationTaskEntity
                {
                    TenantId = tenantId,
                    AppCode = appCode,
                    RuleId = rule.Id,
                    ProcessInstanceId = context.ProcessInstanceId,
                    WorkflowTaskId = context.WorkflowTaskId,
                    NodeId = context.NodeId,
                    Trigger = trigger,
                    ChannelCode = channel.ChannelCode,
                    TemplateCode = template.TemplateCode,
                    ReceiverUserId = receiverId,
                    Subject = rendered.Subject,
                    Content = rendered.Content,
                    VariablesJson = JsonSerializer.Serialize(context.Variables, JsonOptions),
                    CreatedBy = context.CurrentUserId ?? currentUser.GetAsterErpUserId()
                }));
            }
        }

        if (tasks.Count > 0)
        {
            await databaseAccessor.GetCurrentDb().Insertable(tasks).ExecuteCommandAsync(cancellationToken);
        }
    }

    public async Task<int> ProcessDueTasksAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var normalizedBatchSize = Math.Clamp(batchSize, 1, 100);
        var now = clock.Now;
        var tasks = await databaseAccessor.GetCurrentDb().Queryable<WorkflowNotificationTaskEntity>()
            .Where(item => !item.IsDeleted && (item.Status == PendingStatus || item.Status == FailedStatus) && item.DueAt <= now && item.RetryCount < item.MaxRetryCount)
            .OrderBy(item => item.DueAt, OrderByType.Asc)
            .Take(normalizedBatchSize)
            .ToListAsync(cancellationToken);
        if (tasks.Count == 0)
        {
            return 0;
        }

        var tenantIds = tasks.Select(item => item.TenantId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var appCodes = tasks.Select(item => item.AppCode).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var channelCodes = tasks.Select(item => item.ChannelCode).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var channels = await databaseAccessor.GetCurrentDb().Queryable<WorkflowNotificationChannelEntity>()
            .Where(item => !item.IsDeleted && item.IsEnabled && tenantIds.Contains(item.TenantId) && appCodes.Contains(item.AppCode) && channelCodes.Contains(item.ChannelCode))
            .ToListAsync(cancellationToken);
        var channelMap = channels.ToDictionary(item => $"{item.TenantId}:{item.AppCode}:{item.ChannelCode}", StringComparer.OrdinalIgnoreCase);
        var processed = 0;

        foreach (var task in tasks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = $"{task.TenantId}:{task.AppCode}:{task.ChannelCode}";
            if (!channelMap.TryGetValue(key, out var channel))
            {
                await MarkFailedAsync(task, "通知渠道未启用或不存在", null, null, cancellationToken);
                processed++;
                continue;
            }

            await SendTaskAsync(task, channel, cancellationToken);
            processed++;
        }

        return processed;
    }

    public async Task<IReadOnlyList<WorkflowNotificationTaskResponse>> GetInstanceNotificationsAsync(
        string processInstanceId,
        CancellationToken cancellationToken = default)
    {
        var items = await databaseAccessor.GetCurrentDb().Queryable<WorkflowNotificationTaskEntity>()
            .Where(item => !item.IsDeleted && item.ProcessInstanceId == processInstanceId)
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .Take(100)
            .ToListAsync(cancellationToken);
        return items.Select(MapTask).ToList();
    }

    private ISugarQueryable<WorkflowNotificationTaskEntity> BuildTaskQuery(WorkflowNotificationQuery query)
    {
        return databaseAccessor.GetCurrentDb().Queryable<WorkflowNotificationTaskEntity>()
            .Where(item => !item.IsDeleted)
            .WhereIF(!string.IsNullOrWhiteSpace(query.TenantId), item => item.TenantId == query.TenantId)
            .WhereIF(!string.IsNullOrWhiteSpace(query.AppCode), item => item.AppCode == query.AppCode)
            .WhereIF(!string.IsNullOrWhiteSpace(query.Status), item => item.Status == query.Status)
            .WhereIF(!string.IsNullOrWhiteSpace(query.ProcessInstanceId), item => item.ProcessInstanceId == query.ProcessInstanceId)
            .WhereIF(!string.IsNullOrWhiteSpace(query.WorkflowTaskId), item => item.WorkflowTaskId == query.WorkflowTaskId)
            .WhereIF(!string.IsNullOrWhiteSpace(query.Keyword), item =>
                item.TemplateCode.Contains(query.Keyword!) ||
                item.ChannelCode.Contains(query.Keyword!) ||
                item.ReceiverUserId!.Contains(query.Keyword!) ||
                item.ProcessInstanceId!.Contains(query.Keyword!));
    }

    private async Task SendTaskAsync(
        WorkflowNotificationTaskEntity task,
        WorkflowNotificationChannelEntity channel,
        CancellationToken cancellationToken)
    {
        task.Status = SendingStatus;
        task.RetryCount++;
        task.UpdatedTime = clock.Now;
        await databaseAccessor.GetCurrentDb().Updateable(task).ExecuteCommandAsync(cancellationToken);

        AsterErpMessageSendResult? messageSendResult = null;
        try
        {
            var logMessage = task.Content;

            if (IsInAppChannel(channel.ChannelType))
            {
                if (string.IsNullOrWhiteSpace(task.ReceiverUserId))
                {
                    throw new ValidationException("站内通知接收用户不能为空", ErrorCodes.ParameterInvalid);
                }

                var conversation = await imConversationService.CreateDirectConversationAsync(task.ReceiverUserId, cancellationToken);
                await imConversationService.SendMessageAsync(
                    conversation.Id,
                    new AsterERP.Contracts.Im.ImSendMessageRequest(
                        string.IsNullOrWhiteSpace(task.Subject) ? task.Content : $"{task.Subject}\n{task.Content}",
                        $"workflow:{task.Id}",
                        "WorkflowNotification",
                        task.AppCode),
                    cancellationToken);
            }
            else if (IsEmailChannel(channel.ChannelType))
            {
                var receiverAddress = await ResolveReceiverAddressAsync(task, channel.ChannelType, cancellationToken);
                messageSendResult = await messagingService.SendEmailAsync(
                    new AsterErpEmailMessage(receiverAddress, task.Subject ?? "AsterERP 通知", task.Content),
                    cancellationToken);
                if (!messageSendResult.Succeeded)
                {
                    throw new ValidationException(messageSendResult.ErrorMessage ?? "邮件发送失败", ErrorCodes.ParameterInvalid);
                }

                logMessage = messageSendResult.Message ?? logMessage;
            }
            else if (IsSmsChannel(channel.ChannelType))
            {
                var receiverAddress = await ResolveReceiverAddressAsync(task, channel.ChannelType, cancellationToken);
                messageSendResult = await messagingService.SendSmsAsync(
                    new AsterErpSmsMessage(receiverAddress, task.Content),
                    cancellationToken);
                if (!messageSendResult.Succeeded)
                {
                    throw new ValidationException(messageSendResult.ErrorMessage ?? "短信发送失败", ErrorCodes.ParameterInvalid);
                }

                logMessage = messageSendResult.Message ?? logMessage;
            }
            else
            {
                throw new ValidationException($"通知渠道 {channel.ChannelType} 尚未配置发送器", ErrorCodes.ParameterInvalid);
            }

            task.Status = SentStatus;
            task.SentAt = clock.Now;
            task.LastError = null;
            await databaseAccessor.GetCurrentDb().Updateable(task).ExecuteCommandAsync(cancellationToken);
            await AddLogAsync(
                task,
                SentStatus,
                logMessage,
                null,
                messageSendResult?.Provider,
                messageSendResult?.TraceId,
                cancellationToken);
        }
        catch (Exception ex)
        {
            await MarkFailedAsync(
                task,
                ex.Message,
                messageSendResult?.Provider,
                messageSendResult?.TraceId,
                cancellationToken);
        }
    }

    private async Task MarkFailedAsync(
        WorkflowNotificationTaskEntity task,
        string errorMessage,
        string? provider,
        string? traceId,
        CancellationToken cancellationToken)
    {
        task.Status = FailedStatus;
        task.LastError = errorMessage;
        task.UpdatedTime = clock.Now;
        await databaseAccessor.GetCurrentDb().Updateable(task).ExecuteCommandAsync(cancellationToken);
        await AddLogAsync(task, FailedStatus, null, errorMessage, provider, traceId, cancellationToken);
    }

    private async Task AddLogAsync(
        WorkflowNotificationTaskEntity task,
        string result,
        string? message,
        string? errorMessage,
        string? provider,
        string? traceId,
        CancellationToken cancellationToken)
    {
        var log = new WorkflowNotificationLogEntity
        {
            NotificationTaskId = task.Id,
            RuleId = task.RuleId,
            ProcessInstanceId = task.ProcessInstanceId,
            WorkflowTaskId = task.WorkflowTaskId,
            ChannelCode = task.ChannelCode,
            ReceiverUserId = task.ReceiverUserId,
            EventName = task.Trigger,
            Result = result,
            Message = message,
            ErrorMessage = errorMessage,
            Provider = provider,
            TraceId = traceId,
            CreatedBy = currentUser.GetAsterErpUserId()
        };
        await databaseAccessor.GetCurrentDb().Insertable(log).ExecuteCommandAsync(cancellationToken);
    }

    private async Task<string> ResolveReceiverAddressAsync(
        WorkflowNotificationTaskEntity task,
        string channelType,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(task.ReceiverAddress))
        {
            return task.ReceiverAddress.Trim();
        }

        if (string.IsNullOrWhiteSpace(task.ReceiverUserId))
        {
            throw new ValidationException("接收用户不能为空", ErrorCodes.ParameterInvalid);
        }

        var users = await databaseAccessor.GetCurrentDb().Queryable<SystemUserEntity>()
            .Where(item => item.Id == task.ReceiverUserId && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken);
        var user = users.FirstOrDefault()
            ?? throw new ValidationException("接收用户不存在", ErrorCodes.ParameterInvalid);

        var receiverAddress = IsEmailChannel(channelType) ? user.Email : user.PhoneNumber;
        if (string.IsNullOrWhiteSpace(receiverAddress))
        {
            var channelName = IsEmailChannel(channelType) ? "邮箱" : "手机号";
            throw new ValidationException($"接收用户未配置{channelName}", ErrorCodes.ParameterInvalid);
        }

        task.ReceiverAddress = receiverAddress.Trim();
        return task.ReceiverAddress;
    }

    private async Task<IReadOnlyList<string>> ResolveReceiversAsync(
        string receiverType,
        string? receiverValue,
        string? processInstanceId,
        string? workflowTaskId,
        string tenantId,
        string appCode,
        CancellationToken cancellationToken)
    {
        var normalized = receiverType.Trim().ToLowerInvariant();
        if (normalized is "currentuser" or "current-approver" or "currentapprover")
        {
            return [currentUser.GetAsterErpUserId()];
        }

        if (normalized is "user" or "specifieduser" or "specified-user")
        {
            return SplitCsv(receiverValue);
        }

        if (normalized is "starter" && !string.IsNullOrWhiteSpace(processInstanceId))
        {
            var instance = await databaseAccessor.GetCurrentDb().Queryable<WorkflowBusinessInstanceEntity>()
                .FirstAsync(item => !item.IsDeleted && item.ProcessInstanceId == processInstanceId, cancellationToken);
            return string.IsNullOrWhiteSpace(instance?.StartedBy) ? [] : [instance.StartedBy];
        }

        if (normalized is "role" or "specifiedrole" or "specified-role")
        {
            var roleIds = SplitCsv(receiverValue);
            if (roleIds.Count == 0)
            {
                return [];
            }

            return await databaseAccessor.GetCurrentDb().Queryable<SystemUserRoleEntity>()
                .Where(item => roleIds.Contains(item.RoleId))
                .Select(item => item.UserId)
                .ToListAsync(cancellationToken);
        }

        if (normalized is "department" or "dept" or "specifieddepartment" or "specified-department")
        {
            var deptIds = SplitCsv(receiverValue);
            if (deptIds.Count == 0)
            {
                return [];
            }

            return (await databaseAccessor.GetCurrentDb().Queryable<SystemUserEmploymentEntity, SystemUserEntity>(
                    (employment, user) => employment.UserId == user.Id)
                .Where((employment, user) =>
                    !employment.IsDeleted &&
                    employment.Status == "Enabled" &&
                    deptIds.Contains(employment.DeptId) &&
                    !user.IsDeleted &&
                    user.Status == "Enabled")
                .Select((employment, user) => user.Id)
                .ToListAsync(cancellationToken))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        if (normalized is "position" or "specifiedposition" or "specified-position")
        {
            var positionIds = SplitCsv(receiverValue);
            if (positionIds.Count == 0)
            {
                return [];
            }

            return (await databaseAccessor.GetCurrentDb().Queryable<SystemUserEmploymentEntity, SystemUserEntity>(
                    (employment, user) => employment.UserId == user.Id)
                .Where((employment, user) =>
                    !employment.IsDeleted &&
                    employment.Status == "Enabled" &&
                    positionIds.Contains(employment.PositionId) &&
                    !user.IsDeleted &&
                    user.Status == "Enabled")
                .Select((employment, user) => user.Id)
                .ToListAsync(cancellationToken))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        if (normalized is "approver" or "task-assignee" or "taskassignee" or "current-task-assignee" or "currenttaskassignee" &&
            !string.IsNullOrWhiteSpace(workflowTaskId))
        {
            var task = await databaseAccessor.GetCurrentDb().Queryable<AsterERP.Workflow.Persistence.Entities.TaskEntity>()
                .FirstAsync(item => item.Id == workflowTaskId, cancellationToken);
            return string.IsNullOrWhiteSpace(task?.Assignee) ? [] : [task.Assignee];
        }

        return [];
    }

    private async Task<Dictionary<string, string>> ResolveUserNamesAsync(
        IReadOnlyList<string> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var users = await databaseAccessor.GetCurrentDb().Queryable<SystemUserEntity>()
            .Where(item => !item.IsDeleted && userIds.Contains(item.Id))
            .Select(item => new { item.Id, item.DisplayName, item.UserName })
            .ToListAsync(cancellationToken);
        return users.ToDictionary(
            item => item.Id,
            item => string.IsNullOrWhiteSpace(item.DisplayName) ? item.UserName : item.DisplayName,
            StringComparer.OrdinalIgnoreCase);
    }

    private async Task<WorkflowNotificationChannelEntity> RequireChannelAsync(string id, CancellationToken cancellationToken)
    {
        return await databaseAccessor.GetCurrentDb().Queryable<WorkflowNotificationChannelEntity>()
            .FirstAsync(item => item.Id == id && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("通知渠道不存在", ErrorCodes.ParameterInvalid);
    }

    private async Task<WorkflowNotificationChannelEntity> RequireChannelByCodeAsync(
        string tenantId,
        string appCode,
        string channelCode,
        CancellationToken cancellationToken)
    {
        return await databaseAccessor.GetCurrentDb().Queryable<WorkflowNotificationChannelEntity>()
            .FirstAsync(item => !item.IsDeleted && item.IsEnabled && item.TenantId == tenantId && item.AppCode == appCode && item.ChannelCode == channelCode, cancellationToken)
            ?? throw new NotFoundException("通知渠道不存在或未启用", ErrorCodes.ParameterInvalid);
    }

    private async Task<WorkflowMessageTemplateEntity> RequireTemplateAsync(string id, CancellationToken cancellationToken)
    {
        return await databaseAccessor.GetCurrentDb().Queryable<WorkflowMessageTemplateEntity>()
            .FirstAsync(item => item.Id == id && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("通知模板不存在", ErrorCodes.ParameterInvalid);
    }

    private async Task<WorkflowMessageTemplateEntity> RequireTemplateByCodeAsync(
        string tenantId,
        string appCode,
        string templateCode,
        CancellationToken cancellationToken)
    {
        return await databaseAccessor.GetCurrentDb().Queryable<WorkflowMessageTemplateEntity>()
            .FirstAsync(item => !item.IsDeleted && item.IsEnabled && item.TenantId == tenantId && item.AppCode == appCode && item.TemplateCode == templateCode, cancellationToken)
            ?? throw new NotFoundException("通知模板不存在或未启用", ErrorCodes.ParameterInvalid);
    }

    private async Task<WorkflowNodeNotificationRuleEntity> RequireRuleAsync(string id, CancellationToken cancellationToken)
    {
        return await databaseAccessor.GetCurrentDb().Queryable<WorkflowNodeNotificationRuleEntity>()
            .FirstAsync(item => item.Id == id && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("通知规则不存在", ErrorCodes.ParameterInvalid);
    }

    private static WorkflowTemplatePreviewResponse RenderTemplate(
        WorkflowMessageTemplateEntity template,
        IReadOnlyDictionary<string, object?> variables)
    {
        return new WorkflowTemplatePreviewResponse(
            RenderTemplateText(template.SubjectTemplate, variables),
            RenderTemplateText(template.BodyTemplate, variables) ?? string.Empty);
    }

    private static string? RenderTemplateText(string? template, IReadOnlyDictionary<string, object?> variables)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return template;
        }

        var result = template;
        foreach (var (key, value) in variables)
        {
            result = result.Replace($"{{{{{key}}}}}", value?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }

    private static Dictionary<string, object?> MergeVariables(
        WorkflowNotificationTriggerContext context,
        WorkflowNodeNotificationRuleEntity rule,
        WorkflowNotificationChannelEntity channel)
    {
        var variables = new Dictionary<string, object?>(context.Variables, StringComparer.OrdinalIgnoreCase)
        {
            ["tenantId"] = context.TenantId,
            ["appCode"] = context.AppCode,
            ["processInstanceId"] = context.ProcessInstanceId,
            ["processName"] = context.ProcessDefinitionKey ?? context.ProcessDefinitionId ?? "流程",
            ["workflowTaskId"] = context.WorkflowTaskId,
            ["nodeId"] = context.NodeId,
            ["nodeName"] = context.NodeId ?? "流程节点",
            ["trigger"] = context.Trigger,
            ["starterUserId"] = context.StarterUserId,
            ["currentUserId"] = context.CurrentUserId,
            ["templateCode"] = rule.TemplateCode,
            ["channelCode"] = channel.ChannelCode
        };
        return variables;
    }

    private static bool IsInAppChannel(string channelType)
    {
        return channelType.Equals("in-app", StringComparison.OrdinalIgnoreCase) ||
               channelType.Equals("system", StringComparison.OrdinalIgnoreCase) ||
               channelType.Equals("message", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsEmailChannel(string channelType)
    {
        return channelType.Equals("email", StringComparison.OrdinalIgnoreCase) ||
               channelType.Equals("mail", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSmsChannel(string channelType)
    {
        return channelType.Equals("sms", StringComparison.OrdinalIgnoreCase) ||
               channelType.Equals("text-message", StringComparison.OrdinalIgnoreCase);
    }

    private static WorkflowNotificationChannelResponse MapChannel(WorkflowNotificationChannelEntity entity) =>
        new(
            entity.Id,
            entity.TenantId,
            entity.AppCode,
            entity.ChannelCode,
            entity.ChannelName,
            entity.ChannelType,
            entity.IsEnabled,
            entity.ConfigJson,
            entity.FailurePolicy,
            entity.CreatedTime,
            entity.UpdatedTime);

    private static WorkflowMessageTemplateResponse MapTemplate(WorkflowMessageTemplateEntity entity) =>
        new(
            entity.Id,
            entity.TenantId,
            entity.AppCode,
            entity.TemplateCode,
            entity.TemplateName,
            entity.ChannelType,
            entity.SubjectTemplate,
            entity.BodyTemplate,
            entity.VariablesJson,
            entity.IsEnabled,
            entity.CreatedTime,
            entity.UpdatedTime);

    private static WorkflowNodeNotificationRuleResponse MapRule(WorkflowNodeNotificationRuleEntity entity) =>
        new(
            entity.Id,
            entity.TenantId,
            entity.AppCode,
            entity.ModelId,
            entity.ProcessDefinitionId,
            entity.ProcessDefinitionKey,
            entity.NodeId,
            entity.Trigger,
            entity.ReceiverType,
            entity.ReceiverValue,
            ParseStringArray(entity.ChannelCodesJson),
            entity.TemplateCode,
            entity.ConditionJson,
            entity.FailurePolicy,
            entity.IsEnabled,
            entity.CreatedTime,
            entity.UpdatedTime);

    private static WorkflowNotificationTaskResponse MapTask(WorkflowNotificationTaskEntity entity) =>
        new(
            entity.Id,
            entity.TenantId,
            entity.AppCode,
            entity.RuleId,
            entity.ProcessInstanceId,
            entity.WorkflowTaskId,
            entity.NodeId,
            entity.Trigger,
            entity.ChannelCode,
            entity.TemplateCode,
            entity.ReceiverUserId,
            entity.ReceiverAddress,
            entity.Subject,
            entity.Content,
            entity.Status,
            entity.RetryCount,
            entity.MaxRetryCount,
            entity.DueAt,
            entity.SentAt,
            entity.LastError,
            entity.CreatedTime);

    private static WorkflowNotificationLogResponse MapLog(WorkflowNotificationLogEntity entity) =>
        new(
            entity.Id,
            entity.NotificationTaskId,
            entity.RuleId,
            entity.ProcessInstanceId,
            entity.WorkflowTaskId,
            entity.ChannelCode,
            entity.ReceiverUserId,
            entity.EventName,
            entity.Result,
            entity.Message,
            entity.ErrorMessage,
            entity.Provider,
            entity.TraceId,
            entity.CreatedTime);

    private static (int PageIndex, int PageSize) NormalizePage(WorkflowNotificationQuery query)
    {
        return (Math.Max(query.PageIndex, 1), Math.Clamp(query.PageSize, 1, MaxPageSize));
    }

    private void Touch(AsterERP.Domain.Common.EntityBase entity)
    {
        if (string.IsNullOrWhiteSpace(entity.CreatedBy))
        {
            entity.CreatedBy = currentUser.GetAsterErpUserId();
        }

        entity.UpdatedBy = currentUser.GetAsterErpUserId();
        entity.UpdatedTime = clock.Now;
    }

    private void SoftDelete(AsterERP.Domain.Common.EntityBase entity)
    {
        entity.IsDeleted = true;
        entity.DeletedBy = currentUser.GetAsterErpUserId();
        entity.DeletedTime = clock.Now;
        entity.UpdatedBy = currentUser.GetAsterErpUserId();
        entity.UpdatedTime = clock.Now;
    }

    private static string Normalize(string? value, string? fallback, string message)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? fallback : value;
        normalized = normalized?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ValidationException(message, ErrorCodes.ParameterInvalid);
        }

        return normalized;
    }

    private static string NormalizeOptional(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? NormalizeOptionalJson(string? value, string message)
    {
        var normalized = NormalizeNullable(value);
        if (normalized is null)
        {
            return null;
        }

        try
        {
            JsonDocument.Parse(normalized).Dispose();
            return normalized;
        }
        catch (JsonException)
        {
            throw new ValidationException(message, ErrorCodes.ParameterInvalid);
        }
    }

    private static IReadOnlyList<string> SplitCsv(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<string> ParseStringArray(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(value, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
