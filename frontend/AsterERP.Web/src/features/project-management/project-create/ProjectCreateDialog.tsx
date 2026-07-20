import { zodResolver } from '@hookform/resolvers/zod';
import { useQuery } from '@tanstack/react-query';
import { useEffect } from 'react';
import { useForm } from 'react-hook-form';
import { z } from 'zod';

import { getProjectManagementMemberCandidates } from '../../../api/project-management/projectManagement.api';
import type { ProjectManagementProjectUpsertRequest } from '../../../api/project-management/projectManagement.types';
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
  useEffect(() => { if (open) form.reset(initialValue); }, [form, initialValue, open]);
  const submit = form.handleSubmit(value => onSubmit({ ...initialValue, ...value }));
  return <PmDialog actions={<><PmButton color="inherit" onClick={onClose}>{translate('projectManagement.home.cancel')}</PmButton><PmButton disabled={pending} loading={pending} onClick={() => void submit()} variant="contained">{editing ? translate('projectManagement.home.saveUpdate') : translate('projectManagement.home.submitCreate')}</PmButton></>} onClose={onClose} open={open} title={editing ? translate('projectManagement.home.edit') : translate('projectManagement.home.create')}>
    <PmStack component="form" onSubmit={event => { event.preventDefault(); void submit(); }} spacing={2} sx={{ pt: 1 }}>
      <PmFormInput autoFocus label={translate('projectManagement.home.field.name')} {...form.register('projectName')} error={Boolean(form.formState.errors.projectName)} helperText={form.formState.errors.projectName ? translate('projectManagement.home.validationRequired') : undefined} />
      <PmBox sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '2fr 1fr 1fr' }, gap: 1.5 }}>
        <PmFormInput label={translate('projectManagement.home.field.code')} {...form.register('projectCode')} error={Boolean(form.formState.errors.projectCode)} />
        <PmFormSelect label={translate('projectManagement.home.status')} value={form.watch('status')} onChange={event => form.setValue('status', String(event.target.value), { shouldDirty: true })}><PmMenuItem value="Planning">{translate('projectManagement.home.status.Planning')}</PmMenuItem><PmMenuItem value="Active">{translate('projectManagement.home.status.Active')}</PmMenuItem><PmMenuItem value="Paused">{translate('projectManagement.home.status.Paused')}</PmMenuItem></PmFormSelect>
        <PmFormSelect label={translate('projectManagement.home.priority')} value={form.watch('priority')} onChange={event => form.setValue('priority', String(event.target.value), { shouldDirty: true })}><PmMenuItem value="Low">{translate('projectManagement.home.priority.Low')}</PmMenuItem><PmMenuItem value="Medium">{translate('projectManagement.home.priority.Medium')}</PmMenuItem><PmMenuItem value="High">{translate('projectManagement.home.priority.High')}</PmMenuItem><PmMenuItem value="Urgent">{translate('projectManagement.home.priority.Urgent')}</PmMenuItem></PmFormSelect>
      </PmBox>
      <PmFormSelect label={translate('projectManagement.home.lead')} value={form.watch('ownerUserId') ?? ''} onChange={event => form.setValue('ownerUserId', String(event.target.value) || undefined, { shouldDirty: true })}>
        <PmMenuItem value="">{translate('projectManagement.home.currentUser')}</PmMenuItem>
        {candidates.map(candidate => <PmMenuItem key={`${candidate.userId}-${candidate.employmentId}`} value={candidate.userId}>{candidate.displayName || candidate.userName}</PmMenuItem>)}
      </PmFormSelect>
      {candidatesQuery.isError && <PmText color="error.main" fontSize=".75rem">{translate('projectManagement.home.leadCandidatesFailed')}</PmText>}
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
