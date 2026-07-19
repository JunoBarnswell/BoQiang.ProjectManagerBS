import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';

import {
  archiveProjectManagementProject,
  createProjectManagementProject,
  deleteProjectManagementProject,
  getProjectManagementHomeProjects,
  getProjectManagementHomeSummary,
  updateProjectManagementProject,
} from '../../../api/project-management/projectManagement.api';
import type {
  ProjectManagementHomeHealth,
  ProjectManagementHomeProjectItem,
  ProjectManagementHomeQuery,
  ProjectManagementProjectUpsertRequest,
} from '../../../api/project-management/projectManagement.types';
import { isHttpError } from '../../../core/http/httpError';
import { formatMessage } from '../../../core/i18n/formatMessage';
import { useI18n } from '../../../core/i18n/I18nProvider';
import { projectManagementQueryKeys } from '../../../core/query/projectManagementQueryKeys';
import { useApiMutation } from '../../../core/query/useApiMutation';
import { useAuthStore } from '../../../core/state';
import { PermissionButton } from '../../../shared/auth/PermissionButton';
import { useConfirm } from '../../../shared/feedback/useConfirm';
import { useMessage } from '../../../shared/feedback/useMessage';
import type { FormFieldConfig } from '../../../shared/forms/formTypes';
import { ModalForm } from '../../../shared/forms/ModalForm';
import { Page403 } from '../../../shared/status/Page403';
import { projectCenterPreferenceKey, readProjectCenterPreferences, rememberRecentProject, toggleProjectFavorite, writeProjectCenterPreferences, type ProjectCenterCollection, type ProjectCenterPreferences } from '../state/projectCenterPreferences';
import { useProjectManagementWorkspaceScope } from '../state/projectManagementWorkspaceScope';

import { useProjectHomeRealtime } from './useProjectHomeRealtime';

const healthLabels: Record<string, string> = {
  Completed: 'projectManagement.home.health.Completed', UpdateMissing: 'projectManagement.home.health.UpdateMissing', AtRisk: 'projectManagement.home.health.AtRisk', OffTrack: 'projectManagement.home.health.OffTrack', OnTrack: 'projectManagement.home.health.OnTrack', NoUpdateExpected: 'projectManagement.home.health.NoUpdateExpected',
};
const statusLabels: Record<string, string> = { Planning: 'projectManagement.home.status.Planning', Active: 'projectManagement.home.status.Active', Paused: 'projectManagement.home.status.Paused', Completed: 'projectManagement.home.status.Completed', Canceled: 'projectManagement.home.status.Canceled', Archived: 'projectManagement.home.status.Archived' };
const priorityLabels: Record<string, string> = { Low: 'projectManagement.home.priority.Low', Medium: 'projectManagement.home.priority.Medium', High: 'projectManagement.home.priority.High', Urgent: 'projectManagement.home.priority.Urgent' };
const formFields: FormFieldConfig<ProjectManagementProjectUpsertRequest>[] = [
  { label: 'projectManagement.home.field.code', name: 'projectCode', required: true, type: 'text' },
  { label: 'projectManagement.home.field.name', name: 'projectName', required: true, type: 'text' },
  { label: 'projectManagement.home.status', name: 'status', type: 'select', options: Object.keys(statusLabels).map(value => ({ label: statusLabels[value], value })) },
  { label: 'projectManagement.home.priority', name: 'priority', type: 'select', options: Object.keys(priorityLabels).map(value => ({ label: priorityLabels[value], value })) },
  { label: 'projectManagement.home.field.startDate', name: 'startDate', type: 'date' },
  { label: 'projectManagement.home.field.targetDate', name: 'dueDate', type: 'date' },
  { label: 'projectManagement.home.field.description', name: 'description', type: 'textarea', rows: 4, span: 2 },
];
const emptyForm: ProjectManagementProjectUpsertRequest = { projectCode: '', projectName: '', status: 'Planning', priority: 'Medium', progressPercent: 0, versionNo: 0 };

