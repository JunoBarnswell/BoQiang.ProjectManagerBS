import { zodResolver } from '@hookform/resolvers/zod';
import { useQuery } from '@tanstack/react-query';
import { useEffect, useMemo, useState } from 'react';
import { useForm } from 'react-hook-form';
import { z } from 'zod';

import { getProjectManagementMemberCandidates } from '../../../api/project-management/projectManagement.api';
import type {
  ProjectManagementProjectInitialMemberUpsertRequest,
  ProjectManagementProjectUpsertRequest,
} from '../../../api/project-management/projectManagement.types';
import { useI18n } from '../../../core/i18n/I18nProvider';
import { projectManagementQueryKeys } from '../../../core/query/projectManagementQueryKeys';
import { PmBox, PmButton, PmDialog, PmFormInput, PmFormSelect, PmMenuItem, PmStack, PmText } from '../../../ui/project-management';
import { useProjectManagementWorkspaceScope } from '../state/projectManagementWorkspaceScope';

import type { ProjectManagementProjectConflict } from './projectManagementProjectConflict';
import { ProjectManagementProjectConflictPanel } from './ProjectManagementProjectConflictPanel';

const schema = z.object({
  projectCode: z.string().trim().min(1),
  projectName: z.string().trim().min(1),
  status: z.string().min(1),
  priority: z.string().min(1),
  ownerUserId: z.string().optional(),
  startDate: z.string().optional(),
  dueDate: z.string().optional(),
  description: z.string().optional(),
}).refine(value => !value.startDate || !value.dueDate || value.dueDate >= value.startDate, { message: 'Target date must not be earlier than start date', path: ['dueDate'] });

type FormValues = z.infer<typeof schema>;
const projectMemberRoles = ['Manager', 'Lead', 'Member', 'Viewer'] as const;

