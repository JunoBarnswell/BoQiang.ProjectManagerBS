import type { AiTaskPlanDto, AiTaskPlanItemDto, AiTaskPlanItemUpsertRequest, AiTaskPlanUpsertRequest } from '.././api/aiCenter.api';

export interface TaskPlanMetadata {
  overview?: string;
  planMarkdown?: string;
}

export function taskPlanToRequest(plan: AiTaskPlanDto): AiTaskPlanUpsertRequest {
  return {
    assumptionsJson: plan.assumptionsJson,
    executionStrategy: plan.executionStrategy,
    expectedRevision: plan.revision,
    goal: plan.goal,
    items: plan.items.map(taskItemToRequest),
    metadataJson: plan.metadataJson,
    mode: plan.mode,
    risksJson: plan.risksJson,
    status: plan.status,
    title: plan.title
  };
}

export function taskItemToRequest(item: AiTaskPlanItemDto): AiTaskPlanItemUpsertRequest {
  return {
    acceptanceCriteriaJson: item.acceptanceCriteriaJson,
    dependsOnJson: item.dependsOnJson,
    description: item.description,
    executionHint: item.executionHint,
    id: item.id,
    maxRetryCount: item.maxRetryCount,
    ownerType: item.ownerType,
    parentItemId: item.parentItemId,
    priority: item.priority,
    sortOrder: item.sortOrder,
    status: item.status,
    taskType: item.taskType,
    title: item.title,
    toolCode: item.toolCode
  };
}

export function parseJsonArray(value?: string | null): string[] {
  if (!value) {
    return [];
  }

  try {
    const parsed = JSON.parse(value) as unknown;
    return Array.isArray(parsed) ? parsed.filter((item): item is string => typeof item === 'string') : [];
  } catch {
    return [];
  }
}

export function parsePlanMetadata(value?: string | null): TaskPlanMetadata {
  if (!value) {
    return {};
  }

  try {
    const parsed = JSON.parse(value) as Partial<TaskPlanMetadata>;
    return {
      overview: typeof parsed.overview === 'string' ? parsed.overview : undefined,
      planMarkdown: typeof parsed.planMarkdown === 'string' ? parsed.planMarkdown : undefined
    };
  } catch {
    return {};
  }
}
