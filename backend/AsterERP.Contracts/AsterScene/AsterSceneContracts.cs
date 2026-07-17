using System.Text.Json;
using AsterERP.Shared;

namespace AsterERP.Contracts.AsterScene;

public sealed class AsterSceneGridQuery
{
    public int PageIndex { get; set; } = 1;

    public int PageSize { get; set; } = 20;

    public string? Keyword { get; set; }

    public string? Status { get; set; }

    public string? ProjectId { get; set; }

    public string? AssetType { get; set; }

    public string? WorkId { get; set; }

    public string? CreatorHandle { get; set; }

    public List<GridFilter> Filters { get; set; } = [];

    public List<GridSort> Sorts { get; set; } = [];
}

public sealed class AsterSceneProjectDto
{
    public string Id { get; set; } = string.Empty;

    public string ProjectCode { get; set; } = string.Empty;

    public string ProjectName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string Visibility { get; set; } = "Private";

    public string Status { get; set; } = "Draft";

    public int CurrentRevision { get; set; }

    public string DocumentHash { get; set; } = string.Empty;

    public string? CoverAssetId { get; set; }

    public string? CurrentPublishCode { get; set; }

    public int PublishedVersion { get; set; }

    public DateTime CreatedTime { get; set; }

    public DateTime? UpdatedTime { get; set; }
}

public sealed class AsterSceneCreateProjectRequest
{
    public string ProjectName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string Visibility { get; set; } = "Private";

    public string? TemplateCode { get; set; }

    public string ClientMutationId { get; set; } = string.Empty;
}

public sealed class AsterSceneUpdateProjectRequest
{
    public string ProjectName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string Visibility { get; set; } = "Private";

    public string? CoverAssetId { get; set; }

    public string ClientMutationId { get; set; } = string.Empty;
}

public sealed class AsterSceneDocumentDto
{
    public AsterSceneProjectDto Project { get; set; } = new();

    public JsonElement Document { get; set; }

    public int Revision { get; set; }

    public string DocumentHash { get; set; } = string.Empty;

    public DateTime SavedAt { get; set; }
}

public sealed class AsterSceneDocumentVersionDto
{
    public string ProjectId { get; set; } = string.Empty;

    public int Revision { get; set; }

    public string DocumentHash { get; set; } = string.Empty;

    public bool IsCurrent { get; set; }

    public string SaveSource { get; set; } = "Manual";

    public string SavedBy { get; set; } = string.Empty;

    public DateTime SavedAt { get; set; }

    public string? ClientMutationId { get; set; }
}

public sealed class AsterSceneSaveDocumentRequest
{
    public JsonElement Document { get; set; }

    public int ExpectedRevision { get; set; }

    public string ClientMutationId { get; set; } = string.Empty;

    public string DocumentHash { get; set; } = string.Empty;

    public string SaveSource { get; set; } = "Manual";
}

public sealed class AsterSceneRestoreDocumentVersionRequest
{
    public int ExpectedRevision { get; set; }

    public string ClientMutationId { get; set; } = string.Empty;
}

public sealed class AsterSceneSaveDocumentResponse
{
    public string ProjectId { get; set; } = string.Empty;

    public int Revision { get; set; }

    public string DocumentHash { get; set; } = string.Empty;

    public DateTime SavedAt { get; set; }

    public string ClientMutationId { get; set; } = string.Empty;
}

public sealed class AsterSceneValidationIssueDto
{
    public string Code { get; set; } = string.Empty;

    public string Severity { get; set; } = "error";

    public string Path { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}

public sealed class AsterSceneValidationResultDto
{
    public bool IsValid { get; set; }

    public List<AsterSceneValidationIssueDto> Errors { get; set; } = [];

    public List<AsterSceneValidationIssueDto> Warnings { get; set; } = [];
}

public sealed class AsterSceneAssetDto
{
    public string Id { get; set; } = string.Empty;

    public string ProjectId { get; set; } = string.Empty;

    public string AssetCode { get; set; } = string.Empty;

    public string AssetType { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string Status { get; set; } = "Ready";

    public int CurrentVersion { get; set; }

    public string? RuntimeUrl { get; set; }

    public string? ThumbnailUrl { get; set; }

    public string? ContentType { get; set; }

    public long? SizeBytes { get; set; }

    public string? Checksum { get; set; }

    public JsonElement? Metadata { get; set; }

    public DateTime CreatedTime { get; set; }
}

public sealed class AsterSceneAssetRegisterRequest
{
    public string ProjectId { get; set; } = string.Empty;

