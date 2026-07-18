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
}

export interface ProjectManagementAuditPage {
  total: number;
  items: ProjectManagementAuditItem[];
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
}

export interface ProjectManagementOperation extends ProjectManagementOperationItem {}

export interface ProjectManagementOperationPage {
  total: number;
  items: ProjectManagementOperationItem[];
}

export interface ProjectManagementBackup {
  id: string;
  backupName: string;
  sha256: string;
  fileSize: number;
  status: string;
  createdByUserId: string;
  createdTime: string;
  completedAt?: string;
  operationId?: string;
}

export interface ProjectManagementDataSpaceImpact {
  tenantId: string;
  appCode: string;
  projectCount: number;
  taskCount: number;
  memberCount: number;
  milestoneCount: number;
  attachmentCount: number;
}

export interface ProjectManagementBackupRestorePreview {
  backup: ProjectManagementBackup;
  currentDataSpace: ProjectManagementDataSpaceImpact;
  backupDataSpace: ProjectManagementDataSpaceImpact;
  impactScope: string;
  failureCompensationHint: string;
  successfulRestoreRollbackHint: string;
}

export interface ProjectManagementMemberCandidateQuery {
  pageIndex?: number;
  pageSize?: number;
  keyword?: string;
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
  canExecute: boolean;
  blockingReason?: string;
  rollbackHint: string;
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
  groupBy?: "status" | "priority" | "assignee" | "milestone" | "parent";
  sortBy?: "tree" | "dueDate" | "priority" | "status" | "updated";
  sortDirection?: "asc" | "desc";
  milestoneId?: string;
  parentTaskId?: string;
  dueFrom?: string;
  dueTo?: string;
  includeCompleted?: boolean;
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
}

export interface ProjectManagementTaskDetail extends ProjectManagementTaskListItem {
  description?: string;
  assigneeEmploymentId?: string;
  weight: number;
  estimateMinutes?: number;
  actualMinutes: number;
  createdTime: string;
  updatedTime?: string;
}

// 我的任务、批量命令等既有载荷继续使用完整任务；工作台列表使用 ProjectManagementTaskListItem。
export type ProjectManagementTask = ProjectManagementTaskDetail;

export interface ProjectManagementTaskUpsertRequest {
  taskCode: string;
  title: string;
  description?: string;
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
}

export interface ProjectManagementTaskBatchUpdateRequest {
  projectId: string;
  items: Array<{ taskId: string; versionNo: number }>;
  status?: string;
  priority?: string;
  assigneeUserId?: string;
  overrideWip?: boolean;
  milestoneId?: string;
  updateMilestone?: boolean;
  startDate?: string;
  dueDate?: string;
  updateSchedule?: boolean;
  labelIds?: string[];
  updateLabels?: boolean;
}

export interface ProjectManagementLabel {
  id: string;
  projectId?: string;
  labelName: string;
  color: string;
  versionNo: number;
}

export interface ProjectManagementTaskComment {
  id: string;
  projectId: string;
  taskId: string;
  parentCommentId?: string;
  markdown: string;
  mentionUserIds: string[];
  authorUserId: string;
  versionNo: number;
  createdTime: string;
  editedTime?: string;
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
}

export interface ProjectManagementDataSpaceSummary {
  tenantId: string;
  appCode: string;
  databaseStatus: string;
  projectCount: number;
  taskCount: number;
  memberCount: number;
  milestoneCount: number;
  attachmentCount: number;
  lastActivityTime?: string | null;
}

export type ProjectManagementSearchScope = 'all' | 'projects' | 'tasks' | 'comments';

export interface ProjectManagementSearchQuery {
  keyword: string;
  scope?: ProjectManagementSearchScope;
  limit?: number;
}

export interface ProjectManagementSearchItem {
  resultType: 'project' | 'task' | 'comment';
  id: string;
  projectId: string;
  title: string;
  summary?: string;
  targetRoute: string;
  updatedTime?: string;
}

export interface ProjectManagementSearchResponse {
  projects: ProjectManagementSearchItem[];
  tasks: ProjectManagementSearchItem[];
  comments: ProjectManagementSearchItem[];
}

export interface ProjectManagementReportQuery {
  pageIndex?: number;
  pageSize?: number;
  keyword?: string;
  status?: string;
}

export type ProjectManagementReportSnapshotFormat = 'csv' | 'xlsx' | 'pdf';

export interface ProjectManagementReportSnapshotRequest {
  format: ProjectManagementReportSnapshotFormat;
  query: ProjectManagementReportQuery;
}

export interface ProjectManagementReportSnapshotStartResponse {
  operationId: string;
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
}

export interface ProjectManagementSyncImportResponse {
  packageId: string;
  strategy: string;
  inserted: number;
  updated: number;
  skipped: number;
  attachmentsImported: number;
  warnings: string[];
}

export interface ProjectManagementSavedView {
  id: string;
  projectId: string;
  viewName: string;
  viewKey: ProjectManagementTaskView;
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
  viewKey: ProjectManagementTaskView;
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
}

export type ProjectManagementPageState = "loading" | "empty" | "error" | "forbidden";
