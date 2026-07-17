import BpmnViewer from 'bpmn-js/lib/Viewer';
import { type ReactNode, useEffect, useMemo, useRef } from 'react';
import { useNavigate, useParams } from 'react-router-dom';

import 'bpmn-js/dist/assets/diagram-js.css';
import 'bpmn-js/dist/assets/bpmn-font/css/bpmn.css';

import type {
  WorkflowNotificationTaskDto,
  WorkflowSubmittedFormFieldDto,
  WorkflowTaskListItemDto,
  WorkflowTimelineItemDto
} from '../../api/workflow/workflows.api';
import { getWorkflowInstance, getWorkflowInstanceDiagram } from '../../api/workflow/workflows.api';
import { useI18n } from '../../core/i18n/I18nProvider';
import { useApiQuery } from '../../core/query/useApiQuery';
import { CrudPage } from '../../shared/components/crud-page/CrudPage';
import { useMessage } from '../../shared/feedback/useMessage';
import { AppIcon } from '../../shared/icons/AppIcon';
import { DataTable } from '../../shared/table/DataTable';
import type { DataTableColumn } from '../../shared/table/tableTypes';
import { getErrorMessage } from '../../shared/utils/errorMessage';

import './workflow-bpmn.css';

export function WorkflowInstancePage() {
  const { processInstanceId = '' } = useParams();
  const navigate = useNavigate();
  const { translate } = useI18n();
  const message = useMessage();
  const viewerRef = useRef<BpmnViewer | null>(null);
  const canvasRef = useRef<HTMLDivElement | null>(null);

  const detailQuery = useApiQuery({
    enabled: Boolean(processInstanceId),
    queryFn: ({ signal }) => getWorkflowInstance(processInstanceId, signal),
    queryKey: ['workflows', 'instances', processInstanceId]
  });
  const diagramQuery = useApiQuery({
    enabled: Boolean(processInstanceId),
    queryFn: ({ signal }) => getWorkflowInstanceDiagram(processInstanceId, signal),
    queryKey: ['workflows', 'instances', processInstanceId, 'diagram']
  });

  useEffect(() => {
    if (!canvasRef.current) return;
    const viewer = new BpmnViewer({ container: canvasRef.current });
    viewerRef.current = viewer;
    return () => {
      viewer.destroy();
      viewerRef.current = null;
    };
  }, []);

  useEffect(() => {
    const viewer = viewerRef.current;
    const diagram = diagramQuery.data?.data;
    if (!viewer || !diagram?.bpmnXml) return;
    void viewer.importXML(diagram.bpmnXml).then(() => {
      const canvas = viewer.get<{ addMarker(id: string, marker: string): void; zoom(value: string): void }>('canvas');
      diagram.completedActivityIds.forEach((id) => canvas.addMarker(id, 'workflow-marker-completed'));
      diagram.activeActivityIds.forEach((id) => canvas.addMarker(id, 'workflow-marker-active'));
      canvas.zoom('fit-viewport');
    }).catch((error) => message.error(getErrorMessage(error, translate('page.workflowInstance.error.diagramLoadFailed'))));
  }, [diagramQuery.data?.data, message, translate]);

  const detail = detailQuery.data?.data;
  const variables = useMemo(() => Object.entries(detail?.variables ?? {}), [detail?.variables]);
  const submittedFields = detail?.submittedForm?.fields ?? [];
  const taskColumns = useMemo<DataTableColumn<WorkflowTaskListItemDto>[]>(() => [
    { key: 'name', title: translate('page.workflowInstance.column.runningTask'), width: '220px', render: (row) => <><div className="font-medium text-gray-900">{row.name ?? row.id}</div><div className="text-xs text-gray-500">{row.taskDefinitionKey ?? '-'}</div></> },
    { key: 'assignee', title: translate('page.workflowInstance.column.assignee'), width: '190px', render: (row) => row.assigneeName ?? row.assignee ?? (row.candidateNames.join(', ') || '-') },
    { key: 'createdAt', title: translate('page.workflowInstance.column.createdAt'), width: '180px', render: (row) => formatDateTime(row.createdAt) },
    { key: 'dueAt', title: translate('page.workflowInstance.column.dueAt'), width: '180px', render: (row) => formatDateTime(row.dueAt) }
  ], [translate]);

  return (
    <CrudPage
      title={translate('page.workflowInstance.title')}
      actions={<button className="bg-white border border-gray-300 text-gray-700 px-3 py-1.5 rounded text-sm hover:bg-gray-50" type="button" onClick={() => navigate(-1)}><AppIcon name="arrow-left" /></button>}
    >
      <div className="flex h-full min-h-0 flex-col gap-3 overflow-auto pb-3 pr-1">
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-5">
          <SummaryCard label={translate('page.workflowInstance.summary.status')} value={detail?.status ?? '-'} />
          <SummaryCard label={translate('page.workflowInstance.summary.businessType')} value={detail?.businessType ?? '-'} />
          <SummaryCard label={translate('page.workflowInstance.summary.businessKey')} value={detail?.businessKey ?? '-'} />
          <SummaryCard label={translate('page.workflowInstance.summary.currentTask')} value={`${detail?.runtimeTasks.length ?? 0}`} />
          <SummaryCard label={translate('page.workflowInstance.summary.notifications')} value={`${detail?.notifications.length ?? 0}`} />
        </div>

        <div className="grid grid-cols-1 gap-3 lg:grid-cols-[minmax(0,1fr)_360px]">
          <main className="grid min-w-0 gap-3">
            <SectionCard className="overflow-hidden" flush title={translate('page.workflowInstance.section.diagram')}>
              <div className="h-[340px] min-h-[300px] overflow-hidden">
                <div ref={canvasRef} className="workflow-bpmn-viewer h-full min-h-full" />
              </div>
            </SectionCard>

            <SubmittedFormPanel fields={submittedFields} />

            <SectionCard title={translate('page.workflowInstance.section.timeline')}>
              <div className="workflow-timeline">
                {(detail?.timeline ?? []).map((item) => <TimelineItem key={item.id} item={item} />)}
                {!detail?.timeline?.length ? <EmptyState text={translate('page.workflowInstance.empty.timeline')} /> : null}
              </div>
            </SectionCard>
          </main>

          <aside className="grid min-w-0 content-start gap-3">
            <SectionCard title={translate('page.workflowInstance.section.info')}>
              <div className="grid gap-2 text-sm">
                <InfoRow label={translate('page.workflowInstance.info.processKey')} value={detail?.processDefinitionKey} />
                <InfoRow label={translate('page.workflowInstance.info.instanceId')} value={detail?.processInstanceId} />
                <InfoRow label={translate('page.workflowInstance.info.startedBy')} value={detail?.startedByName || detail?.startedBy} />
                <InfoRow label={translate('page.workflowInstance.info.startedAt')} value={formatDateTime(detail?.startedAt)} />
                <InfoRow label={translate('page.workflowInstance.info.finishedAt')} value={formatDateTime(detail?.finishedAt)} />
              </div>
            </SectionCard>

            <SectionCard className="min-h-[260px]" title={translate('page.workflowInstance.section.currentTask')}>
              <div className="h-64 min-h-0">
                <DataTable
                  columns={taskColumns}
                  emptyText={translate('page.workflowInstance.empty.tasks')}
                  fitScreen
                  loading={detailQuery.isLoading}
                  rowKey={(row) => row.id}
                  rows={detail?.runtimeTasks ?? []}
                />
              </div>
            </SectionCard>

            <SectionCard title={translate('page.workflowInstance.section.variables')}>
              <div className="grid gap-2 text-sm">
                {variables.length === 0 ? <div className="text-gray-500">{translate('page.workflowInstance.empty.variables')}</div> : variables.map(([key, value]) => (
                  <div key={key} className="grid grid-cols-[120px_minmax(0,1fr)] gap-2 border-b border-gray-100 pb-2">
                    <span className="text-gray-500">{key}</span>
                    <span className="text-gray-900 break-all">{formatValue(value)}</span>
                  </div>
                ))}
              </div>
            </SectionCard>

            <SectionCard title={translate('page.workflowInstance.section.notifications')}>
              <div className="workflow-notification-list">
                {(detail?.notifications ?? []).map((item) => <NotificationItem key={item.id} item={item} />)}
                {!detail?.notifications?.length ? <EmptyState text={translate('page.workflowInstance.empty.notifications')} /> : null}
              </div>
            </SectionCard>
          </aside>
        </div>
      </div>
    </CrudPage>
  );
}

