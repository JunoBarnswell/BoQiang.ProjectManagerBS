import { useNavigate } from 'react-router-dom';

import type { ProjectManagementOverviewItem } from '../../../api/project-management/projectManagement.types';
import { PmBox, PmButton, PmChip, PmSection, PmSurface, PmText } from '../../../ui/project-management';

interface ProjectOverviewDashboardProps {
  projectId: string;
  project: ProjectManagementOverviewItem;
}

const typeLabels: Record<string, string> = {
  Epic: '史诗',
  Story: '用户故事',
  Requirement: '需求',
  Task: '任务',
  Bug: '缺陷',
};

const statusLabels: Record<string, string> = {
  Todo: '待开始',
  InProgress: '进行中',
  Blocked: '已阻塞',
  Done: '已完成',
  Cancelled: '已取消',
};

export function ProjectOverviewDashboard({ projectId, project }: ProjectOverviewDashboardProps) {
  const navigate = useNavigate();
  const openWorkItems = (params?: Record<string, string>) => {
    const query = new URLSearchParams(params);
    navigate(`/platform/project-management/projects/${encodeURIComponent(projectId)}/tasks${query.size ? `?${query.toString()}` : ''}`);
  };
  const metrics = [
    { label: '需求总数', value: project.taskCount, action: () => openWorkItems() },
    { label: '已完成', value: project.completedTaskCount, action: () => openWorkItems({ status: 'Done' }) },
    { label: '进行中', value: project.inProgressTaskCount, action: () => openWorkItems({ status: 'InProgress' }) },
    { label: '逾期', value: project.overdueTaskCount, action: () => openWorkItems({ due: 'overdue' }) },
    { label: '阻塞', value: project.blockedTaskCount, action: () => openWorkItems({ status: 'Blocked' }) },
  ];
  const typeDistribution = project.workItemTypeDistribution ?? [];
  const statusDistribution = project.statusDistribution ?? [];
  const people = project.people ?? [];

  return <PmBox sx={{ display: 'grid', gap: 1.5, mb: 2 }}>
    <PmBox sx={{ display: 'grid', gridTemplateColumns: 'repeat(5, minmax(0, 1fr))', gap: 1.25 }}>
      {metrics.map(metric => <PmSurface key={metric.label} onClick={metric.action} sx={{ cursor: 'pointer', minHeight: 92, p: 1.5, '&:hover': { borderColor: 'primary.main' } }}>
        <PmText color="text.secondary" fontSize=".74rem">{metric.label}</PmText>
        <PmText fontSize="1.65rem" fontWeight={750} sx={{ mt: .5 }}>{metric.value}</PmText>
      </PmSurface>)}
    </PmBox>
    <PmBox sx={{ display: 'grid', gridTemplateColumns: 'minmax(0, 1.2fr) minmax(0, 1fr)', gap: 1.5 }}>
      <PmSurface>
        <PmSection>
          <PmBox sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 1 }}>
            <PmText variant="h2" fontSize=".95rem" fontWeight={700}>需求分布</PmText>
            <PmButton onClick={() => openWorkItems()} variant="text">打开需求工作台</PmButton>
          </PmBox>
          <PmBox sx={{ display: 'grid', gap: 1.1, mt: 1.25 }}>
            {typeDistribution.length ? typeDistribution.map(item => <DistributionRow key={item.key} label={typeLabels[item.key] ?? item.key} count={item.count} percent={item.percent} />) : <PmText color="text.secondary" fontSize=".78rem">暂无需求数据</PmText>}
          </PmBox>
        </PmSection>
      </PmSurface>
      <PmSurface>
        <PmSection>
          <PmText variant="h2" fontSize=".95rem" fontWeight={700}>状态分布</PmText>
          <PmBox sx={{ display: 'flex', flexWrap: 'wrap', gap: .75, mt: 1.25 }}>
            {statusDistribution.length ? statusDistribution.map(item => <PmChip key={item.key} label={`${statusLabels[item.key] ?? item.key} ${item.count}`} color={item.key === 'Blocked' ? 'error' : item.key === 'Done' ? 'success' : 'default'} onClick={() => openWorkItems({ status: item.key })} />) : <PmText color="text.secondary" fontSize=".78rem">暂无状态数据</PmText>}
          </PmBox>
          <PmText color="text.secondary" fontSize=".75rem" sx={{ display: 'block', mt: 1.5 }}>整体进度 {Math.round(project.taskProgressPercent)}%</PmText>
          <progress max={100} value={project.taskProgressPercent} style={{ width: '100%', marginTop: 6 }} />
        </PmSection>
      </PmSurface>
    </PmBox>
    <PmBox sx={{ display: 'grid', gridTemplateColumns: 'minmax(0, 1.2fr) minmax(0, 1fr)', gap: 1.5 }}>
      <PmSurface>
        <PmSection>
          <PmText variant="h2" fontSize=".95rem" fontWeight={700}>团队工作量</PmText>
          <PmBox sx={{ display: 'grid', gap: .5, mt: 1 }}>
            {people.length ? people.map(person => <PmBox key={person.userId} sx={{ display: 'grid', gridTemplateColumns: 'minmax(0, 1fr) 80px 80px', alignItems: 'center', gap: 1, minHeight: 34 }}><PmText fontSize=".78rem" noWrap>{person.displayName || person.userId}</PmText><PmText color="text.secondary" fontSize=".75rem">{person.taskCount} 项</PmText><PmText color={person.overdueTaskCount ? 'error.main' : 'text.secondary'} fontSize=".75rem">{person.overdueTaskCount ? `${person.overdueTaskCount} 逾期` : '无逾期'}</PmText></PmBox>) : <PmText color="text.secondary" fontSize=".78rem">暂无负责人数据</PmText>}
          </PmBox>
        </PmSection>
      </PmSurface>
      <PmSurface>
        <PmSection>
          <PmText variant="h2" fontSize=".95rem" fontWeight={700}>风险摘要</PmText>
          <PmBox sx={{ display: 'grid', gap: .9, mt: 1 }}>
            <RiskRow label="逾期需求" value={project.riskSummary?.overdueTaskCount ?? project.overdueTaskCount} danger />
            <RiskRow label="阻塞需求" value={project.riskSummary?.blockedTaskCount ?? project.blockedTaskCount} danger />
            <RiskRow label="7 天内到期" value={project.riskSummary?.dueSoonIncompleteTaskCount ?? 0} />
            <RiskRow label="WIP 超限" value={project.riskSummary?.isWipExceeded ? project.riskSummary.wipExceededBy : 0} danger={Boolean(project.riskSummary?.isWipExceeded)} />
          </PmBox>
        </PmSection>
      </PmSurface>
    </PmBox>
  </PmBox>;
}

function DistributionRow({ label, count, percent }: { label: string; count: number; percent: number }) {
  return <PmBox sx={{ display: 'grid', gridTemplateColumns: '92px minmax(0, 1fr) 48px', alignItems: 'center', gap: 1 }}><PmText fontSize=".76rem">{label}</PmText><progress max={100} value={percent} /><PmText color="text.secondary" fontSize=".74rem" sx={{ textAlign: 'right' }}>{count} · {percent}%</PmText></PmBox>;
}

function RiskRow({ label, value, danger = false }: { label: string; value: number; danger?: boolean }) {
  return <PmBox sx={{ display: 'flex', justifyContent: 'space-between', gap: 1 }}><PmText color="text.secondary" fontSize=".76rem">{label}</PmText><PmText color={danger && value ? 'error.main' : 'text.primary'} fontSize=".78rem" fontWeight={700}>{value}</PmText></PmBox>;
}
