export interface ProjectManagementWorkspaceQuery {
  appCode: string;
  filter?: ProjectManagementFilter;
  milestoneId?: string;
  projectId?: string;
  tenantId: string;
  version?: string;
}

export interface ProjectManagementFilter {
  keyword?: string;
  status?: string;
  assigneeId?: string;
}

export interface ProjectManagementWorkspaceOverview {
  projectCount: number;
  activeProjectCount: number;
  taskCount: number;
  overdueTaskCount: number;
}

export interface ProjectManagementAuditQuery {
  pageIndex?: number;
  pageSize?: number;
  projectId?: string;
  aggregateType?: string;
  activityType?: string;
  keyword?: string;
  from?: string;
  to?: string;
  actorUserId?: string;
  actorRole?: string;
  source?: string;
  sourceDeviceId?: string;
  isSuccess?: boolean;
  sorts?: Array<{ field: 'createdTime' | 'projectId' | 'aggregateType' | 'activityType' | 'actorUserId'; order?: 'asc' | 'desc' }>;
}

export type ProjectManagementAuditExportField = 'Project' | 'Object' | 'ActivityType' | 'Summary' | 'TraceId' | 'Actor' | 'CreatedTime' | 'Source' | 'FieldChanges';

export interface ProjectManagementAuditExportRequest {
  query: ProjectManagementAuditQuery;
  fields?: ProjectManagementAuditExportField[];
  includeSensitive?: boolean;
}

export interface ProjectManagementAuditExportStartResponse {
  operationId: string;
  traceId: string;
  expiresAt: string;
}

export interface ProjectManagementAuditItem {
  id: string;
  projectId: string;
  aggregateType: string;
  aggregateId: string;
  activityType: string;
  summary?: string;
  traceId: string;
  actorUserId: string;
  createdTime: string;
  source: string;
  sourceDeviceId?: string;
  isSuccess: boolean;
  projectDisplayName?: string;
  aggregateDisplayName?: string;
  actorDisplayName?: string;
}

export interface ProjectManagementAuditPage {
  total: number;
  items: ProjectManagementAuditItem[];
}

export interface ProjectManagementAuditFieldChange {
  field: string;
  displayName?: string;
  before?: string;
  after?: string;
  isSensitive: boolean;
}

export interface ProjectManagementAuditBatchItem {
  aggregateType: string;
  aggregateId: string;
  summary?: string;
  fieldChanges?: ProjectManagementAuditFieldChange[];
}

export interface ProjectManagementAuditBatch {
  operationId: string;
  totalCount: number;
  successCount: number;
  failureCount: number;
  details?: ProjectManagementAuditBatchItem[];
}

export interface ProjectManagementAuditEntitySnapshot {
  projectId: string;
  aggregateType: string;
  aggregateId: string;
  summary?: string;
  isDeleted: boolean;
}

export interface ProjectManagementAuditRelatedEvent {
  id: string;
  kind: string;
  causality: 'Current' | 'Preceded' | 'Followed' | string;
  aggregateType?: string;
  aggregateId?: string;
  activityType?: string;
  summary?: string;
  status?: string;
  occurredAt: string;
}

export interface ProjectManagementAuditReference {
  kind: string;
  id: string;
  displayName?: string;
}

export interface ProjectManagementAuditDetail {
  audit: ProjectManagementAuditItem;
  fieldChanges: ProjectManagementAuditFieldChange[];
  batch?: ProjectManagementAuditBatch;
  entitySnapshot: ProjectManagementAuditEntitySnapshot;
  failureReason?: string;
  relatedEvents: ProjectManagementAuditRelatedEvent[];
  references: ProjectManagementAuditReference[];
  traceDiagnosticsRoute?: string;
}

export interface ProjectManagementOperationQuery {
  pageIndex?: number;
  pageSize?: number;
  operationType?: string;
  status?: string;
}

export interface ProjectManagementOperationItem {
  id: string;
  operationType: string;
  status: string;
  phase: string;
  progressPercent: number;
  isCancellationRequested: boolean;
  impactJson: string;
  errorMessage?: string;
  traceId: string;
  actorUserId: string;
  startedTime: string;
  completedTime?: string;
  actorDisplayName?: string;
}

export type ProjectManagementOperation = ProjectManagementOperationItem;

export interface ProjectManagementOperationPage {
  total: number;
  items: ProjectManagementOperationItem[];
}

export interface ProjectManagementReversibleCommand {
  id: string;
  sequenceNo: number;
  commandType: string;
  projectId: string;
  aggregateType: string;
  aggregateId: string;
  state: 'Applied' | 'Undone' | 'Invalidated';
  summary?: string;
  traceId: string;
  createdTime: string;
  lastReplayedTime?: string;
  isReplayPending: boolean;
}