    public string AssetType { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string SourceUrl { get; set; } = string.Empty;

    public string? ContentType { get; set; }

    public long? SizeBytes { get; set; }

    public string? Checksum { get; set; }

    public JsonElement? Metadata { get; set; }

    public string? ClientMutationId { get; set; }
}

public sealed class AsterSceneGeneratedAssetRequest
{
    public string ProjectId { get; set; } = string.Empty;

    public string AssetType { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public JsonElement Payload { get; set; }

    public string? ContentType { get; set; }

    public string? Checksum { get; set; }

    public JsonElement? Metadata { get; set; }

    public string ClientMutationId { get; set; } = string.Empty;
}

public sealed class AsterSceneStartUploadRequest
{
    public string ProjectId { get; set; } = string.Empty;

    public string AssetType { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string? ContentType { get; set; }

    public long SizeBytes { get; set; }

    public string? Checksum { get; set; }

    public int TotalChunks { get; set; } = 1;

    public string? ClientMutationId { get; set; }
}

public sealed class AsterSceneUploadSessionDto
{
    public string UploadId { get; set; } = string.Empty;

    public string ProjectId { get; set; } = string.Empty;

    public string Status { get; set; } = "Pending";

    public int TotalChunks { get; set; }

    public int UploadedChunks { get; set; }

    public long SizeBytes { get; set; }
}

public sealed class AsterSceneCompleteUploadRequest
{
    public JsonElement? Metadata { get; set; }

    public string ClientMutationId { get; set; } = string.Empty;
}

public sealed class AsterSceneJobDto
{
    public string Id { get; set; } = string.Empty;

    public string JobCode { get; set; } = string.Empty;

    public string JobType { get; set; } = string.Empty;

    public string Status { get; set; } = "Pending";

    public int ProgressPercent { get; set; }

    public string? ErrorCode { get; set; }

    public string? ErrorMessage { get; set; }

    public JsonElement? Output { get; set; }

    public DateTime CreatedTime { get; set; }
}

public sealed class AsterScenePublishRequest
{
    public int ExpectedRevision { get; set; }

    public string DocumentHash { get; set; } = string.Empty;

    public string Visibility { get; set; } = "Public";

    public string QualityGateMode { get; set; } = "Strict";

    public string ClientMutationId { get; set; } = string.Empty;
}

public sealed class AsterScenePublishVersionDto
{
    public string Id { get; set; } = string.Empty;

    public string ProjectId { get; set; } = string.Empty;

    public string PublishCode { get; set; } = string.Empty;

    public int Version { get; set; }

    public string Status { get; set; } = "Active";

    public int DocumentRevision { get; set; }

    public string DocumentHash { get; set; } = string.Empty;

    public string Visibility { get; set; } = "Public";

    public DateTime PublishedAt { get; set; }
}

public sealed class AsterScenePublishResponse
{
    public AsterScenePublishVersionDto PublishVersion { get; set; } = new();

    public AsterSceneRuntimeManifestDto Manifest { get; set; } = new();

    public AsterScenePublicWorkDto? PublicWork { get; set; }
}

public sealed class AsterSceneRuntimeManifestDto
{
    public string PublishCode { get; set; } = string.Empty;

    public string DocumentHash { get; set; } = string.Empty;

    public string EntrySceneId { get; set; } = string.Empty;

    public JsonElement Document { get; set; }

    public JsonElement CapabilityPolicy { get; set; }

    public JsonElement AssetVariants { get; set; }

    public JsonElement Preload { get; set; }

    public JsonElement LazyGroups { get; set; }

    public JsonElement Security { get; set; }

    public JsonElement Analytics { get; set; }
}

public sealed class AsterSceneRuntimeEventRequest
{
    public string PublishCode { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;

    public string? SceneId { get; set; }

    public string? HotspotId { get; set; }

    public string ClientEventId { get; set; } = string.Empty;
}

public sealed class AsterSceneRuntimeEventResponse
{
    public string LedgerId { get; set; } = string.Empty;

    public string PublishCode { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;

    public string? SceneId { get; set; }

    public string? HotspotId { get; set; }

    public string ClientEventId { get; set; } = string.Empty;

    public DateTime OccurredAt { get; set; }
}

public sealed class AsterSceneRollbackRequest
{
    public int ExpectedRevision { get; set; }

