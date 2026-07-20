import { PmBox, PmChip, PmDivider, PmNotice, PmStack, PmText } from '../../../../ui/project-management';

import { formatShiftMinutes, type DependencyImpactPreviewResponse } from './dependencyAnalysisModel';

interface DependencyImpactPreviewPanelProps {
  preview: DependencyImpactPreviewResponse | undefined;
}

/** 只展示服务端计算的建议；保存操作仍必须由宿主调用原任务更新流程并让用户确认。 */
export function DependencyImpactPreviewPanel({ preview }: DependencyImpactPreviewPanelProps) {
  if (!preview) return null;
  const riskyMilestones = preview.preview.milestoneImpacts.filter((item) => item.isAtRisk);
  return (
    <PmStack aria-live="polite" spacing={1.25}>
      <PmNotice severity="info">以下顺延为预览建议，尚未修改任何任务。请逐项确认后再保存。</PmNotice>
      {preview.suggestions.length === 0 ? <PmText fontSize=".78rem">本次日期调整不会要求后续任务顺延。</PmText> : preview.suggestions.map((suggestion) => (
        <PmBox key={suggestion.taskId} sx={{ border: 1, borderColor: 'divider', borderRadius: 1.5, p: 1.25 }}>
          <PmStack direction="row" spacing={1} sx={{ alignItems: 'center', justifyContent: 'space-between' }}>
            <PmText fontSize=".78rem" fontWeight={600}>{suggestion.title}</PmText>
            <PmChip color="warning" label={formatShiftMinutes(suggestion.shiftMinutes)} />
          </PmStack>
          <PmText color="text.secondary" fontSize=".7rem">{suggestion.currentStart} → {suggestion.suggestedStart}</PmText>
        </PmBox>
      ))}
      {riskyMilestones.length > 0 && <><PmDivider /><PmNotice severity="warning">{riskyMilestones.map((item) => `里程碑“${item.name}”预计延误 ${formatShiftMinutes(item.delayMinutes).replace('顺延 ', '')}`).join('；')}</PmNotice></>}
    </PmStack>
  );
}