export interface ProjectManagementReversibleCommandStack {
  commands: ProjectManagementReversibleCommand[];
  canUndo: boolean;
  canRedo: boolean;
}

export interface ProjectManagementReversibleCommandExecuteRequest {
  requestId: string;
}

export interface ProjectManagementMemberCandidateQuery {
  pageIndex?: number;
  pageSize?: number;
  keyword?: string;
  projectId?: string;
  deptId?: string;
  positionId?: string;
}

export interface ProjectManagementMemberCandidate {
  userId: string;
  userName: string;
  displayName: string;
  deptId?: string;
  deptName?: string;
  positionId?: string;
  positionName?: string;
  employmentId: string;
  employmentName: string;
  status: string;
  isSelectable: boolean;
}

export interface ProjectManagementProjectQuery {
  pageIndex?: number;
  pageSize?: number;
  keyword?: string;
  status?: string;
  ownerUserId?: string;
}

export type ProjectManagementHomeCollection = 'all' | 'favorites' | 'recent';
export type ProjectManagementHomeHealth = 'Completed' | 'UpdateMissing' | 'AtRisk' | 'OffTrack' | 'OnTrack' | 'NoUpdateExpected';
export type ProjectManagementHomeView = 'all' | 'my-projects' | 'due-this-week' | 'at-risk' | 'archived' | string;

export interface ProjectManagementHomeFilterRule {
  field: string;
  operator: string;
  values: string[];
}

export interface ProjectManagementHomeFilterGroup {
  conjunction: 'and';
  rules: ProjectManagementHomeFilterRule[];
}

export interface ProjectManagementHomeQuery {
  collection?: ProjectManagementHomeCollection;
  view?: ProjectManagementHomeView;
  keyword?: string;
  health?: ProjectManagementHomeHealth;
  priority?: string;
  leadUserId?: string;
  status?: string;
  targetDateFrom?: string;
  targetDateTo?: string;
  includeArchived?: boolean;
  sortBy?: string;
  sortDirection?: 'asc' | 'desc';
  pageIndex?: number;
  pageSize?: number;
  filter?: string;
  columns?: string;
  density?: 'compact' | 'default' | 'comfortable' | string;
  insights?: boolean;
  insightsTab?: 'health' | 'leads' | string;
  projectIds?: string;
}

export interface ProjectManagementHomeProjectItem extends ProjectManagementProject {
  health: ProjectManagementHomeHealth;
  targetDate?: string;
  currentMilestoneId?: string;
  currentMilestoneName?: string;
  issueCount: number;
  openIssueCount: number;
  completedIssueCount: number;
}

export interface ProjectManagementHomeProjectsResponse {
  items: ProjectManagementHomeProjectItem[];
  total: number;
  pageIndex: number;
  pageSize: number;
  sequence: number;
}

export interface ProjectManagementHomeSummaryResponse {
  health: Array<{ key: ProjectManagementHomeHealth; count: number }>;
  leads: Array<{ userId?: string; displayName: string; count: number }>;
  unassignedCount: number;
  sequence: number;
  total?: number;
  status?: Array<{ key: string; count: number }>;
  updatedTime?: string;
}

export interface ProjectManagementRecycleQuery {
  pageIndex?: number;
  pageSize?: number;
  projectId?: string;
  keyword?: string;
}

export interface ProjectManagementRecycleProjectItem {
  id: string;
  projectCode: string;
  projectName: string;
  status: string;
  versionNo: number;
  deletedTime?: string;
  deletedBy?: string;
  deletedByDisplayName?: string;
  affectedTaskCount?: number;
  canRestore: boolean;
  canPurge: boolean;
}

export interface ProjectManagementRecyclePurgePreview {
  projectId: string;
  projectCode: string;
  projectName: string;
  versionNo: number;
  memberReferenceCount: number;
  milestoneReferenceCount: number;
  taskReferenceCount: number;
  impact: ProjectManagementRecyclePurgeImpact;
  canExecute: boolean;
  blockingReason?: string;
  rollbackHint: string;
}

export interface ProjectManagementRecycleTaskPurgePreview {
  taskId: string;
  projectId: string;
  taskCode: string;
  title: string;
  versionNo: number;
  taskCount: number;
  dependencyCount: number;
  impact: ProjectManagementRecyclePurgeImpact;
  canExecute: boolean;
  blockingReason?: string;
  rollbackHint: string;
}

