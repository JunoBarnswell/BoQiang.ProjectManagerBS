import { Box, Chip, Stack, Tooltip, Typography } from '@mui/material';
import { useMemo } from 'react';

import { buildGanttDependencyPaths, type DependencyAnalysisResponse, type GanttTaskRowLayout } from './dependencyAnalysisModel';

interface DependencyAnalysisOverlayProps {
  analysis: DependencyAnalysisResponse;
  rows: readonly GanttTaskRowLayout[];
  width: number;
  height: number;
  onSelectTask?: (taskId: string) => void;
}

/** 作为甘特内容层的绝对定位子项使用；它不拥有任何任务更新行为。 */
export function DependencyAnalysisOverlay({ analysis, rows, width, height, onSelectTask }: DependencyAnalysisOverlayProps) {
  const paths = useMemo(() => buildGanttDependencyPaths(analysis.links, rows), [analysis.links, rows]);
  const criticalByTaskId = useMemo(() => new Map(analysis.tasks.map((task) => [task.taskId, task.isCritical])), [analysis.tasks]);
  return (
    <Box sx={{ inset: 0, pointerEvents: 'none', position: 'absolute' }}>
      <svg aria-label="任务依赖连线" height={height} role="img" width={width}>
        <defs>
          <marker id="dependency-arrow" markerHeight="7" markerWidth="8" orient="auto" refX="7" refY="3.5">
            <path d="M0,0 L8,3.5 L0,7 Z" fill="#64748b" />
          </marker>
          <marker id="critical-dependency-arrow" markerHeight="7" markerWidth="8" orient="auto" refX="7" refY="3.5">
            <path d="M0,0 L8,3.5 L0,7 Z" fill="#d32f2f" />
          </marker>
        </defs>
        {paths.map((path) => (
          <g key={path.dependencyId}>
            <path
              d={path.d}
              fill="none"
              markerEnd={`url(#${path.isCritical ? 'critical-dependency-arrow' : 'dependency-arrow'})`}
              stroke={path.isCritical ? '#d32f2f' : '#64748b'}
              strokeDasharray={path.isWarning ? '4 3' : undefined}
              strokeWidth={path.isCritical ? 2.5 : 1.5}
            />
          </g>
        ))}
      </svg>
      <Stack direction="row" spacing={0.75} sx={{ left: 8, pointerEvents: 'auto', position: 'absolute', top: 8 }}>
        <Chip color="error" label={`关键任务 ${criticalByTaskId.size === 0 ? 0 : [...criticalByTaskId.values()].filter(Boolean).length}`} size="small" variant="outlined" />
        {analysis.diagnostics.length > 0 && (
          <Tooltip title={analysis.diagnostics.map((item) => item.message).join('\n')}>
            <Chip color="warning" label={`依赖异常 ${analysis.diagnostics.length}`} size="small" />
          </Tooltip>
        )}
      </Stack>
      {analysis.diagnostics.length > 0 && (
        <Box sx={{ bottom: 8, maxWidth: 440, pointerEvents: 'auto', position: 'absolute', right: 8 }}>
          {analysis.diagnostics.slice(0, 3).map((diagnostic) => (
            <Typography
              color={diagnostic.severity === 'Error' ? 'error.main' : 'warning.dark'}
              key={`${diagnostic.code}-${diagnostic.dependencyId ?? diagnostic.taskIds.join('-')}`}
              onClick={() => diagnostic.taskIds[0] && onSelectTask?.(diagnostic.taskIds[0])}
              sx={{ bgcolor: 'background.paper', borderRadius: 1, cursor: onSelectTask ? 'pointer' : 'default', px: 1, py: 0.5 }}
              variant="caption"
            >
              {diagnostic.message}
            </Typography>
          ))}
        </Box>
      )}
    </Box>
  );
}
