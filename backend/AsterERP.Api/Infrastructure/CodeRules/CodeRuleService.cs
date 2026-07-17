using AsterERP.Shared;
using AsterERP.Api.Infrastructure.Repositories;
using AsterERP.Api.Infrastructure.UnitOfWork;
using AsterERP.Api.Modules.System.CodeRules;

namespace AsterERP.Api.Infrastructure.CodeRules;

public sealed class CodeRuleService(
    IRepository<SystemCodeRuleEntity> codeRuleRepository,
    IRepository<SystemCodeRuleSegmentEntity> codeRuleSegmentRepository,
    IUnitOfWork unitOfWork,
    ILogger<CodeRuleService> logger) : ICodeRuleService
{
    public Task<string> PreviewAsync(string ruleCode, CancellationToken cancellationToken = default)
    {
        return BuildNextCodeAsync(ruleCode, persist: false, cancellationToken);
    }

    public Task<string> GenerateAsync(string ruleCode, CancellationToken cancellationToken = default)
    {
        return BuildNextCodeAsync(ruleCode, persist: true, cancellationToken);
    }

    private Task<string> BuildNextCodeAsync(string ruleCode, bool persist, CancellationToken cancellationToken)
    {
        return unitOfWork.ExecuteAsync(async () =>
        {
            var rule = await codeRuleRepository.FirstOrDefaultAsync(item => item.RuleCode == ruleCode && item.IsEnabled, cancellationToken);
            if (rule is null)
            {
                throw new InvalidOperationException($"Code rule not found: {ruleCode}");
            }

            var segments = await codeRuleSegmentRepository.ListAsync(
                item => item.CodeRuleId == rule.Id && item.IsEnabled,
                cancellationToken: cancellationToken);

            var generatedAt = DateTime.UtcNow;
            var dateKey = ResolveDateKey(rule, segments, generatedAt);
            var nextSequence = ResolveNextSequence(rule, dateKey);
            var code = BuildCode(segments, generatedAt, nextSequence);

            if (persist)
            {
                rule.CurrentDateKey = dateKey;
                rule.CurrentSequence = nextSequence;
                await codeRuleRepository.UpdateAsync(rule, cancellationToken);
            }

            logger.LogInformation("Code rule {RuleCode} generated {Code}", ruleCode, code);
            return code;
        }, cancellationToken);
    }

    private static string? ResolveDateKey(SystemCodeRuleEntity rule, IReadOnlyList<SystemCodeRuleSegmentEntity> segments, DateTime generatedAt)
    {
        if (!ShouldResetSequence(rule.ResetPolicy))
        {
            return rule.CurrentDateKey;
        }

        var dateSegment = segments.FirstOrDefault(item => item.SegmentType.Equals("Date", StringComparison.OrdinalIgnoreCase));
        if (dateSegment is null)
        {
            return generatedAt.ToString("yyyyMMdd");
        }

        var pattern = string.IsNullOrWhiteSpace(dateSegment.SegmentValue) ? "yyyyMMdd" : dateSegment.SegmentValue;
        return generatedAt.ToString(pattern);
    }

    private static int ResolveNextSequence(SystemCodeRuleEntity rule, string? dateKey)
    {
        if (!ShouldResetSequence(rule.ResetPolicy))
        {
            return rule.CurrentSequence + 1;
        }

        if (!string.Equals(rule.CurrentDateKey, dateKey, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return rule.CurrentSequence + 1;
    }

    private static string BuildCode(IReadOnlyList<SystemCodeRuleSegmentEntity> segments, DateTime generatedAt, int sequence)
    {
        if (segments.Count == 0)
        {
            return sequence.ToString("D4");
        }

        var parts = segments
            .OrderBy(item => item.SortOrder)
            .Select(item =>
            {
                if (item.SegmentType.Equals("Static", StringComparison.OrdinalIgnoreCase))
                {
                    return item.SegmentValue ?? string.Empty;
                }

                if (item.SegmentType.Equals("Date", StringComparison.OrdinalIgnoreCase))
                {
                    var pattern = string.IsNullOrWhiteSpace(item.SegmentValue) ? "yyyyMMdd" : item.SegmentValue;
                    return generatedAt.ToString(pattern);
                }

                if (item.SegmentType.Equals("Sequence", StringComparison.OrdinalIgnoreCase))
                {
                    var length = item.SegmentLength > 0 ? item.SegmentLength : 4;
                    return sequence.ToString($"D{length}");
                }

                return item.SegmentValue ?? string.Empty;
            });

        return string.Concat(parts);
    }

    private static bool ShouldResetSequence(string resetPolicy)
    {
        return !resetPolicy.Equals("Never", StringComparison.OrdinalIgnoreCase);
    }
}