export interface ProjectManagementRecyclePurgeImpact {
  projectCount: number;
  taskCount: number;
  descendantTaskCount: number;
  memberCount: number;
  milestoneCount: number;
  dependencyCount: number;
  participantCount: number;
  labelRelationCount: number;
  timeLogCount: number;
  commentCount: number;
  attachmentCount: number;
  reminderCount: number;
  notificationCount: number;
  recurrenceCount: number;
  occurrenceCount: number;
  conversationLinkCount: number;
  savedViewCount: number;
  syncJournalCount: number;
}

export interface ProjectManagementRecycleTaskItem {
  id: string;
  projectId: string;
  taskCode: string;
  title: string;
  status: string;
  versionNo: number;
  deletedTime?: string;
  deletedBy?: string;
  projectDisplayName?: string;
  deletedByDisplayName?: string;
  affectedDescendantCount?: number;
  canRestore: boolean;
  canPurge: boolean;
}

export interface ProjectManagementRecycleResponse {
  projects: { total: number; items: ProjectManagementRecycleProjectItem[] };
  tasks: { total: number; items: ProjectManagementRecycleTaskItem[] };
}

export interface ProjectManagementOverviewQuery {
  pageIndex?: number;
  pageSize?: number;
  projectId?: string;
  keyword?: string;
}

export interface ProjectManagementOverviewMilestone {
  id: string;
  name: string;
  status: string;
  healthStatus: string;
  progressPercent: number;
  dueDate?: string;
}

export interface ProjectManagementOverviewPerson {
  userId: string;
  taskCount: number;
  completedTaskCount: number;
  overdueTaskCount: number;
  displayName?: string;
  estimatedMinutes?: number;
  capacityMinutes?: number;
  workloadPercent?: number;
}

export interface ProjectManagementOverviewDistribution {
  key: string;
  count: number;
  percent: number;
}

export interface ProjectManagementOverviewRiskSummary {
  overdueTaskCount: number;
  blockedTaskCount: number;
  dueSoonIncompleteTaskCount: number;
  inProgressTaskCount: number;
  wipLimit?: number;
  isWipExceeded: boolean;
  wipExceededBy: number;
  hasScheduleRisk: boolean;
}

export interface ProjectManagementOverviewItem {
  project: ProjectManagementProject;
  taskCount: number;
  completedTaskCount: number;
  inProgressTaskCount: number;
  overdueTaskCount: number;
  blockedTaskCount: number;
  taskProgressPercent: number;
  milestoneCount: number;
  memberCount: number;
  milestones: ProjectManagementOverviewMilestone[];
  people: ProjectManagementOverviewPerson[];
  riskSummary?: ProjectManagementOverviewRiskSummary;
  health?: string;
  workItemTypeDistribution?: ProjectManagementOverviewDistribution[];
  statusDistribution?: ProjectManagementOverviewDistribution[];
  pendingTaskCount?: number;
  storyPointsTotal?: number;
  requirementTypeDistribution?: ProjectManagementOverviewDistribution[];
}

export type ProjectManagementMyWorkCategory = 'all' | 'assigned' | 'participating' | 'created' | 'mentioned' | 'today' | 'upcoming' | 'overdue' | 'blocked';

export interface ProjectManagementMyWorkQuery {
  pageIndex?: number;
  pageSize?: number;
  projectId?: string;
  category?: ProjectManagementMyWorkCategory;
  sortBy?: 'dueDate' | 'updated' | 'created' | 'priority';
  sortDirection?: 'asc' | 'desc';
  includeCompleted?: boolean;
}

export interface ProjectManagementMyWorkProjectOptionQuery {
  keyword?: string;
  pageIndex?: number;
  pageSize?: number;
}

export interface ProjectManagementMyWorkProjectOption {
  id: string;
  projectCode: string;
  projectName: string;
}

export interface ProjectManagementMyWorkItem {
  task: ProjectManagementTask;
  projectName: string;
  isAssignee: boolean;
  isParticipant: boolean;
  isCreator: boolean;
  isMentioned: boolean;
}

export interface ProjectManagementProjectUpsertRequest {
  projectCode: string;
  projectName: string;
  description?: string;
  status?: string;
  priority?: string;
  ownerUserId?: string;
  startDate?: string;
  dueDate?: string;
  wipLimit?: number;
  progressPercent?: number;
  versionNo?: number;
  clientMutationId?: string;
}

export interface ProjectManagementProject {
  id: string;
  tenantId: string;
  appCode: string;
  projectCode: string;
  projectName: string;
  description?: string;
  status: string;
  priority: string;
  ownerUserId: string;
  ownerDisplayName?: string;
  startDate?: string;
  dueDate?: string;
  completedAt?: string;
  wipLimit?: number;
  progressPercent: number;
  versionNo: number;
  createdTime: string;
  updatedTime?: string;
}

