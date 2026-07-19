import { useQuery } from '@tanstack/react-query';

import { getTaskDependencyAnalysis } from './dependencyAnalysisApi';

/** 宿主甘特图传入当前项目；项目为空时不发送请求，也不会暴露跨项目缓存。 */
export function useDependencyAnalysis(projectId: string | undefined, enabled = true) {
  return useQuery({
    enabled: enabled && Boolean(projectId),
    queryFn: ({ signal }) => getTaskDependencyAnalysis(projectId!, signal),
    queryKey: ['project-management', 'gantt', 'dependency-analysis', projectId],
    staleTime: 15_000,
  });
}