    public string DocumentHash { get; set; } = string.Empty;

    public string ClientMutationId { get; set; } = string.Empty;
}

public sealed class AsterScenePublicWorkDto
{
    public string Id { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Summary { get; set; }

    public string? CoverAssetId { get; set; }

    public string CreatorHandle { get; set; } = string.Empty;

    public string Visibility { get; set; } = "Public";

    public string Status { get; set; } = "Published";

    public int ViewCount { get; set; }

    public int LikeCount { get; set; }

    public int FavoriteCount { get; set; }

    public int RemixCount { get; set; }

    public DateTime PublishedAt { get; set; }

    public string PublishCode { get; set; } = string.Empty;
}

public sealed class AsterSceneCreatorProfileDto
{
    public string Handle { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? Bio { get; set; }

    public string? AvatarUrl { get; set; }

    public int WorksCount { get; set; }

    public int FollowersCount { get; set; }
}

public sealed class AsterSceneReactionRequest
{
    public string ClientMutationId { get; set; } = string.Empty;
}

public sealed class AsterSceneRemixRequest
{
    public string ProjectName { get; set; } = string.Empty;

    public string ClientMutationId { get; set; } = string.Empty;
}

public sealed class AsterSceneRemixResponse
{
    public AsterSceneProjectDto Project { get; set; } = new();

    public string SourceWorkId { get; set; } = string.Empty;
}

public sealed class AsterSceneSubscriptionPlanDto
{
    public string PlanCode { get; set; } = string.Empty;

    public string PlanName { get; set; } = string.Empty;

    public decimal PriceMonthly { get; set; }

    public int StorageGb { get; set; }

    public int AiCreditsMonthly { get; set; }

    public int PublishedWorks { get; set; }
}

public sealed class AsterSceneSubscribeRequest
{
    public string PlanCode { get; set; } = string.Empty;

    public string ClientMutationId { get; set; } = string.Empty;
}

public sealed class AsterSceneSubscriptionLifecycleRequest
{
    public string ClientMutationId { get; set; } = string.Empty;

    public string? Reason { get; set; }
}

public sealed class AsterSceneSubscriptionDto
{
    public string Id { get; set; } = string.Empty;

    public string PlanCode { get; set; } = string.Empty;

    public string Status { get; set; } = "Active";

    public DateTime StartedAt { get; set; }

    public DateTime? EndsAt { get; set; }
}

public sealed class AsterSceneUsageLedgerDto
{
    public string Id { get; set; } = string.Empty;

    public string UsageType { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public string Unit { get; set; } = string.Empty;

    public string Direction { get; set; } = "Debit";

    public string SourceType { get; set; } = string.Empty;

    public string SourceId { get; set; } = string.Empty;

    public DateTime OccurredAt { get; set; }
}

public sealed class AsterSceneUsageSummaryDto
{
    public string PlanCode { get; set; } = "free";

    public decimal StorageGbUsed { get; set; }

    public decimal StorageGbLimit { get; set; }

    public decimal AiCreditsRemaining { get; set; }

    public decimal PublishedWorksUsed { get; set; }

    public decimal PublishedWorksLimit { get; set; }
}

public sealed class AsterSceneModerationReportRequest
{
    public string ReasonCode { get; set; } = string.Empty;

    public string? Detail { get; set; }

    public string ClientMutationId { get; set; } = string.Empty;

    public IReadOnlyList<AsterSceneEvidenceInput> Evidence { get; set; } = [];
}

public sealed class AsterSceneEvidenceInput
{
    public string EvidenceType { get; set; } = string.Empty;

    public JsonElement Payload { get; set; }
}

public sealed class AsterSceneModerationDecisionRequest
{
    public string Decision { get; set; } = string.Empty;

    public string? Note { get; set; }

    public string ClientMutationId { get; set; } = string.Empty;
}

public class AsterSceneModerationCaseDto
{
    public string Id { get; set; } = string.Empty;

    public string? WorkId { get; set; }

    public string? ProjectId { get; set; }

    public string ReasonCode { get; set; } = string.Empty;

    public string Status { get; set; } = "Open";

    public string? Decision { get; set; }

    public DateTime CreatedTime { get; set; }

    public string? DecisionNote { get; set; }

    public DateTime? DecidedAt { get; set; }
}

public sealed class AsterSceneModerationCaseDetailDto : AsterSceneModerationCaseDto
{
    public IReadOnlyList<AsterSceneModerationDecisionDto> Decisions { get; set; } = [];