export interface ProjectManagementImConversation {
  id: string;
  projectId: string;
  taskId?: string | null;
  conversationId: string;
  conversationType: 'Group' | string;
  title: string;
  status: 'Active' | 'Archived' | string;
  targetRoute: string;
  versionNo: number;
}

export interface ProjectManagementImConversationEnsureRequest {
  taskId?: string | null;
}

export interface ProjectManagementImConversationTarget {
  isAvailable: boolean;
  projectId?: string | null;
  taskId?: string | null;
  targetRoute?: string | null;
}

export type ProjectManagementTaskView = "tree" | "list" | "card" | "board" | "gantt" | "calendar";

export interface ProjectManagementTaskQuery {
  projectId: string;
  pageIndex?: number;
  pageSize?: number;
  keyword?: string;
  status?: string;
  assigneeUserId?: string;
  viewKey?: ProjectManagementTaskView;
  groupBy?: "status" | "priority" | "assignee" | "milestone" | "parent" | "label";
  sortBy?: "tree" | "dueDate" | "priority" | "status" | "updated";
  sortDirection?: "asc" | "desc";
  milestoneId?: string;
  parentTaskId?: string;
  dueFrom?: string;
  dueTo?: string;
  includeCompleted?: boolean;
  labelFilter?: ProjectManagementTaskLabelFilter;
  workItemType?: string;
  riskLevel?: string;
  requirementType?: string;
  requirementSource?: string;
  mentionedUserId?: string;
  hasAttachment?: boolean;
  hasChildren?: boolean;
}

export interface ProjectManagementTaskListItem {
  id: string;
  projectId: string;
  milestoneId?: string;
  parentTaskId?: string;
  taskCode: string;
  title: string;
  status: string;
  priority: string;
  assigneeUserId?: string;
  startDate?: string;
  dueDate?: string;
  progressPercent: number;
  sortOrder: number;
  depth: number;
  versionNo: number;
  blockedByCount: number;
  canStart: boolean;
  blockedReason?: string;
  summary?: string;
  hasChildren?: boolean;
  labels?: ProjectManagementTaskLabel[];
  participantUserIds?: string[];
  assigneeDisplayName?: string;
  participantDisplayNames?: string[];
  workItemType?: string;
  riskLevel?: string;
  requirementType?: string;
  requirementSource?: string;
  storyPoints?: number;
  childCount?: number;
  completedChildCount?: number;
  hasAttachments?: boolean;
}

export interface ProjectManagementTaskDetail extends ProjectManagementTaskListItem {
  description?: string;
  markdown?: string;
  assigneeEmploymentId?: string;
  weight: number;
  estimateMinutes?: number;
  actualMinutes: number;
  createdTime: string;
  updatedTime?: string;
  contentJson?: string;
  contentText?: string;
  mentionUserIds?: string[];
  followerUserIds?: string[];
  draftId?: string;
}

export interface ProjectManagementTaskFollower {
  id: string;
  taskId: string;
  userId: string;
  versionNo: number;
  createdTime: string;
}

export interface ProjectManagementTaskFollowerUpsertRequest {
  userId: string;
  versionNo?: number;
}

export interface ProjectManagementTaskDraft {
  id: string;
  projectId: string;
  payloadJson: string;
  expiresAt: string;
  versionNo: number;
}

export interface ProjectManagementTaskDraftAttachment {
  id: string;
  draftId: string;
  fileName: string;
  contentType: string;
  fileSize: number;
  versionNo: number;
  createdTime: string;
}

export interface ProjectManagementTaskConflictLocalValues extends ProjectManagementTaskUpsertRequest {
  operation: string;
  versionNo: number;
  submittedFields: string[];
}

export interface ProjectManagementTaskConflictField {
  field: string;
  displayName: string;
  serverValue?: unknown;
  localValue?: unknown;
}

export interface ProjectManagementTaskVersionConflictResponse {
  serverValues: ProjectManagementTaskDetail;
  localValues: ProjectManagementTaskConflictLocalValues;
  fieldConflicts: ProjectManagementTaskConflictField[];
}

export interface ProjectManagementTaskDependency {
  id: string;
  projectId: string;
  predecessorTaskId: string;
  successorTaskId: string;
  dependencyType: string;
  lagMinutes: number;
  versionNo: number;
}

// 我的任务、批量命令等既有载荷继续使用完整任务；工作台列表使用 ProjectManagementTaskListItem。
export type ProjectManagementTask = ProjectManagementTaskDetail;

