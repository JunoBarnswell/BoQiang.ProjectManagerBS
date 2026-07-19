import { Alert, Box, Chip, Divider, Stack, Typography } from '@mui/material';

import { formatShiftMinutes, type DependencyImpactPreviewResponse } from './dependencyAnalysisModel';

interface DependencyImpactPreviewPanelProps {
  preview: DependencyImpactPreviewResponse | undefined;
}

/** 只展示服务端计算的建议；保存操作仍必须由宿主调用原任务更新流程并让用户确认。 */
export function DependencyImpactPreviewPanel({ preview }: DependencyImpactPreviewPanelProps) {
  if (!preview) return null;
  const riskyMilestones = preview.preview.milestoneImpacts.filter((item) => item.isAtRisk);
  return (
    <Stack aria-live="polite" spacing={1.25}>
      <Alert severity="info">以下顺延为预览建议，尚未修改任何任务。请逐项确认后再保存。</Alert>
      {preview.suggestions.length === 0 ? <Typography variant="body2">本次日期调整不会要求后续任务顺延。</Typography> : preview.suggestions.map((suggestion) => (
        <Box key={suggestion.taskId} sx={{ border: 1, borderColor: 'divider', borderRadius: 1.5, p: 1.25 }}>
          <Stack direction="row" spacing={1} sx={{ alignItems: 'center', justifyContent: 'space-between' }}>
            <Typography sx={{ fontWeight: 600 }} variant="body2">{suggestion.title}</Typography>
            <Chip color="warning" label={formatShiftMinutes(suggestion.shiftMinutes)} size="small" />
          </Stack>
          <Typography color="text.secondary" variant="caption">{suggestion.currentStart} → {suggestion.suggestedStart}</Typography>
        </Box>
      ))}
      {riskyMilestones.length > 0 && <><Divider /><Alert severity="warning">{riskyMilestones.map((item) => `里程碑“${item.name}”预计延误 ${formatShiftMinutes(item.delayMinutes).replace('顺延 ', '')}`).join('；')}</Alert></>}
    </Stack>
  );
}
