import { useMutation, useQuery } from '@tanstack/react-query';
import { Box, Stack as MuiStack, Typography as MuiTypography } from '@mui/material';
import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';

import { createProjectManagementProject, getProjectManagementOverview } from '../../../api/project-management/projectManagement.api';
import type { ProjectManagementProjectUpsertRequest } from '../../../api/project-management/projectManagement.types';
import { projectManagementQueryKeys } from '../../../core/query/projectManagementQueryKeys';
import { ProjectCreateDialog } from '../project-create/ProjectCreateDialog';
import { toProjectManagementPlatformRoute } from '../state/projectManagementPlatformRoutes';
import { useProjectManagementWorkspaceScope } from '../state/projectManagementWorkspaceScope';
import { PmIcon } from '../../../ui/project-management';

const Stack = MuiStack as any;
const Typography = MuiTypography as any;
const newProject: ProjectManagementProjectUpsertRequest = { projectCode: '', projectName: '', status: 'Planning', priority: 'Medium' };

export function ProjectSelectorScreen() {
  const scope = useProjectManagementWorkspaceScope();
  const navigate = useNavigate();
  const [keyword, setKeyword] = useState('');
  const [createOpen, setCreateOpen] = useState(false);
  const query = useQuery({
    enabled: scope.isAvailable,
    queryKey: projectManagementQueryKeys.overview(scope, { keyword, pageIndex: 1, pageSize: 50 }),
    queryFn: ({ signal }) => getProjectManagementOverview({ keyword, pageIndex: 1, pageSize: 50 }, signal),
  });
  const create = useMutation({ mutationFn: createProjectManagementProject, onSuccess: (result) => navigate(toProjectManagementPlatformRoute(`projects/${encodeURIComponent(result.data.id)}/overview`)) });
  const projects = useMemo(() => query.data?.data.items ?? [], [query.data]);

  return <Box sx={{ display: 'flex', flex: '1 1 auto', minHeight: 0, p: { xs: 2, md: 4 }, bgcolor: '#f7f8fb' }}>
    <Stack spacing={2} sx={{ flex: '1 1 auto', minWidth: 0, maxWidth: 1440, mx: 'auto' }}>
      <Stack alignItems={{ xs: 'flex-start', md: 'center' }} direction={{ xs: 'column', md: 'row' }} justifyContent="space-between" spacing={1.5}>
        <Stack spacing={0.25}><Typography fontWeight={750} variant="h5">项目</Typography><Typography color="text.secondary" variant="body2">选择一个项目，进入项目概览和需求工作台。</Typography></Stack>
        <button className="pm-primary-button" onClick={() => setCreateOpen(true)} type="button"><PmIcon name="plus" size={16} /> 新建项目</button>
      </Stack>
      <input aria-label="搜索项目" className="pm-project-search" onChange={(event) => setKeyword(event.target.value)} placeholder="搜索项目名称或编号" value={keyword} />
      <Box sx={{ overflow: 'hidden', bgcolor: '#fff', border: '1px solid #e7eaf0', borderRadius: 2 }}>
        <Box sx={{ display: 'grid', gridTemplateColumns: 'minmax(260px, 2fr) repeat(4, minmax(100px, 1fr))', gap: 2, px: 2.25, py: 1.25, bgcolor: '#fbfcfe', color: 'text.secondary', fontSize: 12 }}><span>名称</span><span>健康度</span><span>负责人</span><span>目标日期</span><span>状态</span></Box>
        {query.isLoading ? <Typography sx={{ p: 4, textAlign: 'center' }}>正在加载项目…</Typography> : null}
        {query.isError ? <Typography color="error" sx={{ p: 4, textAlign: 'center' }}>项目列表加载失败，请刷新重试。</Typography> : null}
        {!query.isLoading && !query.isError && projects.length === 0 ? <Typography color="text.secondary" sx={{ p: 6, textAlign: 'center' }}>没有匹配项目</Typography> : null}
        {projects.map((item) => <button className="pm-project-selector-row" key={item.project.id} onClick={() => navigate(toProjectManagementPlatformRoute(`projects/${encodeURIComponent(item.project.id)}/overview`))} type="button">
          <Stack alignItems="center" direction="row" spacing={1}><PmIcon name="folder" /><Stack alignItems="flex-start"><Typography fontWeight={650}>{item.project.projectName}</Typography><Typography color="text.secondary" variant="caption">{item.project.projectCode}</Typography></Stack></Stack>
          <span>{item.health}</span><span>{item.project.ownerDisplayName ?? item.project.ownerUserId}</span><span>{item.project.dueDate ? new Date(item.project.dueDate).toLocaleDateString() : '—'}</span><span>{item.project.status}</span>
        </button>)}
      </Box>
    </Stack>
    <ProjectCreateDialog editing={false} initialValue={newProject} onClose={() => setCreateOpen(false)} onSubmit={(value) => create.mutate(value)} open={createOpen} pending={create.isPending} />
  </Box>;
}