export interface ProjectManagementTaskUpsertRequest {
  taskCode: string;
  title: string;
  description?: string;
  markdown?: string;
  status?: string;
  priority?: string;
  milestoneId?: string;
  parentTaskId?: string;
  assigneeUserId?: string;
  assigneeEmploymentId?: string;
  startDate?: string;
  dueDate?: string;
  progressPercent?: number;
  weight?: number;
  estimateMinutes?: number;
  versionNo?: number;
  overrideWip?: boolean;
  overrideWipReason?: string;
  summary?: string;
  workItemType?: string;
  contentJson?: string;
  contentText?: string;
  riskLevel?: string;
  requirementType?: string;
  requirementSource?: string;
  storyPoints?: number;
  mentionUserIds?: string[];
  followerUserIds?: string[];
  draftId?: string;
}

export interface ProjectManagementTaskBatchUpdateRequest {
  projectId: string;
  items: Array<{ taskId: string; versionNo: number }>;
  status?: string;
  priority?: string;
  assigneeUserId?: string;
  overrideWip?: boolean;
  overrideWipReason?: string;
  milestoneId?: string;
  updateMilestone?: boolean;
  startDate?: string;
  dueDate?: string;
  updateSchedule?: boolean;
  labelIds?: string[];
  updateLabels?: boolean;
  operation?: 'update' | 'delete';
  deleteMode?: 'Cascade' | 'PromoteChildren';
}

export type ProjectManagementTaskBatchResultStatus = 'succeeded' | 'skipped' | 'failed' | 'conflict';

export interface ProjectManagementTaskBatchItemResult {
  taskId: string;
  taskCode?: string | null;
  status: ProjectManagementTaskBatchResultStatus;
  message?: string | null;
  errorCode?: number | null;
  versionNo?: number | null;
}

export interface ProjectManagementTaskBatchExecutionResult {
  operationId: string;
  projectId: string;
  requestedCount: number;
  succeededCount: number;
  skippedCount: number;
  failedCount: number;
  conflictCount: number;
  items: ProjectManagementTaskBatchItemResult[];
}

export interface ProjectManagementLabel {
  id: string;
  projectId?: string;
  scope: "Public" | "Project";
  labelName: string;
  color: string;
  versionNo: number;
}

export interface ProjectManagementLabelUpsertRequest {
  labelName: string;
  color?: string;
  versionNo?: number;
}

export interface ProjectManagementTaskLabel {
  id: string;
  taskId: string;
  labelId: string;
  labelName: string;
  color: string;
}

export interface ProjectManagementTaskLabelSetRequest {
  labelIds: string[];
  versionNo: number;
}

// 与后端 ProjectManagementTaskLabelFilter 对齐；任务列表、看板、甘特和导出在后续查询实现中复用此语义。
export interface ProjectManagementTaskLabelFilter {
  labelIds: string[];
  matchMode?: "Any" | "All";
}

export interface ProjectManagementTaskComment {
  id: string;
  projectId: string;
  taskId: string;
  parentCommentId?: string;
  markdown: string;
  mentions: ProjectManagementTaskCommentMention[];
  authorUserId: string;
  versionNo: number;
  createdTime: string;
  editedTime?: string;
  authorDisplayName?: string;
}

export interface ProjectManagementTaskCommentMention {
  userId: string;
  displayName: string;
}

export interface ProjectManagementTaskCommentPage {
  total: number;
  items: ProjectManagementTaskComment[];
}

export interface ProjectManagementTaskCommentQuery {
  pageIndex?: number;
  pageSize?: number;
  sort?: 'timeline' | 'desc';
}

export interface ProjectManagementTaskCommentUpsertRequest {
  markdown: string;
  parentCommentId?: string;
  mentionUserIds?: string[];
  versionNo?: number;
}

export interface ProjectManagementTaskReminder {
  id: string;
  projectId: string;
  taskId: string;
  recipientUserId: string;
  reminderAtUtc: string;
  timeZoneId: string;
  note?: string;
  status: 'Pending' | 'Sent' | 'Canceled' | 'Failed';
  attemptCount: number;
  maxAttempts: number;
  lastAttemptAt?: string;
  triggeredAt?: string;
  lastError?: string;
  versionNo: number;
  createdTime: string;
}

export interface ProjectManagementTaskReminderCreateRequest {
  reminderAt: string;
  timeZoneId: string;
  recipientScope: 'Self' | 'Assignee' | 'Participants' | 'Members';
  recipientUserIds?: string[];
  note?: string;
  clientRequestId: string;
}

export interface ProjectManagementTaskReminderUpdateRequest {
  reminderAt: string;
  timeZoneId: string;
  note?: string;
  versionNo: number;
}