export function ProjectHomeShell() {
  const scope = useProjectManagementWorkspaceScope();
  const userId = useAuthStore(state => state.user?.userId ?? '');
  const [searchParams, setSearchParams] = useSearchParams();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { translate } = useI18n();
  const confirm = useConfirm();
  const message = useMessage();
  const [insightsOpen, setInsightsOpen] = useState(() => localStorage.getItem('pm-home-insights') !== 'false');
  const [insightTab, setInsightTab] = useState<'health' | 'leads'>('health');
  const [density, setDensity] = useState<'compact' | 'default' | 'comfortable'>(() => (localStorage.getItem('pm-home-density') as 'compact' | 'default' | 'comfortable') || 'default');
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<ProjectManagementHomeProjectItem | null>(null);
  const [form, setForm] = useState(emptyForm);
  const preferenceKey = projectCenterPreferenceKey(userId, scope.tenantId, scope.appCode);
  const [preferences, setPreferences] = useState<ProjectCenterPreferences>(() => readProjectCenterPreferences(preferenceKey));
  const localizedFormFields = useMemo(() => formFields.map((field) => ({ ...field, label: translate(field.label), options: field.options?.map((option) => ({ ...option, label: translate(option.label) })) })), [translate]);
  const focusIndex = useRef(0);

  useEffect(() => setPreferences(readProjectCenterPreferences(preferenceKey)), [preferenceKey]);
  const savePreference = useCallback((next: ProjectCenterPreferences) => { setPreferences(next); writeProjectCenterPreferences(preferenceKey, next); }, [preferenceKey]);
  const setParam = useCallback((name: string, value?: string) => {
    const next = new URLSearchParams(searchParams);
    if (value) next.set(name, value); else next.delete(name);
    setSearchParams(next);
  }, [searchParams, setSearchParams]);
  const collection = (searchParams.get('collection') as ProjectCenterCollection | null) || 'all';
  const query = useMemo<ProjectManagementHomeQuery>(() => ({
    collection,
    keyword: searchParams.get('keyword') || undefined,
    health: (searchParams.get('health') as ProjectManagementHomeHealth | null) || undefined,
    priority: searchParams.get('priority') || undefined,
    leadUserId: searchParams.get('leadUserId') || undefined,
    status: searchParams.get('status') || undefined,
    targetDateFrom: searchParams.get('targetDateFrom') || undefined,
    targetDateTo: searchParams.get('targetDateTo') || undefined,
    includeArchived: searchParams.get('includeArchived') === 'true',
    sortBy: searchParams.get('sortBy') || 'updated',
    sortDirection: (searchParams.get('sortDirection') as 'asc' | 'desc' | null) || 'desc',
    pageIndex: Number(searchParams.get('pageIndex') || 1),
    pageSize: 50,
  }), [collection, searchParams]);
  const projectsQuery = useQuery({ enabled: scope.isAvailable, queryKey: projectManagementQueryKeys.homeProjects(scope, query), queryFn: ({ signal }) => getProjectManagementHomeProjects(query, signal) });
  const summaryQuery = useQuery({ enabled: scope.isAvailable && insightsOpen, queryKey: projectManagementQueryKeys.homeSummary(scope, query), queryFn: ({ signal }) => getProjectManagementHomeSummary({ ...query, pageIndex: 1, pageSize: 50 }, signal) });
  const realtime = useProjectHomeRealtime('/hubs/system-notification', scope, scope.isAvailable);

  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: projectManagementQueryKeys.homeProjectsRoot(scope) });
    void queryClient.invalidateQueries({ queryKey: projectManagementQueryKeys.homeSummaryRoot(scope) });
  };
  const closeDialog = () => { setDialogOpen(false); setEditing(null); setForm(emptyForm); };
  const saveMutation = useApiMutation({
    mutationFn: () => editing ? updateProjectManagementProject(editing.id, form) : createProjectManagementProject(form),
    onError: error => message.error(isHttpError(error) && error.status === 409 ? translate('projectManagement.home.conflict') : translate('projectManagement.home.createFailed')),
    onSuccess: async () => { message.success(editing ? translate('projectManagement.home.updateSuccess') : translate('projectManagement.home.createSuccess')); closeDialog(); refresh(); },
  });
  const archiveMutation = useApiMutation({ mutationFn: (row: ProjectManagementHomeProjectItem) => archiveProjectManagementProject(row.id, row.versionNo), onSuccess: refresh, onError: () => message.error(translate('projectManagement.home.archiveFailed')) });
  const deleteMutation = useApiMutation({ mutationFn: (row: ProjectManagementHomeProjectItem) => deleteProjectManagementProject(row.id, row.versionNo), onSuccess: refresh, onError: () => message.error(translate('projectManagement.home.deleteFailed')) });
  const rows = projectsQuery.data?.data.items ?? [];
  const visibleRows = collection === 'favorites' ? rows.filter(row => preferences.favoriteProjectIds.includes(row.id)) : collection === 'recent' ? [...rows].sort((a, b) => preferences.recentProjectIds.indexOf(a.id) - preferences.recentProjectIds.indexOf(b.id)) : rows;
  const openProject = (row: ProjectManagementHomeProjectItem) => { savePreference(rememberRecentProject(preferences, row.id)); navigate(`/platform/project-management/projects/${encodeURIComponent(row.id)}/overview`); };

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.target instanceof HTMLInputElement || event.target instanceof HTMLTextAreaElement || event.target instanceof HTMLSelectElement) return;
      if (event.key.toLowerCase() === 'c') { event.preventDefault(); setEditing(null); setForm(emptyForm); setDialogOpen(true); }
      if (event.key === 'ArrowDown') { focusIndex.current = Math.min(focusIndex.current + 1, Math.max(visibleRows.length - 1, 0)); document.querySelector<HTMLElement>(`[data-home-row="${focusIndex.current}"]`)?.focus(); }
      if (event.key === 'ArrowUp') { focusIndex.current = Math.max(focusIndex.current - 1, 0); document.querySelector<HTMLElement>(`[data-home-row="${focusIndex.current}"]`)?.focus(); }
      if (event.key === 'Enter') document.querySelector<HTMLElement>(`[data-home-row="${focusIndex.current}"]`)?.click();
      if (event.key.toLowerCase() === 'e') { const row = visibleRows[focusIndex.current]; if (row) { setEditing(row); setForm({ projectCode: row.projectCode, projectName: row.projectName, status: row.status, priority: row.priority, ownerUserId: row.ownerUserId, startDate: row.startDate, dueDate: row.targetDate, progressPercent: row.progressPercent, versionNo: row.versionNo }); setDialogOpen(true); } }
      if (event.key === 'Delete' && event.shiftKey) { const row = visibleRows[focusIndex.current]; if (row) confirm({ title: translate('projectManagement.home.deleteTitle'), content: formatMessage(translate('projectManagement.home.deleteDescription'), { name: row.projectName }), confirmText: translate('projectManagement.home.delete'), onConfirm: () => deleteMutation.mutate(row) }); }
    };
    window.addEventListener('keydown', onKeyDown); return () => window.removeEventListener('keydown', onKeyDown);
  }, [confirm, deleteMutation, translate, visibleRows]);

  if (!scope.isAvailable) return <Page403 />;
  const noResults = !projectsQuery.isLoading && visibleRows.length === 0;
  return <div className={`pm-home ${insightsOpen ? '' : 'pm-home--insights-hidden'}`}>
    <main className="pm-home__main">
      <div className="pm-home__tabbar"><button aria-label={translate('projectManagement.home.back')} onClick={() => navigate(-1)} type="button">←</button><button aria-label={translate('projectManagement.home.forward')} onClick={() => navigate(1)} type="button">→</button><span>{translate('projectManagement.home.title')}</span><button aria-label={translate('projectManagement.home.create')} onClick={() => { setEditing(null); setForm(emptyForm); setDialogOpen(true); }} type="button">＋</button></div>
      <header className="pm-home__title"><h1>{translate('projectManagement.home.title')}</h1><PermissionButton code="project-management:project:add" onClick={() => { setEditing(null); setForm(emptyForm); setDialogOpen(true); }}>＋</PermissionButton></header>
      <div className="pm-home__toolbar"><div className="pm-home__collections">{(['all', 'favorites', 'recent'] as ProjectCenterCollection[]).map(value => <button className={collection === value ? 'is-active' : ''} key={value} onClick={() => setParam('collection', value)} type="button">{translate(value === 'all' ? 'projectManagement.home.allProjects' : value === 'favorites' ? 'projectManagement.home.favorites' : 'projectManagement.home.recent')}</button>)}</div><div className="pm-home__toolbar-actions"><input aria-label={translate('projectManagement.home.search')} defaultValue={searchParams.get('keyword') || ''} onKeyDown={event => { if (event.key === 'Enter') setParam('keyword', event.currentTarget.value.trim() || undefined); }} placeholder={translate('projectManagement.home.search')} /><button aria-label={translate('projectManagement.home.filter')} className={searchParams.get('status') || searchParams.get('health') ? 'has-filter' : ''} onClick={() => setParam('health', searchParams.get('health') ? undefined : 'AtRisk')} type="button">{translate('projectManagement.home.filter')}</button><label>{translate('projectManagement.home.density')} <select aria-label={translate('projectManagement.home.density')} value={density} onChange={event => { setDensity(event.target.value as typeof density); localStorage.setItem('pm-home-density', event.target.value); }}><option value="compact">{translate('projectManagement.home.compact')}</option><option value="default">{translate('projectManagement.home.defaultDensity')}</option><option value="comfortable">{translate('projectManagement.home.comfortable')}</option></select></label><button aria-label={translate('projectManagement.home.insights')} onClick={() => { const next = !insightsOpen; setInsightsOpen(next); localStorage.setItem('pm-home-insights', String(next)); }} type="button">{translate('projectManagement.home.insights')}</button></div></div>
      <div className="pm-home__filterbar"><select aria-label={translate('projectManagement.home.allStatuses')} value={searchParams.get('status') || ''} onChange={event => setParam('status', event.target.value || undefined)}><option value="">{translate('projectManagement.home.allStatuses')}</option>{Object.keys(statusLabels).map(value => <option key={value} value={value}>{translate(statusLabels[value])}</option>)}</select><select aria-label={translate('projectManagement.home.allPriorities')} value={searchParams.get('priority') || ''} onChange={event => setParam('priority', event.target.value || undefined)}><option value="">{translate('projectManagement.home.allPriorities')}</option>{Object.keys(priorityLabels).map(value => <option key={value} value={value}>{translate(priorityLabels[value])}</option>)}</select>{(searchParams.get('status') || searchParams.get('priority') || searchParams.get('health') || searchParams.get('keyword')) && <button onClick={() => setSearchParams({ collection })} type="button">{translate('projectManagement.home.clearFilters')}</button>}</div>
      {projectsQuery.isLoading ? <div className="pm-home__skeleton" aria-label={translate('common.loading')}>{Array.from({ length: 8 }, (_, index) => <div key={index} />)}</div> : projectsQuery.isError ? <section className="pm-home__state"><h2>{translate('projectManagement.home.loadingFailed')}</h2><button onClick={() => void projectsQuery.refetch()} type="button">{translate('projectManagement.home.retry')}</button></section> : noResults ? <section className="pm-home__state"><h2>{searchParams.toString() ? translate('projectManagement.home.noResults') : translate('projectManagement.home.empty')}</h2><p>{translate('projectManagement.home.emptyDescription')}</p><PermissionButton code="project-management:project:add" onClick={() => { setEditing(null); setForm(emptyForm); setDialogOpen(true); }}>{translate('projectManagement.home.create')}</PermissionButton></section> : <div className={`pm-home__table pm-home__table--${density}`}><div className="pm-home__row pm-home__row--header"><span>{translate('projectManagement.home.name')}</span><span>{translate('projectManagement.home.health')}</span><span>{translate('projectManagement.home.priority')}</span><span>{translate('projectManagement.home.lead')}</span><span>{translate('projectManagement.home.targetDate')}</span><span>{translate('projectManagement.home.issues')}</span><span>{translate('projectManagement.home.status')}</span></div>{visibleRows.map((row, index) => <div className="pm-home__row" data-home-row={index} key={row.id} onClick={() => openProject(row)} onKeyDown={event => { if (event.key === 'Enter') openProject(row); }} role="button" tabIndex={0}><span className="pm-home__name"><i style={{ background: row.health === 'AtRisk' || row.health === 'OffTrack' ? '#ef6b73' : '#5cae8b' }} /> <strong>{row.projectName}</strong>{row.currentMilestoneName && <small>{row.currentMilestoneName}</small>}</span><span title={translate(healthLabels[row.health] || row.health)}><b className={`pm-home__health pm-home__health--${row.health}`}>●</b><small>{translate(healthLabels[row.health] || row.health)}</small></span><span className={`pm-home__priority pm-home__priority--${row.priority}`}>{translate(priorityLabels[row.priority] || row.priority)}</span><span title={row.ownerDisplayName}>{row.ownerDisplayName || '—'}</span><span className={row.targetDate && new Date(row.targetDate) < new Date() && row.status !== 'Completed' ? 'is-overdue' : ''}>{row.targetDate ? new Date(row.targetDate).toLocaleDateString() : '—'}</span><span title={`${translate('projectManagement.home.issues')}: ${row.issueCount} / ${row.openIssueCount}`}>{row.openIssueCount}</span><span>{translate(statusLabels[row.status] || row.status)}</span><div className="pm-home__row-actions"><button aria-label={translate('projectManagement.home.favorite')} onClick={event => { event.stopPropagation(); savePreference(toggleProjectFavorite(preferences, row.id)); }} type="button">{preferences.favoriteProjectIds.includes(row.id) ? '★' : '☆'}</button><button aria-label={translate('projectManagement.home.moreActions')} onClick={event => { event.stopPropagation(); confirm({ title: translate('projectManagement.home.archiveTitle'), content: formatMessage(translate('projectManagement.home.archiveDescription'), { name: row.projectName }), confirmText: translate('projectManagement.home.archive'), onConfirm: () => archiveMutation.mutate(row) }); }} type="button">⋯</button></div></div>)}</div>}
    </main>
    {insightsOpen && <aside className="pm-home__insights"><div className="pm-home__insight-tabs"><button className={insightTab === 'health' ? 'is-active' : ''} onClick={() => setInsightTab('health')} type="button">{translate('projectManagement.home.health')}</button><button className={insightTab === 'leads' ? 'is-active' : ''} onClick={() => setInsightTab('leads')} type="button">{translate('projectManagement.home.leads')}</button></div>{summaryQuery.isError ? <div className="pm-home__state">{translate('projectManagement.home.loadingFailed')}</div> : insightTab === 'health' ? <div>{summaryQuery.data?.data.health.map(item => <button className="pm-home__insight-row" key={item.key} onClick={() => setParam('health', searchParams.get('health') === item.key ? undefined : item.key)} type="button"><span>{translate(healthLabels[item.key] || item.key)}</span><strong>{item.count}</strong></button>)}</div> : <div>{summaryQuery.data?.data.leads.map(item => <button className="pm-home__insight-row" key={item.userId || 'unassigned'} onClick={() => setParam('leadUserId', item.userId)} type="button"><span>{item.displayName}</span><strong>{item.count}</strong></button>)}<div className="pm-home__insight-row"><span>{translate('projectManagement.home.unassigned')}</span><strong>{summaryQuery.data?.data.unassignedCount ?? 0}</strong></div></div>}<div className="pm-home__connection" aria-live="polite">{realtime.connectionState === 'reconnecting' ? translate('projectManagement.home.connection.reconnecting') : realtime.connectionState === 'disconnected' ? translate('projectManagement.home.connection.disconnected') : ''}</div></aside>}
    <ModalForm actions={[{ label: translate('projectManagement.home.cancel'), onClick: closeDialog, variant: 'ghost' }, { label: editing ? translate('projectManagement.home.saveUpdate') : translate('projectManagement.home.submitCreate'), loading: saveMutation.isPending, onClick: () => saveMutation.mutate(), variant: 'primary' }]} fields={localizedFormFields} open={dialogOpen} onClose={closeDialog} onValueChange={(name, value) => setForm(current => ({ ...current, [name]: value }))} title={editing ? translate('projectManagement.home.edit') : translate('projectManagement.home.create')} value={form}>{translate('projectManagement.home.formHint')}</ModalForm>
  </div>;
}
