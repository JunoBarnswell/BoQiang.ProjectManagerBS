export interface DependencyAnalysisTask {
  taskId: string;
  title: string;
  milestoneId?: string | null;
  plannedStart?: string | null;
  plannedFinish?: string | null;
  earliestStart?: string | null;
  earliestFinish?: string | null;
  latestStart?: string | null;
  latestFinish?: string | null;
  totalFloatMinutes?: number | null;
  isCritical: boolean;
  isSchedulable: boolean;
}

export interface DependencyAnalysisLink {
  dependencyId: string;
  predecessorTaskId: string;
  successorTaskId: string;
  dependencyType: string;
  lagMinutes: number;
  isRenderable: boolean;
  isCritical: boolean;
}

export interface DependencyAnalysisDiagnostic {
  code: string;
  severity: 'Error' | 'Warning' | 'Info' | string;
  message: string;
  taskIds: string[];
  dependencyId?: string | null;
}

export interface DependencyAnalysisMilestoneImpact {
  milestoneId: string;
  name: string;
  dueDate?: string | null;
  forecastFinish?: string | null;
  delayMinutes: number;
  isAtRisk: boolean;
  affectedTaskIds: string[];
}

export interface DependencyAnalysisResponse {
  tasks: DependencyAnalysisTask[];
  links: DependencyAnalysisLink[];
  milestoneImpacts: DependencyAnalysisMilestoneImpact[];
  diagnostics: DependencyAnalysisDiagnostic[];
  projectEarliestFinish?: string | null;
}

export interface DependencyScheduleSuggestion {
  taskId: string;
  title: string;
  currentStart: string;
  suggestedStart: string;
  currentFinish: string;
  suggestedFinish: string;
  shiftMinutes: number;
  requiresManualConfirmation: boolean;
}

export interface DependencyImpactPreviewResponse {
  baseline: DependencyAnalysisResponse;
  preview: DependencyAnalysisResponse;
  suggestions: DependencyScheduleSuggestion[];
}

export interface GanttTaskRowLayout {
  taskId: string;
  left: number;
  top: number;
  width: number;
  height: number;
}

export interface GanttDependencyPath {
  dependencyId: string;
  d: string;
  isCritical: boolean;
  isWarning: boolean;
  label?: string;
}

/** 将后端依赖快照投影为视口无关的 SVG 路径。缺失行或异常依赖保留给诊断面板，不绘制误导性的连线。 */
export function buildGanttDependencyPaths(
  links: readonly DependencyAnalysisLink[],
  rows: readonly GanttTaskRowLayout[],
): GanttDependencyPath[] {
  const rowsByTaskId = new Map(rows.map((row) => [row.taskId, row]));
  return links.flatMap((link) => {
    if (!link.isRenderable) return [];
    const from = rowsByTaskId.get(link.predecessorTaskId);
    const to = rowsByTaskId.get(link.successorTaskId);
    if (!from || !to) return [];
    const startX = from.left + from.width;
    const startY = from.top + from.height / 2;
    const endX = to.left;
    const endY = to.top + to.height / 2;
    const bend = Math.max(18, Math.abs(endX - startX) / 2);
    return [{
      dependencyId: link.dependencyId,
      d: `M ${startX} ${startY} H ${startX + bend} V ${endY} H ${endX}`,
      isCritical: link.isCritical,
      isWarning: false,
      label: link.lagMinutes > 0 ? `+${link.lagMinutes}m` : undefined,
    }];
  });
}

export function formatFloatMinutes(value: number | null | undefined): string {
  if (value === null || value === undefined) return '—';
  if (value === 0) return '关键路径';
  if (value % 1440 === 0) return `${value / 1440} 天`;
  if (value % 60 === 0) return `${value / 60} 小时`;
  return `${value} 分钟`;
}

export function formatShiftMinutes(value: number): string {
  if (value % 1440 === 0) return `顺延 ${value / 1440} 天`;
  if (value % 60 === 0) return `顺延 ${value / 60} 小时`;
  return `顺延 ${value} 分钟`;
}