export interface ProjectManagementProjectSubscription {
  projectId: string;
  userId: string;
  mode: 'AllUpdates' | 'Important' | 'Mentions';
  versionNo: number;
  updatedTime?: string;
}

export interface ProjectManagementProjectSubscriptionUpsertRequest {
  mode: ProjectManagementProjectSubscription['mode'];
  versionNo?: number;
}

export interface ProjectManagementProjectReminder {
  id: string;
  projectId: string;
  recipientUserId: string;
  reminderAtUtc: string;
  timeZoneId: string;
  note?: string;
  status: 'Pending' | 'Sent' | 'Canceled' | 'Failed';
  attemptCount: number;
  maxAttempts: number;
  lastAttemptAt?: string;
  triggeredAt?: string;
  lastError?: string;
  versionNo: number;
  createdTime: string;
}

export interface ProjectManagementProjectReminderCreateRequest {
  reminderAt: string;
  timeZoneId: string;
  note?: string;
  clientRequestId: string;
}

export interface ProjectManagementNotificationQuery {
  pageIndex?: number;
  pageSize?: number;
  unreadOnly?: boolean;
  notificationType?: string;
}

export interface ProjectManagementNotification {
  id: string;
  notificationType: string;
  title: string;
  message: string;
  targetRoute: string;
  traceId: string;
  projectId?: string;
  taskId?: string;
  isRead: boolean;
  createdTime: string;
  readTime?: string;
}

export interface ProjectManagementNotificationPage {
  total: number;
  unreadCount: number;
  items: ProjectManagementNotification[];
}

export interface ProjectManagementNotificationOpenResult {
  isAvailable: boolean;
  targetRoute?: string;
  unavailableReason?: string;
}

export interface ProjectManagementTaskAttachment {
  id: string;
  projectId: string;
  taskId: string;
  fileId: string;
  fileName: string;
  contentType: string;
  fileSize: number;
  downloadUrl: string;
  previewUrl: string;
  uploadedByUserId: string;
  createdTime: string;
  versionNo: number;
  previewSupported: boolean;
  previewType?: string;
  previewPipeline?: string;
}

export type ProjectManagementSearchScope = 'all' | 'projects' | 'tasks' | 'milestones' | 'labels' | 'members' | 'comments';

export interface ProjectManagementSearchQuery {
  keyword: string;
  scope?: ProjectManagementSearchScope;
  limit?: number;
  projectId?: string;
  status?: string;
  from?: string;
  to?: string;
  pageIndex?: number;
}

export interface ProjectManagementSearchItem {
  resultType: 'project' | 'task' | 'milestone' | 'label' | 'member' | 'comment';
  id: string;
  projectId: string;
  title: string;
  summary?: string;
  targetRoute: string;
  updatedTime?: string | null;
}

export interface ProjectManagementSearchResponse {
  projects: ProjectManagementSearchItem[];
  tasks: ProjectManagementSearchItem[];
  milestones: ProjectManagementSearchItem[];
  labels: ProjectManagementSearchItem[];
  members: ProjectManagementSearchItem[];
  comments: ProjectManagementSearchItem[];
}

export interface ProjectManagementSearchIndexStatus {
  status: 'Unavailable' | 'Rebuilding' | 'Incremental' | 'Ready' | 'Failed' | string;
  mode: 'none' | 'rebuild' | 'incremental' | string;
  appliedSequenceNo: number;
  targetSequenceNo: number;
  documentCount: number;
  failureCount: number;
  lastError?: string | null;
  operationId?: string | null;
  startedTime?: string | null;
  completedTime?: string | null;
  updatedTime: string;
}

export interface ProjectManagementReportQuery {
  pageIndex?: number;
  pageSize?: number;
  keyword?: string;
  status?: string;
  labelFilter?: ProjectManagementTaskLabelFilter;
}

export type ProjectManagementReportSnapshotFormat = 'csv' | 'xlsx' | 'pdf';

export interface ProjectManagementReportSnapshotOptions {
  includeCompleted?: boolean;
  includeDeleted?: boolean;
  includeCommentSummary?: boolean;
  includeAttachmentList?: boolean;
  includeGanttSnapshot?: boolean;
  maxTaskRows?: number;
  retentionHours?: number;
}

export interface ProjectManagementReportSnapshotRequest {
  format: ProjectManagementReportSnapshotFormat;
  query: ProjectManagementReportQuery;
  options?: ProjectManagementReportSnapshotOptions;
}

export interface ProjectManagementReportSnapshotStartResponse {
  operationId: string;
  traceId: string;
  expiresAt: string;
}

