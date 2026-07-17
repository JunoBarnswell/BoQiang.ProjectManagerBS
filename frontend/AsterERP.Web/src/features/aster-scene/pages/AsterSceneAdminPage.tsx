import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { MessageSquareText, ShieldCheck, ShieldX } from 'lucide-react';
import { useState } from 'react';

import { usePermission } from '@/core/auth/usePermission';
import { useI18n } from '@/core/i18n/I18nProvider';
import { PermissionButton } from '@/shared/auth/PermissionButton';

import { asterSceneApi } from '../api/asterScene.api';
import { AsterSceneLayout } from '../components/AsterSceneLayout';
import { AsterSceneState } from '../components/AsterSceneState';
import { AsterSceneUsageLedgerPanel } from '../components/AsterSceneUsageLedgerPanel';
import { createClientMutationId } from '../core/scene-document/documentKernel';
import { asterScenePermissions } from '../model/permissions';
import type { AsterSceneSupportTicket } from '../model/types';

type AdminSection = 'moderation' | 'appeals' | 'support' | 'operations';

export function AsterSceneAdminPage() {
  const { translate: t } = useI18n();
  const queryClient = useQueryClient();
  const [section, setSection] = useState<AdminSection>('moderation');
  const [selectedTicketId, setSelectedTicketId] = useState<string | null>(null);
  const canViewAppeals = usePermission(asterScenePermissions.appealManage).hasPermission;
  const canViewSupport = usePermission(asterScenePermissions.supportAdminView).hasPermission;
  const canViewJobs = usePermission(asterScenePermissions.jobView).hasPermission;
  const canViewUsage = usePermission(asterScenePermissions.usageView).hasPermission;

  const casesQuery = useQuery({
    queryFn: ({ signal }) => asterSceneApi.admin.moderationCases({ pageSize: 100 }, signal),
    queryKey: ['asterscene', 'moderation']
  });
  const appealsQuery = useQuery({
    enabled: canViewAppeals,
    queryFn: ({ signal }) => asterSceneApi.admin.appeals({ pageSize: 100 }, signal),
    queryKey: ['asterscene', 'appeals']
  });
  const supportQuery = useQuery({
    enabled: canViewSupport,
    queryFn: ({ signal }) => asterSceneApi.admin.supportTickets({ pageSize: 100 }, signal),
    queryKey: ['asterscene', 'support-admin']
  });
  const supportDetailQuery = useQuery({
    enabled: Boolean(selectedTicketId) && canViewSupport,
    queryFn: ({ signal }) => asterSceneApi.admin.supportTicket(selectedTicketId ?? '', signal),
    queryKey: ['asterscene', 'support-admin-detail', selectedTicketId]
  });
  const jobsQuery = useQuery({
    enabled: canViewJobs,
    queryFn: ({ signal }) => asterSceneApi.jobs.list({ pageSize: 100 }, signal),
    queryKey: ['asterscene', 'jobs']
  });
  const usageQuery = useQuery({
    enabled: canViewUsage,
    queryFn: ({ signal }) => asterSceneApi.usage.summary(signal),
    queryKey: ['asterscene', 'usage-summary']
  });

  const invalidateAdmin = () => {
    void queryClient.invalidateQueries({ queryKey: ['asterscene', 'moderation'] });
    void queryClient.invalidateQueries({ queryKey: ['asterscene', 'appeals'] });
    void queryClient.invalidateQueries({ queryKey: ['asterscene', 'support-admin'] });
    void queryClient.invalidateQueries({ queryKey: ['asterscene', 'support-admin-detail', selectedTicketId] });
    void queryClient.invalidateQueries({ queryKey: ['asterscene', 'usage-summary'] });
  };
  const decideMutation = useMutation({
    mutationFn: ({ caseId, decision }: { caseId: string; decision: string }) =>
      asterSceneApi.admin.decide(caseId, {
        clientMutationId: createClientMutationId('moderation-decision'),
        decision
      }),
    onSuccess: invalidateAdmin
  });
  const decideAppealMutation = useMutation({
    mutationFn: ({ appealId, decision }: { appealId: string; decision: 'Approve' | 'Reject' }) =>
      asterSceneApi.admin.decideAppeal(appealId, {
        clientMutationId: createClientMutationId('appeal-decision'),
        decision
      }),
    onSuccess: invalidateAdmin
  });
  const addCommentMutation = useMutation({
    mutationFn: ({ ticketId, message }: { ticketId: string; message: string }) =>
      asterSceneApi.admin.addSupportComment(ticketId, {
        clientMutationId: createClientMutationId('support-admin-comment'),
        message
      }),
    onSuccess: (_, variables) => {
      void queryClient.invalidateQueries({ queryKey: ['asterscene', 'support-admin-detail', variables.ticketId] });
      void queryClient.invalidateQueries({ queryKey: ['asterscene', 'support-admin'] });
    }
  });
  const changeStatusMutation = useMutation({
    mutationFn: ({ ticketId, status }: { ticketId: string; status: 'Open' | 'Closed' }) =>
      asterSceneApi.admin.changeSupportStatus(ticketId, {
        clientMutationId: createClientMutationId('support-admin-status'),
        note: t('asterscene.admin.support.statusNote'),
        status
      }),
    onSuccess: (_, variables) => {
      void queryClient.invalidateQueries({ queryKey: ['asterscene', 'support-admin-detail', variables.ticketId] });
      void queryClient.invalidateQueries({ queryKey: ['asterscene', 'support-admin'] });
    }
  });

  const cases = casesQuery.data?.data.items ?? [];
  const appeals = appealsQuery.data?.data.items ?? [];
  const tickets = supportQuery.data?.data.items ?? [];
  const selectedTicket = supportDetailQuery.data?.data;

  return (
    <AsterSceneLayout eyebrow={t('asterscene.admin.eyebrow')} title={t('asterscene.admin.title')}>
      <nav className="as-band as-tabs" aria-label={t('asterscene.admin.sections')}>
        <AdminTab active={section === 'moderation'} label={t('asterscene.admin.cases')} onClick={() => setSection('moderation')} />
        {canViewAppeals ? <AdminTab active={section === 'appeals'} label={t('asterscene.admin.appeals')} onClick={() => setSection('appeals')} /> : null}
        {canViewSupport ? <AdminTab active={section === 'support'} label={t('asterscene.admin.support')} onClick={() => setSection('support')} /> : null}
        <AdminTab active={section === 'operations'} label={t('asterscene.admin.operations')} onClick={() => setSection('operations')} />
      </nav>

      {section === 'moderation' ? (
        <ModerationSection
          cases={cases}
          isError={casesQuery.isError}
          isLoading={casesQuery.isLoading}
          onDecide={(caseId, decision) => decideMutation.mutate({ caseId, decision })}
          t={t}
        />
      ) : null}
      {section === 'appeals' ? (
        <AppealsSection
          appeals={appeals}
          isError={appealsQuery.isError}
          isLoading={appealsQuery.isLoading}
          onDecide={(appealId, decision) => decideAppealMutation.mutate({ appealId, decision })}
          t={t}
        />
      ) : null}
      {section === 'support' ? (
        <SupportSection
          addComment={(message) => selectedTicketId && addCommentMutation.mutate({ message, ticketId: selectedTicketId })}
          changeStatus={(status) => selectedTicketId && changeStatusMutation.mutate({ status, ticketId: selectedTicketId })}
          isDetailLoading={supportDetailQuery.isLoading}
          onSelect={setSelectedTicketId}
          selectedTicket={selectedTicket}
          tickets={tickets}
          t={t}
        />
      ) : null}
      {section === 'operations' ? (
        <>
          <OperationsSection jobs={jobsQuery.data?.data.items ?? []} showJobs={canViewJobs} showUsage={canViewUsage} t={t} usage={usageQuery.data?.data} />
          {canViewUsage ? <AsterSceneUsageLedgerPanel /> : null}
        </>
      ) : null}
    </AsterSceneLayout>
  );
}

