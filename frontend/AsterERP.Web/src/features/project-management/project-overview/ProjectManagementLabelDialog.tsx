import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';

import { createProjectManagementProjectLabel, deleteProjectManagementProjectLabel, getProjectManagementLabels, updateProjectManagementProjectLabel } from '../../../api/project-management/projectManagement.api';
import type { ProjectManagementLabel } from '../../../api/project-management/projectManagement.types';
import { usePermission } from '../../../core/auth/usePermission';
import { useI18n } from '../../../core/i18n/I18nProvider';
import { projectManagementQueryKeys } from '../../../core/query/projectManagementQueryKeys';
import { useApiMutation } from '../../../core/query/useApiMutation';
import { useConfirm } from '../../../shared/feedback/useConfirm';
import { useMessage } from '../../../shared/feedback/useMessage';
import { PmBox, PmButton, PmDialog, PmFormInput, PmIcon, PmIconButton, PmRow, PmStack, PmText } from '../../../ui/project-management';
import { useProjectManagementWorkspaceScope } from '../state/projectManagementWorkspaceScope';

export function ProjectManagementLabelDialog({ open, projectId, onClose }: { open: boolean; projectId: string; onClose: () => void }) {
  const { translate } = useI18n();
  const scope = useProjectManagementWorkspaceScope();
  const queryClient = useQueryClient();
  const message = useMessage();
  const confirm = useConfirm();
  const { hasPermission: canManage } = usePermission('project-management:label:manage');
  const [labelName, setLabelName] = useState('');
  const [color, setColor] = useState('#64748B');
  const labelsQuery = useQuery({ enabled: open && scope.isAvailable && Boolean(projectId), queryKey: projectManagementQueryKeys.labels(scope, projectId), queryFn: ({ signal }) => getProjectManagementLabels(projectId, signal) });
  const invalidate = () => void queryClient.invalidateQueries({ queryKey: projectManagementQueryKeys.labels(scope, projectId) });
  const createMutation = useApiMutation({ mutationFn: () => createProjectManagementProjectLabel(projectId, { labelName: labelName.trim(), color }), onSuccess: () => { setLabelName(''); message.success(translate('projectManagement.home.labels.created')); invalidate(); }, onError: () => message.error(translate('projectManagement.home.labels.createFailed')) });
  const updateMutation = useApiMutation({ mutationFn: (label: ProjectManagementLabel) => updateProjectManagementProjectLabel(projectId, label.id, { labelName: label.labelName, color: label.color, versionNo: label.versionNo }), onSuccess: () => { message.success(translate('projectManagement.home.labels.updated')); invalidate(); }, onError: () => message.error(translate('projectManagement.home.labels.updateFailed')) });
  const deleteMutation = useApiMutation({ mutationFn: (label: ProjectManagementLabel) => deleteProjectManagementProjectLabel(projectId, label.id, label.versionNo), onSuccess: () => { message.success(translate('projectManagement.home.labels.deleted')); invalidate(); }, onError: () => message.error(translate('projectManagement.home.labels.deleteFailed')) });
  const labels = normalizeLabels(labelsQuery.data?.data);
  return <PmDialog actions={<PmButton color="inherit" onClick={onClose}>{translate('projectManagement.home.cancel')}</PmButton>} onClose={onClose} open={open} title={translate('projectManagement.home.labels.title')}>
    <PmStack spacing={1.5}>
      <PmText color="text.secondary" fontSize=".78rem">{translate('projectManagement.home.labels.description')}</PmText>
      {canManage && <PmBox component="form" onSubmit={event => { event.preventDefault(); if (labelName.trim()) createMutation.mutate(); }} sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: 'minmax(0, 1fr) 88px auto' }, gap: 1, alignItems: 'center' }}>
        <PmFormInput autoFocus label={translate('projectManagement.home.labels.name')} value={labelName} onChange={event => setLabelName(event.target.value)} />
        <PmFormInput aria-label={translate('projectManagement.home.labels.color')} label={translate('projectManagement.home.labels.color')} type="color" value={color} onChange={event => setColor(event.target.value.toUpperCase())} />
        <PmButton disabled={!labelName.trim() || createMutation.isPending} loading={createMutation.isPending} type="submit" variant="contained">{translate('projectManagement.home.labels.create')}</PmButton>
      </PmBox>}
      {labelsQuery.isError ? <PmText color="error.main">{translate('projectManagement.home.labels.loadFailed')}</PmText> : labels.length === 0 ? <PmText color="text.secondary">{translate('projectManagement.home.labels.empty')}</PmText> : <PmBox sx={{ display: 'grid', gap: .5 }}>{labels.map(label => <PmRow key={label.id} sx={{ gridTemplateColumns: 'minmax(0, 1fr) auto auto', minHeight: 44 }}><PmBox sx={{ display: 'flex', alignItems: 'center', gap: 1, minWidth: 0 }}><PmBox sx={{ width: 12, height: 12, borderRadius: '50%', bgcolor: label.color, flexShrink: 0 }} /><PmText noWrap>{label.labelName}</PmText></PmBox>{canManage && <PmFormInput aria-label={`${translate('projectManagement.home.labels.updateColor')}: ${label.labelName}`} label={translate('projectManagement.home.labels.color')} type="color" value={label.color} onChange={event => updateMutation.mutate({ ...label, color: event.target.value.toUpperCase() })} size="small" sx={{ width: 72 }} />} {canManage && <PmIconButton aria-label={translate('projectManagement.home.labels.delete')} onClick={() => void confirm({ title: translate('projectManagement.home.labels.deleteTitle'), content: `${translate('projectManagement.home.labels.deleteDescription')} ${label.labelName}`, confirmText: translate('projectManagement.home.labels.delete'), onConfirm: () => deleteMutation.mutate(label) })} size="small"><PmIcon name="trash" size={15} /></PmIconButton>}</PmRow>)}</PmBox>}
    </PmStack>
  </PmDialog>;
}

function normalizeLabels(value: unknown): ProjectManagementLabel[] {
  if (Array.isArray(value)) return value.filter(isProjectManagementLabel);
  if (!value || typeof value !== 'object' || !('items' in value)) return [];
  const items = (value as { items?: unknown }).items;
  return Array.isArray(items) ? items.filter(isProjectManagementLabel) : [];
}

function isProjectManagementLabel(value: unknown): value is ProjectManagementLabel {
  if (!value || typeof value !== 'object') return false;
  const label = value as Partial<ProjectManagementLabel>;
  return typeof label.id === 'string' && typeof label.labelName === 'string' && typeof label.color === 'string' && typeof label.versionNo === 'number';
}