export function ProjectCreateDialog({ open, initialValue, editing, pending, conflict, onClose, onSubmit }: { open: boolean; initialValue: ProjectManagementProjectUpsertRequest; editing: boolean; pending: boolean; conflict?: ProjectManagementProjectConflict | null; onClose: () => void; onSubmit: (value: ProjectManagementProjectUpsertRequest) => void }) {
  const { translate } = useI18n();
  const scope = useProjectManagementWorkspaceScope();
  const form = useForm<FormValues>({ defaultValues: initialValue, resolver: zodResolver(schema) });
  const candidatesQuery = useQuery({
    enabled: open && scope.isAvailable,
    queryKey: projectManagementQueryKeys.memberCandidates(scope, { pageIndex: 1, pageSize: 100 }),
    queryFn: ({ signal }) => getProjectManagementMemberCandidates({ pageIndex: 1, pageSize: 100 }, signal),
  });
  const candidates = candidatesQuery.data?.data.items ?? [];
  const [memberKeyword, setMemberKeyword] = useState('');
  const [initialMembers, setInitialMembers] = useState<ProjectManagementProjectInitialMemberUpsertRequest[]>([]);
  const ownerUserId = form.watch('ownerUserId') ?? '';
  useEffect(() => {
    if (!open) return;
    form.reset(initialValue);
    setInitialMembers(initialValue.initialMembers ?? []);
    setMemberKeyword('');
  }, [form, initialValue, open]);
  const visibleCandidates = useMemo(() => {
    const keyword = memberKeyword.trim().toLocaleLowerCase();
    return candidates.filter((candidate) =>
      candidate.isSelectable &&
      (!keyword || `${candidate.displayName} ${candidate.userName} ${candidate.deptName ?? ''} ${candidate.positionName ?? ''}`.toLocaleLowerCase().includes(keyword)));
  }, [candidates, memberKeyword]);
  const addMember = (userId: string) => {
    const candidate = candidates.find((item) => item.userId === userId);
    if (!candidate || initialMembers.some((item) => item.userId === userId)) return;
    setInitialMembers((current) => [...current, {
      userId,
      employmentId: candidate.employmentId,
      roleCode: 'Member',
    }]);
  };
  const submit = form.handleSubmit(value => onSubmit({
    ...initialValue,
    ...value,
    initialMembers: initialMembers.filter((member) => member.userId !== (value.ownerUserId || initialValue.ownerUserId)),
  }));
  return <PmDialog actions={<><PmButton color="inherit" onClick={onClose}>{translate('projectManagement.home.cancel')}</PmButton><PmButton disabled={pending} loading={pending} onClick={() => void submit()} variant="contained">{editing ? translate('projectManagement.home.saveUpdate') : translate('projectManagement.home.submitCreate')}</PmButton></>} onClose={onClose} open={open} title={editing ? translate('projectManagement.home.edit') : translate('projectManagement.home.create')}>
    <PmStack component="form" onSubmit={event => { event.preventDefault(); void submit(); }} spacing={2} sx={{ pt: 1 }}>
      <PmFormInput autoFocus label={translate('projectManagement.home.field.name')} {...form.register('projectName')} error={Boolean(form.formState.errors.projectName)} helperText={form.formState.errors.projectName ? translate('projectManagement.home.validationRequired') : undefined} />
      <PmBox sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '2fr 1fr 1fr' }, gap: 1.5 }}>
        <PmFormInput label={translate('projectManagement.home.field.code')} {...form.register('projectCode')} error={Boolean(form.formState.errors.projectCode)} />
        <PmFormSelect label={translate('projectManagement.home.status')} value={form.watch('status')} onChange={event => form.setValue('status', String(event.target.value), { shouldDirty: true })}>
          <PmMenuItem value="Planning">{translate('projectManagement.home.status.Planning')}</PmMenuItem>
          <PmMenuItem value="Active">{translate('projectManagement.home.status.Active')}</PmMenuItem>
          <PmMenuItem value="Paused">{translate('projectManagement.home.status.Paused')}</PmMenuItem>
          {editing ? <>
            <PmMenuItem value="Completed">{translate('projectManagement.home.status.Completed')}</PmMenuItem>
            <PmMenuItem value="Canceled">{translate('projectManagement.home.status.Canceled')}</PmMenuItem>
            <PmMenuItem value="Archived">{translate('projectManagement.home.status.Archived')}</PmMenuItem>
          </> : null}
        </PmFormSelect>
        <PmFormSelect label={translate('projectManagement.home.priority')} value={form.watch('priority')} onChange={event => form.setValue('priority', String(event.target.value), { shouldDirty: true })}><PmMenuItem value="Low">{translate('projectManagement.home.priority.Low')}</PmMenuItem><PmMenuItem value="Medium">{translate('projectManagement.home.priority.Medium')}</PmMenuItem><PmMenuItem value="High">{translate('projectManagement.home.priority.High')}</PmMenuItem><PmMenuItem value="Urgent">{translate('projectManagement.home.priority.Urgent')}</PmMenuItem></PmFormSelect>
      </PmBox>
      <PmFormSelect label={translate('projectManagement.home.lead')} value={form.watch('ownerUserId') ?? ''} onChange={event => form.setValue('ownerUserId', String(event.target.value) || undefined, { shouldDirty: true })}>
        <PmMenuItem value="">{translate('projectManagement.home.currentUser')}</PmMenuItem>
        {candidates.map(candidate => <PmMenuItem key={`${candidate.userId}-${candidate.employmentId}`} value={candidate.userId}>{candidate.displayName || candidate.userName}</PmMenuItem>)}
      </PmFormSelect>
      {candidatesQuery.isError && <PmText color="error.main" fontSize=".75rem">{translate('projectManagement.home.leadCandidatesFailed')}</PmText>}
      {!editing ? <PmBox sx={{ border: 1, borderColor: 'divider', borderRadius: 1.5, p: 1.5 }}>
        <PmText fontWeight={700}>{translate('projectManagement.home.members.title')}</PmText>
        <PmText color="text.secondary" fontSize=".75rem" sx={{ display: 'block', mt: .25 }}>{translate('projectManagement.home.members.hint')}</PmText>
        <PmBox sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: 'minmax(0, 1fr) 180px' }, gap: 1, mt: 1.25 }}>
          <PmFormInput label={translate('projectManagement.home.members.search')} onChange={(event) => setMemberKeyword(event.target.value)} value={memberKeyword} />
          <PmFormSelect label={translate('projectManagement.home.members.add')} value="" onChange={(event) => addMember(String(event.target.value))}>
            <PmMenuItem value="">{translate('projectManagement.home.members.add')}</PmMenuItem>
            {visibleCandidates
              .filter((candidate) => candidate.userId !== ownerUserId && !initialMembers.some((member) => member.userId === candidate.userId))
              .map((candidate) => <PmMenuItem key={candidate.userId} value={candidate.userId}>{formatCandidate(candidate)}</PmMenuItem>)}
          </PmFormSelect>
        </PmBox>
        {initialMembers.length === 0 ? <PmText color="text.secondary" fontSize=".75rem" sx={{ display: 'block', mt: 1 }}>{translate('projectManagement.home.members.empty')}</PmText> : null}
        {initialMembers.map((member) => {
          const candidate = candidates.find((item) => item.userId === member.userId);
          return (
            <PmBox key={member.userId} sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr auto', sm: 'minmax(180px, 1fr) 160px auto' }, alignItems: 'center', gap: 1, mt: 1 }}>
              <PmText fontSize=".8125rem">{candidate ? formatCandidate(candidate) : member.userId}</PmText>
              <PmFormSelect label={translate('projectManagement.home.members.role')} value={member.roleCode} onChange={(event) => {
                const roleCode = event.target.value as ProjectManagementProjectInitialMemberUpsertRequest['roleCode'];
                setInitialMembers((current) => current.map((item) => item.userId === member.userId ? { ...item, roleCode, scopeRootTaskId: undefined } : item));
              }}>
                {projectMemberRoles.map((roleCode) => <PmMenuItem key={roleCode} value={roleCode}>{roleCode}</PmMenuItem>)}
              </PmFormSelect>
              <PmButton color="inherit" onClick={() => setInitialMembers((current) => current.filter((item) => item.userId !== member.userId))}>{translate('projectManagement.home.members.remove')}</PmButton>
              {member.roleCode === 'Lead' ? <PmText color="text.secondary" fontSize=".7rem" sx={{ gridColumn: { sm: '1 / -1' } }}>{translate('projectManagement.home.members.leadScopeAfterCreate')}</PmText> : null}
            </PmBox>
          );
        })}
      </PmBox> : null}
      <PmBox sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr' }, gap: 1.5 }}>
        <PmFormInput label={translate('projectManagement.home.field.startDate')} type="date" {...form.register('startDate')} />
        <PmBox><PmFormInput label={translate('projectManagement.home.field.targetDate')} type="date" {...form.register('dueDate')} error={Boolean(form.formState.errors.dueDate)} />{form.formState.errors.dueDate?.message && <PmText color="error.main" fontSize=".75rem">{form.formState.errors.dueDate.message}</PmText>}</PmBox>
      </PmBox>
      <PmFormInput multiline minRows={5} label={translate('projectManagement.home.field.description')} {...form.register('description')} />
      <PmText color="text.secondary" fontSize=".75rem">{translate('projectManagement.home.formHint')}</PmText>
      {conflict && <ProjectManagementProjectConflictPanel conflict={conflict} translate={translate} />}
    </PmStack>
  </PmDialog>;
}

function formatCandidate(candidate: { displayName: string; userName: string; deptName?: string; positionName?: string }) {
  const profile = [candidate.deptName, candidate.positionName].filter(Boolean).join(' · ');
  return profile ? `${candidate.displayName || candidate.userName} · ${profile}` : candidate.displayName || candidate.userName;
}