    public IReadOnlyList<AsterSceneEvidenceDto> Evidence { get; set; } = [];

    public IReadOnlyList<AsterSceneAppealDto> Appeals { get; set; } = [];
}

public sealed class AsterSceneModerationDecisionDto
{
    public string Id { get; set; } = string.Empty;

    public string Decision { get; set; } = string.Empty;

    public string? Note { get; set; }

    public string ActorUserId { get; set; } = string.Empty;

    public string ClientMutationId { get; set; } = string.Empty;

    public DateTime CreatedTime { get; set; }
}

public sealed class AsterSceneEvidenceDto
{
    public string Id { get; set; } = string.Empty;

    public string? AppealId { get; set; }

    public string EvidenceType { get; set; } = string.Empty;

    public string EvidenceHash { get; set; } = string.Empty;

    public string SubmittedBy { get; set; } = string.Empty;

    public DateTime CreatedTime { get; set; }
}

public sealed class AsterSceneAppealRequest
{
    public string Reason { get; set; } = string.Empty;

    public string ClientMutationId { get; set; } = string.Empty;

    public IReadOnlyList<AsterSceneEvidenceInput> Evidence { get; set; } = [];
}

public sealed class AsterSceneAppealDecisionRequest
{
    public string Decision { get; set; } = string.Empty;

    public string? Note { get; set; }

    public string ClientMutationId { get; set; } = string.Empty;
}

public sealed class AsterSceneAppealDto
{
    public string Id { get; set; } = string.Empty;

    public string CaseId { get; set; } = string.Empty;

    public string AppellantUserId { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public string Status { get; set; } = "Submitted";

    public string ClientMutationId { get; set; } = string.Empty;

    public DateTime CreatedTime { get; set; }
}

public sealed class AsterSceneAppealDecisionDto
{
    public string Id { get; set; } = string.Empty;

    public string AppealId { get; set; } = string.Empty;

    public string Decision { get; set; } = string.Empty;

    public string? Note { get; set; }

    public string ActorUserId { get; set; } = string.Empty;

    public string ClientMutationId { get; set; } = string.Empty;

    public DateTime CreatedTime { get; set; }
}

public sealed class AsterSceneAiGenerateRequest
{
    public string ProjectId { get; set; } = string.Empty;

    public int ExpectedRevision { get; set; }

    public string Prompt { get; set; } = string.Empty;

    public string ClientMutationId { get; set; } = string.Empty;
}

public sealed class AsterSceneSupportBundleRequest
{
    public string ProjectId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Severity { get; set; } = "Normal";

    public JsonElement Diagnostics { get; set; }

    public string ClientMutationId { get; set; } = string.Empty;
}

public sealed class AsterSceneSupportCommentRequest
{
    public string Message { get; set; } = string.Empty;

    public string ClientMutationId { get; set; } = string.Empty;
}

public sealed class AsterSceneSupportCloseRequest
{
    public string Resolution { get; set; } = string.Empty;

    public string ClientMutationId { get; set; } = string.Empty;
}

public sealed class AsterSceneSupportTicketDto
{
    public string Id { get; set; } = string.Empty;

    public string ProjectId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Status { get; set; } = "Open";

    public string Severity { get; set; } = "Normal";

    public DateTime CreatedTime { get; set; }
}

public sealed class AsterSceneSupportTicketDetailDto
{
    public string Id { get; set; } = string.Empty;

    public string ProjectId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Status { get; set; } = "Open";

    public string Severity { get; set; } = "Normal";

    public DateTime CreatedTime { get; set; }

    public JsonElement? Diagnostics { get; set; }

    public IReadOnlyList<AsterSceneSupportCommentDto> Comments { get; set; } = [];
}

public sealed class AsterSceneSupportTicketStatusRequest
{
    public string Status { get; set; } = string.Empty;

    public string Note { get; set; } = string.Empty;

    public string ClientMutationId { get; set; } = string.Empty;
}

public sealed class AsterSceneSupportCommentDto
{
    public string Id { get; set; } = string.Empty;

    public string TicketId { get; set; } = string.Empty;

    public string CommentType { get; set; } = "Comment";

    public string Message { get; set; } = string.Empty;

    public string? StatusAfter { get; set; }

    public DateTime CreatedTime { get; set; }
}