export type ProjectManagementExcelImportPreviewStatus = 'Completed' | 'CompletedWithErrors';

export interface ProjectManagementExcelImportRowError {
  sheetName: string;
  rowNumber: number;
  stableId?: string;
  code: string;
  message: string;
  severity: string;
}

export interface ProjectManagementExcelImportPreview {
  previewId: string;
  status: ProjectManagementExcelImportPreviewStatus;
  templateVersion: string;
  parsedAt: string;
  totalRows: number;
  importableRows: number;
  duplicateRows: number;
  errorRows: number;
  warningRows: number;
  newRows: number;
  updatedRows: number;
  skippedRows: number;
  errors: ProjectManagementExcelImportRowError[];
  errorsTruncated: boolean;
}

export type ProjectManagementExcelImportResultStatus = 'Succeeded' | 'Failed' | 'Replayed';

export interface ProjectManagementExcelImportRowResult {
  sheetName: string;
  rowNumber: number;
  stableId?: string;
  status: 'Added' | 'Updated' | 'Skipped' | 'Failed' | 'Conflict' | 'Warning' | string;
  message?: string | null;
  versionNo?: number | null;
}

export interface ProjectManagementExcelImportResult {
  importId: string;
  previewId: string;
  idempotencyKey: string;
  status: ProjectManagementExcelImportResultStatus;
  traceId: string;
  completedAt: string;
  addedRows: number;
  updatedRows: number;
  skippedRows: number;
  failedRows: number;
  conflictRows: number;
  warningRows: number;
  rows: ProjectManagementExcelImportRowResult[];
  replayed: boolean;
}

export interface ProjectManagementSyncWatermark {
  deviceId: string;
  currentSequenceNo: number;
  acknowledgedSequenceNo: number;
  lastSeenAt?: string;
}

export interface ProjectManagementSyncJournalItem {
  sequenceNo: number;
  aggregateType: string;
  aggregateId: string;
  projectId?: string;
  operation: string;
  versionNo: number;
  payloadJson: string;
  traceId: string;
  createdTime: string;
  source: string;
  fieldChanges?: ProjectManagementSyncFieldChange[] | null;
  deviceId?: string | null;
  projectDisplayName?: string;
  aggregateDisplayName?: string;
}

export interface ProjectManagementSyncConflict {
  aggregateType: string;
  aggregateId: string;
  projectId?: string | null;
  field: string;
  localValue?: string | null;
  remoteValue?: string | null;
  localVersionNo?: number | null;
  remoteVersionNo?: number | null;
  recommendedStrategy: string;
}

export interface ProjectManagementSyncFieldChange {
  field: string;
  before?: string | null;
  after?: string | null;
}

export interface ProjectManagementSyncPreviewResponse {
  packageId: string;
  schemaVersion: string;
  tenantId: string;
  appCode: string;
  exportedAt: string;
  sourceDeviceId?: string;
  projectCount: number;
  memberCount: number;
  milestoneCount: number;
  taskCount: number;
  dependencyCount: number;
  attachmentCount: number;
  packageSize: number;
  packageSha256: string;
  journalSequenceNo: number;
  isCompatible: boolean;
  warnings: string[];
  conflicts: string[];
  mode: 'Full' | 'Incremental' | 'History' | string;
  sinceSequenceNo: number;
  conflictDetails?: ProjectManagementSyncConflict[] | null;
  alreadyImported: boolean;
  signatureAlgorithm: string;
  signatureKeyId: string;
  signatureValid: boolean;
  journalCount: number;
  hasChanges: boolean;
  attachmentEntryCount: number;
  validationState: string;
  uncompressedSize: number;
  archiveEntryCount: number;
  previewOnly: boolean;
}

export interface ProjectManagementSyncImportResponse {
  packageId: string;
  strategy: string;
  inserted: number;
  updated: number;
  skipped: number;
  attachmentsImported: number;
  warnings: string[];
  importId: string;
  traceId: string;
  replayed: boolean;
  conflictCount: number;
  conflicts?: ProjectManagementSyncConflict[] | null;
  deleted: number;
  failed: number;
}

export interface ProjectManagementSyncHistoryItem {
  id: string;
  operationType: 'Import' | 'Export' | string;
  packageId: string;
  sourceTenantId: string;
  sourceAppCode: string;
  sourceDeviceId?: string | null;
  targetTenantId: string;
  targetAppCode: string;
  actorUserId: string;
  status: 'Succeeded' | 'Failed' | string;
  inserted: number;
  updated: number;
  deleted: number;
  skipped: number;
  conflictCount: number;
  failed: number;
  attachmentsImported: number;
  traceId: string;
  errorMessage?: string | null;
  retryOfHistoryId?: string | null;
  occurredAt: string;
  actorDisplayName?: string;
}