function SummaryCard({ label, value }: { label: string; value: string }) {
  return (
    <div className="min-w-0 rounded-lg border border-gray-200 bg-white p-3 shadow-sm">
      <div className="text-xs text-gray-500">{label}</div>
      <strong className="mt-2 block text-lg font-semibold leading-6 text-gray-900" style={{ overflowWrap: 'anywhere' }} title={value}>
        {value}
      </strong>
    </div>
  );
}

function SectionCard({
  children,
  className = '',
  flush = false,
  title
}: {
  children: ReactNode;
  className?: string;
  flush?: boolean;
  title: string;
}) {
  return (
    <section className={['min-w-0 rounded-lg border border-gray-200 bg-white shadow-sm', flush ? 'p-0' : 'p-4', className].filter(Boolean).join(' ')}>
      {flush ? (
        <div className="border-b border-gray-100 px-4 py-3">
          <div className="workflow-panel-title">{title}</div>
        </div>
      ) : (
        <div className="workflow-panel-title mb-3">{title}</div>
      )}
      {children}
    </section>
  );
}

function SubmittedFormPanel({ fields }: { fields: WorkflowSubmittedFormFieldDto[] }) {
  const { translate } = useI18n();
  return (
    <SectionCard title={translate('page.workflowInstance.section.submittedForm')}>
      {fields.length === 0 ? (
        <EmptyState text={translate('page.workflowInstance.empty.submittedFields')} />
      ) : (
        <div className="grid grid-cols-1 gap-2 md:grid-cols-2">
          {fields.map((field) => (
            <div key={field.field} className="min-w-0 rounded-md border border-gray-100 bg-gray-50 px-3 py-2">
              <div className="min-w-0">
                <div className="font-medium text-gray-800 break-all">{field.label || field.field}</div>
                <div className="text-xs text-gray-400 break-all">{field.field}</div>
              </div>
              <div className="mt-2 whitespace-pre-wrap break-all text-gray-900">
                {formatValue(field.value)}
              </div>
            </div>
          ))}
        </div>
      )}
    </SectionCard>
  );
}

