import { describe, expect, it } from 'vitest';

import { buildGanttDependencyPaths, formatFloatMinutes } from './dependencyAnalysisModel';

describe('buildGanttDependencyPaths', () => {
  it('renders Finish-to-Start links only when both visible rows exist', () => {
    const paths = buildGanttDependencyPaths([
      { dependencyId: 'a-b', predecessorTaskId: 'a', successorTaskId: 'b', dependencyType: 'FinishToStart', lagMinutes: 30, isRenderable: true, isCritical: true },
      { dependencyId: 'ghost-b', predecessorTaskId: 'ghost', successorTaskId: 'b', dependencyType: 'FinishToStart', lagMinutes: 0, isRenderable: false, isCritical: false },
    ], [
      { taskId: 'a', left: 10, top: 20, width: 50, height: 20 },
      { taskId: 'b', left: 120, top: 70, width: 30, height: 20 },
    ]);

    expect(paths).toEqual([expect.objectContaining({ dependencyId: 'a-b', isCritical: true, label: '+30m' })]);
    expect(paths[0].d).toContain('M 60 30');
  });

  it('formats float without hiding the critical-path distinction', () => {
    expect(formatFloatMinutes(0)).toBe('关键路径');
    expect(formatFloatMinutes(2880)).toBe('2 天');
    expect(formatFloatMinutes(null)).toBe('—');
  });
});