export interface ProjectManagementSyncHistoryDetail {
  item: ProjectManagementSyncHistoryItem;
  strategy: string;
  warnings: string[];
  conflicts: ProjectManagementSyncConflict[];
}

export interface ProjectManagementSyncHistoryPage {
  total: number;
  items: ProjectManagementSyncHistoryItem[];
}

export interface ProjectManagementSavedView {
  id: string;
  projectId: string;
  viewName: string;
  viewKey: ProjectManagementTaskView | 'home';
  queryJson: string;
  ownerUserId: string;
  isShared: boolean;
  isDefault: boolean;
  versionNo: number;
  createdTime: string;
  updatedTime?: string;
}

export interface ProjectManagementSavedViewUpsertRequest {
  viewName: string;
  viewKey: ProjectManagementTaskView | 'home';
  queryJson: string;
  isShared?: boolean;
  isDefault?: boolean;
  versionNo?: number;
}

export interface ProjectManagementMilestone {
  id: string;
  projectId: string;
  milestoneName: string;
  description?: string;
  ownerUserId?: string;
  status: string;
  healthStatus: string;
  startDate?: string;
  dueDate?: string;
  completedAt?: string;
  progressPercent: number;
  leafTaskCount: number;
  completedLeafTaskCount: number;
  sortOrder: number;
  versionNo: number;
  ownerDisplayName?: string;
}

export interface ProjectManagementMember {
  id: string;
  projectId: string;
  userId: string;
  employmentId?: string;
  roleCode: string;
  scopeRootTaskId?: string;
  isActive: boolean;
  joinedAt: string;
  leftAt?: string;
  versionNo: number;
  displayName?: string;
}

export interface ProjectManagementMemberUpsertRequest {
  userId: string;
  employmentId?: string;
  roleCode?: string;
  scopeRootTaskId?: string;
  versionNo?: number;
}

export interface ProjectManagementMilestoneUpsertRequest {
  milestoneName: string;
  description?: string;
  ownerUserId?: string;
  status?: string;
  startDate?: string;
  dueDate?: string;
  progressPercent?: number;
  sortOrder?: number;
  versionNo?: number;
}

export interface ProjectManagementActivity {
  id: string;
  projectId: string;
  aggregateType: string;
  aggregateId: string;
  activityType: string;
  summary?: string;
  traceId: string;
  actorUserId: string;
  createdTime: string;
  batch?: {
    operationId: string;
    totalCount: number;
    successCount: number;
    failureCount: number;
    details?: Array<{ aggregateType: string; aggregateId: string; summary?: string }>;
  };
  targetRoute?: string;
  isTargetDeleted?: boolean;
  actorDisplayName?: string;
}

export interface ProjectManagementActivityQuery {
  pageIndex?: number;
  pageSize?: number;
  aggregateType?: string;
  aggregateId?: string;
  activityType?: string;
  actorUserId?: string;
  from?: string;
  to?: string;
}

export interface ProjectManagementActivityPage {
  total: number;
  items: ProjectManagementActivity[];
}

export interface ProjectManagementProjectUpdateRequest {
  body: string;
  clientMutationId?: string;
}

export interface ProjectManagementResource {
  id: string;
  projectId: string;
  resourceName: string;
  resourceUrl: string;
  description?: string;
  versionNo: number;
  createdTime: string;
  updatedTime?: string;
}

export interface ProjectManagementResourceUpsertRequest {
  resourceName: string;
  resourceUrl: string;
  description?: string;
  versionNo?: number;
}

export interface ProjectManagementWebhookSubscription {
  id: string;
  projectId: string;
  name: string;
  endpointUrl: string;
  secretConfigured: boolean;
  eventTypes: string[];
  isEnabled: boolean;
  maxAttempts: number;
  createdTime: string;
  updatedTime?: string;
}

export interface ProjectManagementWebhookSubscriptionUpsertRequest {
  id?: string;
  projectId: string;
  name: string;
  endpointUrl: string;
  secret?: string;
  eventTypes: string[];
  isEnabled: boolean;
  maxAttempts?: number;
}

export interface ProjectManagementWebhookDelivery {
  eventId: string;
  subscriptionId: string;
  projectId: string;
  eventType: string;
  status: string;
  attemptCount: number;
  maxAttempts: number;
  nextAttemptAt: string;
  errorMessage?: string;
  createdTime: string;
  completedTime?: string;
}

export type ProjectManagementPageState = "loading" | "empty" | "error" | "forbidden";
