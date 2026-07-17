using System.Diagnostics;
using AsterERP.Api.Infrastructure.Abp.Settings;
using Volo.Abp.Emailing;
using Volo.Abp.EventBus;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Settings;
using Volo.Abp.Sms;

namespace AsterERP.Api.Infrastructure.Messaging;

public sealed class AsterErpMessagingService(
    IEmailSender emailSender,
    ISmsSender smsSender,
    ISettingProvider settingProvider,
    ILocalEventBus localEventBus,
    ILogger<AsterErpMessagingService> logger) : IAsterErpMessagingService
{
    private const string MailKitProvider = "ABP.MailKit";
    private const string AbpSmsProvider = "ABP.Sms";

    public async Task<AsterErpMessageSendResult> SendEmailAsync(
        AsterErpEmailMessage message,
        CancellationToken cancellationToken = default)
    {
        var traceId = ResolveTraceId();
        var startedAt = Stopwatch.GetTimestamp();
        cancellationToken.ThrowIfCancellationRequested();

        if (!await IsEnabledAsync(AsterErpSettingNames.EmailEnabled))
        {
            return await CompleteAsync(
                AsterErpMessageChannels.Email,
                MailKitProvider,
                message.To,
                traceId,
                false,
                "邮件发送未启用或未配置",
                startedAt,
                cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(message.To))
        {
            return await CompleteAsync(
                AsterErpMessageChannels.Email,
                MailKitProvider,
                message.To,
                traceId,
                false,
                "邮件接收地址不能为空",
                startedAt,
                cancellationToken);
        }

        try
        {
            await emailSender.SendAsync(message.To.Trim(), message.Subject.Trim(), message.Body, message.IsBodyHtml);
            logger.LogInformation(
                "Email sent through {Provider}: to={To}, traceId={TraceId}",
                MailKitProvider,
                message.To,
                traceId);
            return await CompleteAsync(
                AsterErpMessageChannels.Email,
                MailKitProvider,
                message.To,
                traceId,
                true,
                "邮件发送成功",
                startedAt,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Email sending failed through {Provider}: to={To}, traceId={TraceId}",
                MailKitProvider,
                message.To,
                traceId);
            return await CompleteAsync(
                AsterErpMessageChannels.Email,
                MailKitProvider,
                message.To,
                traceId,
                false,
                ex.Message,
                startedAt,
                cancellationToken);
        }
    }

    public async Task<AsterErpMessageSendResult> SendSmsAsync(
        AsterErpSmsMessage message,
        CancellationToken cancellationToken = default)
    {
        var traceId = ResolveTraceId();
        var startedAt = Stopwatch.GetTimestamp();
        cancellationToken.ThrowIfCancellationRequested();

        if (!await IsEnabledAsync(AsterErpSettingNames.SmsEnabled))
        {
            return await CompleteAsync(
                AsterErpMessageChannels.Sms,
                AbpSmsProvider,
                message.PhoneNumber,
                traceId,
                false,
                "短信发送未启用或未配置",
                startedAt,
                cancellationToken);
        }

        var provider = await settingProvider.GetOrNullAsync(AsterErpSettingNames.SmsProvider);
        if (string.IsNullOrWhiteSpace(provider) || provider.Equals("Null", StringComparison.OrdinalIgnoreCase))
        {
            return await CompleteAsync(
                AsterErpMessageChannels.Sms,
                AbpSmsProvider,
                message.PhoneNumber,
                traceId,
                false,
                "短信 Provider 未配置",
                startedAt,
                cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(message.PhoneNumber))
        {
            return await CompleteAsync(
                AsterErpMessageChannels.Sms,
                provider,
                message.PhoneNumber,
                traceId,
                false,
                "短信接收手机号不能为空",
                startedAt,
                cancellationToken);
        }

        try
        {
            await smsSender.SendAsync(new SmsMessage(message.PhoneNumber.Trim(), message.Text));
            logger.LogInformation(
                "SMS sent through {Provider}: phone={PhoneNumber}, traceId={TraceId}",
                provider,
                message.PhoneNumber,
                traceId);
            return await CompleteAsync(
                AsterErpMessageChannels.Sms,
                provider,
                message.PhoneNumber,
                traceId,
                true,
                "短信发送成功",
                startedAt,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "SMS sending failed through {Provider}: phone={PhoneNumber}, traceId={TraceId}",
                provider,
                message.PhoneNumber,
                traceId);
            return await CompleteAsync(
                AsterErpMessageChannels.Sms,
                provider,
                message.PhoneNumber,
                traceId,
                false,
                ex.Message,
                startedAt,
                cancellationToken);
        }
    }

    private async Task<AsterErpMessageSendResult> CompleteAsync(
        string channel,
        string provider,
        string? target,
        string traceId,
        bool success,
        string message,
        long startedAt,
        CancellationToken cancellationToken)
    {
        var durationMs = (long)Math.Round(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        try
        {
            await localEventBus.PublishAsync(new MessageSendCompletedEvent(
                new MessageSendLogWriteRequest(
                    channel,
                    provider,
                    target,
                    traceId,
                    Activity.Current?.Id ?? traceId,
                    success,
                    success ? null : message,
                    durationMs)),
                onUnitOfWorkComplete: false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to write message send log for {Channel} traceId={TraceId}", channel, traceId);
        }

        return success
            ? AsterErpMessageSendResult.Success(provider, traceId, message)
            : AsterErpMessageSendResult.Failed(provider, traceId, message);
    }

    private async Task<bool> IsEnabledAsync(string settingName)
    {
        var rawValue = await settingProvider.GetOrNullAsync(settingName);
        return bool.TryParse(rawValue, out var enabled) && enabled;
    }

    private string ResolveTraceId()
    {
        return Activity.Current?.Id ?? Guid.NewGuid().ToString("N");
    }
}