function InfoRow({ label, value }: { label: string; value?: string | null }) {
  return (
    <div className="grid grid-cols-[84px_minmax(0,1fr)] gap-2">
      <span className="text-gray-500">{label}</span>
      <span className="text-gray-900 break-all">{value || '-'}</span>
    </div>
  );
}

function TimelineItem({ item }: { item: WorkflowTimelineItemDto }) {
  return (
    <div className="workflow-timeline-item">
      <div className="workflow-timeline-time">{formatDateTime(item.createdAt)}</div>
      <div className="workflow-timeline-body">
        <strong>{item.title}</strong>
        <span>{item.kind} / {item.userName ?? item.userId ?? item.action ?? '-'}</span>
        {item.comment ? <p>{item.comment}</p> : null}
      </div>
    </div>
  );
}

function NotificationItem({ item }: { item: WorkflowNotificationTaskDto }) {
  const { translate } = useI18n();
  return (
    <div className="workflow-notification-item">
      <div>
        <strong>{item.subject ?? item.templateCode ?? translate('page.workflowInstance.notification.defaultTitle')}</strong>
        <span>{item.channelCode} / {item.status} / {formatDateTime(item.sentAt ?? item.dueAt ?? item.createdTime)}</span>
      </div>
      <p>{item.content}</p>
      {item.lastError ? <em>{item.lastError}</em> : null}
    </div>
  );
}

function EmptyState({ text }: { text: string }) {
  return <div className="rounded-md border border-dashed border-gray-200 bg-gray-50 px-3 py-5 text-center text-sm text-gray-500">{text}</div>;
}

function formatDateTime(value?: string | null) {
  if (!value) {
    return '-';
  }

  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
}

function formatValue(value: unknown) {
  if (value === null || value === undefined) {
    return '-';
  }

  return typeof value === 'object' ? JSON.stringify(value, null, 2) : String(value);
}
