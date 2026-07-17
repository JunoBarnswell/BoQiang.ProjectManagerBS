import { describe, expect, it } from 'vitest';

import type { AiTaskPlanDto } from '.././api/aiCenter.api';

import { parseJsonArray, parsePlanMetadata, taskPlanToRequest } from './taskPlanUtils';

describe('taskPlanUtils', () => {
  it('parses arrays and metadata safely', () => {
    expect(parseJsonArray('["验收一",2,"验收二"]')).toEqual(['验收一', '验收二']);
    expect(parseJsonArray('{bad json')).toEqual([]);
    expect(parsePlanMetadata('{"overview":"目标","planMarkdown":"# 计划"}')).toEqual({
      overview: '目标',
      planMarkdown: '# 计划'
    });
  });

  it('maps plan detail back to update request without dropping v2 fields', () => {
    const plan: AiTaskPlanDto = {
      id: 'plan-1',
      conversationId: 'conv-1',
      title: '计划',
      goal: '目标',
      status: 'PlanReady',
      mode: 'Plan',
      versionNo: 1,
      revision: 3,
      executionStrategy: 'Serial',
      createdTime: new Date(0).toISOString(),
      progress: { blockedCount: 0, completedCount: 0, failedCount: 0, percent: 0, totalCount: 1, waitingUserCount: 0 },
      events: [],
      items: [
        {
          id: 'item-1',
          planId: 'plan-1',
          title: '任务',
          description: '说明',
          status: 'Pending',
          priority: 'P1',
          ownerType: 'Agent',
          taskType: 'Design',
          sortOrder: 1,
          depth: 0,
          dependsOnJson: '["item-0"]',
          acceptanceCriteriaJson: '["通过"]',
          maxRetryCount: 3,
          retryCount: 0
        }
      ]
    };

    const request = taskPlanToRequest(plan);

    expect(request.expectedRevision).toBe(3);
    expect(request.executionStrategy).toBe('Serial');
    expect(request.items[0].acceptanceCriteriaJson).toBe('["通过"]');
    expect(request.items[0].dependsOnJson).toBe('["item-0"]');
    expect(request.items[0].taskType).toBe('Design');
  });
});