function AdminTab({ active, label, onClick }: { active: boolean; label: string; onClick: () => void }) {
  return <button className={active ? 'as-button as-button--primary' : 'as-button'} onClick={onClick} type="button">{label}</button>;
}

function ModerationSection({ cases, isError, isLoading, onDecide, t }: { cases: Array<{ decision?: string | null; id: string; projectId?: string | null; reasonCode: string; status: string; workId?: string | null }>; isError: boolean; isLoading: boolean; onDecide: (caseId: string, decision: string) => void; t: (key: string) => string }) {
  return (
    <section className="as-band">
      {isLoading ? <AsterSceneState title={t('asterscene.admin.loading')} /> : null}
      {isError ? <AsterSceneState title={t('asterscene.admin.failed')} /> : null}
      <div className="as-panel">
        <h2>{t('asterscene.admin.cases')}</h2>
        {cases.length === 0 ? <AsterSceneState title={t('asterscene.admin.noCases')} /> : null}
        <div className="as-table">
          {cases.map((item) => (
            <div className="as-table__row" key={item.id}>
              <div><strong>{item.reasonCode}</strong><span>{item.workId || item.projectId}</span></div>
              <span>{item.status}</span><span>{item.decision || t('asterscene.admin.pending')}</span>
              {item.status === 'Open' ? <div className="as-actions">
                <PermissionButton className="as-icon-button" code={asterScenePermissions.moderationManage} iconStart={false} onClick={() => onDecide(item.id, 'Allow')} title={t('asterscene.admin.allow')} type="button"><ShieldCheck size={16} /></PermissionButton>
                <PermissionButton className="as-icon-button" code={asterScenePermissions.moderationManage} iconStart={false} onClick={() => onDecide(item.id, 'Remove')} title={t('asterscene.admin.remove')} type="button"><ShieldX size={16} /></PermissionButton>
              </div> : null}
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}

function AppealsSection({ appeals, isError, isLoading, onDecide, t }: { appeals: Array<{ caseId: string; createdTime: string; id: string; reason: string; status: string }>; isError: boolean; isLoading: boolean; onDecide: (appealId: string, decision: 'Approve' | 'Reject') => void; t: (key: string) => string }) {
  return (
    <section className="as-band"><div className="as-panel">
      <h2>{t('asterscene.admin.appeals')}</h2>
      {isLoading ? <AsterSceneState title={t('asterscene.admin.appealsLoading')} /> : null}
      {isError ? <AsterSceneState title={t('asterscene.admin.appealsFailed')} /> : null}
      {appeals.length === 0 ? <AsterSceneState title={t('asterscene.admin.noAppeals')} /> : null}
      <div className="as-table">{appeals.map((appeal) => <div className="as-table__row" key={appeal.id}>
        <div><strong>{appeal.reason}</strong><span>{appeal.caseId} · {new Date(appeal.createdTime).toLocaleString()}</span></div><span>{appeal.status}</span>
        {appeal.status === 'Submitted' ? <div className="as-actions"><PermissionButton className="as-button" code={asterScenePermissions.appealManage} iconStart={false} onClick={() => onDecide(appeal.id, 'Approve')} type="button">{t('asterscene.admin.approveAppeal')}</PermissionButton><PermissionButton className="as-button" code={asterScenePermissions.appealManage} iconStart={false} onClick={() => onDecide(appeal.id, 'Reject')} type="button">{t('asterscene.admin.rejectAppeal')}</PermissionButton></div> : null}
      </div>)}</div>
    </div></section>
  );
}

function SupportSection({ addComment, changeStatus, isDetailLoading, onSelect, selectedTicket, tickets, t }: { addComment: (message: string) => void; changeStatus: (status: 'Open' | 'Closed') => void; isDetailLoading: boolean; onSelect: (ticketId: string) => void; selectedTicket?: { comments: Array<{ commentType: string; createdTime: string; message: string; statusAfter?: string | null }>; id: string; severity: string; status: string; title: string } | null; tickets: AsterSceneSupportTicket[]; t: (key: string) => string }) {
  const [message, setMessage] = useState('');
  return <section className="as-band as-band--split"><div className="as-panel"><h2>{t('asterscene.admin.support')}</h2>{tickets.length === 0 ? <AsterSceneState title={t('asterscene.admin.noSupport')} /> : null}<div className="as-table">{tickets.map((ticket) => <button className="as-table__row" key={ticket.id} onClick={() => onSelect(ticket.id)} type="button"><div><strong>{ticket.title}</strong><span>{ticket.projectId}</span></div><span>{ticket.severity}</span><span>{ticket.status}</span></button>)}</div></div><div className="as-panel"><h2>{t('asterscene.admin.supportDetail')}</h2>{isDetailLoading ? <AsterSceneState title={t('asterscene.admin.supportLoading')} /> : null}{selectedTicket ? <><p><strong>{selectedTicket.title}</strong> · {selectedTicket.status}</p><div className="as-table">{selectedTicket.comments.map((comment) => <div className="as-table__row" key={`${comment.createdTime}-${comment.message}`}><div><strong>{comment.commentType}</strong><span>{new Date(comment.createdTime).toLocaleString()}</span></div><span>{comment.message}</span><span>{comment.statusAfter ?? ''}</span></div>)}</div><label>{t('asterscene.admin.supportComment')}<textarea value={message} onChange={(event) => setMessage(event.target.value)} /></label><div className="as-actions"><PermissionButton className="as-button" code={asterScenePermissions.supportAdminManage} disabled={!message.trim()} iconStart={false} onClick={() => { addComment(message.trim()); setMessage(''); }} type="button"><MessageSquareText size={16} />{t('asterscene.admin.addComment')}</PermissionButton><PermissionButton className="as-button" code={asterScenePermissions.supportAdminManage} iconStart={false} onClick={() => changeStatus(selectedTicket.status === 'Closed' ? 'Open' : 'Closed')} type="button">{selectedTicket.status === 'Closed' ? t('asterscene.admin.reopen') : t('asterscene.admin.close')}</PermissionButton></div></> : <AsterSceneState title={t('asterscene.admin.selectSupport')} />}</div></section>;
}

function OperationsSection({ jobs, showJobs, showUsage, t, usage }: { jobs: Array<{ createdTime: string; errorCode?: string | null; id: string; jobCode: string; jobType: string; progressPercent: number; status: string }>; showJobs: boolean; showUsage: boolean; t: (key: string) => string; usage?: { aiCreditsRemaining: number; planCode: string; publishedWorksLimit: number; publishedWorksUsed: number; storageGbLimit: number; storageGbUsed: number } }) {
  return <section className="as-band as-band--split">{showUsage ? <div className="as-panel"><h2>{t('asterscene.admin.usage')}</h2>{usage ? <div className="as-metrics"><span>{t('asterscene.pricing.plan')} {usage.planCode}</span><span>{t('asterscene.pricing.storage')} {usage.storageGbUsed.toFixed(2)} / {usage.storageGbLimit} GB</span><span>{t('asterscene.pricing.aiCredits')} {usage.aiCreditsRemaining}</span><span>{t('asterscene.pricing.published')} {usage.publishedWorksUsed} / {usage.publishedWorksLimit}</span></div> : <AsterSceneState title={t('asterscene.admin.operationsUnavailable')} />}</div> : null}{showJobs ? <div className="as-panel"><h2>{t('asterscene.admin.jobs')}</h2>{jobs.length === 0 ? <AsterSceneState title={t('asterscene.admin.noJobs')} /> : <div className="as-table">{jobs.map((job) => <div className="as-table__row" key={job.id}><div><strong>{job.jobCode}</strong><span>{job.jobType} · {new Date(job.createdTime).toLocaleString()}</span></div><span>{job.status}</span><span>{job.progressPercent}%{job.errorCode ? ` · ${job.errorCode}` : ''}</span></div>)}</div>}</div> : null}</section>;
}
