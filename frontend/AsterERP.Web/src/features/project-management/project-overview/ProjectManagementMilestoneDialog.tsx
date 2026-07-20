import { zodResolver } from '@hookform/resolvers/zod';
import { useEffect } from 'react';
import { useForm } from 'react-hook-form';
import { z } from 'zod';

import type { ProjectManagementMilestone, ProjectManagementMilestoneUpsertRequest } from '../../../api/project-management/projectManagement.types';
import { useI18n } from '../../../core/i18n/I18nProvider';
import { PmBox, PmButton, PmDialog, PmFormInput, PmFormSelect, PmMenuItem, PmStack, PmText } from '../../../ui/project-management';

const schema = z.object({
  milestoneName: z.string().trim().min(1),
  description: z.string().optional(),
  status: z.string().min(1),
  startDate: z.string().optional(),
  dueDate: z.string().optional(),
  progressPercent: z.number().min(0).max(100),
  sortOrder: z.number().int().min(0),
}).refine(value => !value.startDate || !value.dueDate || value.dueDate >= value.startDate, {
  message: 'Target date must not be earlier than start date',
  path: ['dueDate'],
});

type FormValues = z.infer<typeof schema>;

const emptyValue: FormValues = {
  milestoneName: '',
  description: '',
  status: 'Planning',
  startDate: '',
  dueDate: '',
  progressPercent: 0,
  sortOrder: 0,
};

export function ProjectManagementMilestoneDialog({
  open,
  milestone,
  pending,
  onClose,
  onSubmit,
}: {
  open: boolean;
  milestone?: ProjectManagementMilestone | null;
  pending: boolean;
  onClose: () => void;
  onSubmit: (value: ProjectManagementMilestoneUpsertRequest) => void;
}) {
  const { translate } = useI18n();
  const form = useForm<FormValues>({ defaultValues: emptyValue, resolver: zodResolver(schema) });
  const editing = Boolean(milestone);

  useEffect(() => {
    if (!open) return;
    form.reset(milestone ? {
      milestoneName: milestone.milestoneName,
      description: milestone.description ?? '',
      status: milestone.status || 'Planning',
      startDate: milestone.startDate ?? '',
      dueDate: milestone.dueDate ?? '',
      progressPercent: milestone.progressPercent,
      sortOrder: milestone.sortOrder,
    } : emptyValue);
  }, [form, milestone, open]);

  const submit = form.handleSubmit(value => onSubmit({
    ...value,
    versionNo: milestone?.versionNo,
    progressPercent: value.progressPercent,
    sortOrder: value.sortOrder,
  }));

  return <PmDialog
    actions={<><PmButton color="inherit" disabled={pending} onClick={onClose}>{translate('projectManagement.home.cancel')}</PmButton><PmButton disabled={pending} loading={pending} onClick={() => void submit()} variant="contained">{editing ? translate('projectManagement.home.saveUpdate') : translate('projectManagement.home.milestoneCreate')}</PmButton></>}
    onClose={onClose}
    open={open}
    title={editing ? translate('projectManagement.home.milestoneEdit') : translate('projectManagement.home.milestoneCreate')}
  >
    <PmStack component="form" onSubmit={event => { event.preventDefault(); void submit(); }} spacing={2} sx={{ pt: 1 }}>
      <PmFormInput autoFocus label={translate('projectManagement.home.milestoneName')} {...form.register('milestoneName')} error={Boolean(form.formState.errors.milestoneName)} helperText={form.formState.errors.milestoneName ? translate('projectManagement.home.validationRequired') : undefined} />
      <PmBox sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr' }, gap: 1.5 }}>
        <PmFormSelect label={translate('projectManagement.home.status')} value={form.watch('status')} onChange={event => form.setValue('status', String(event.target.value), { shouldDirty: true })}><PmMenuItem value="Planning">{translate('projectManagement.home.status.Planning')}</PmMenuItem><PmMenuItem value="Active">{translate('projectManagement.home.status.Active')}</PmMenuItem><PmMenuItem value="Completed">{translate('projectManagement.home.status.Completed')}</PmMenuItem></PmFormSelect>
        <PmFormInput type="number" label={translate('projectManagement.home.progress')} {...form.register('progressPercent', { setValueAs: value => Number(value) })} />
      </PmBox>
      <PmBox sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr' }, gap: 1.5 }}>
        <PmFormInput type="date" label={translate('projectManagement.home.field.startDate')} {...form.register('startDate')} />
        <PmBox><PmFormInput type="date" label={translate('projectManagement.home.field.targetDate')} {...form.register('dueDate')} error={Boolean(form.formState.errors.dueDate)} />{form.formState.errors.dueDate?.message && <PmText color="error.main" fontSize=".75rem">{form.formState.errors.dueDate.message}</PmText>}</PmBox>
      </PmBox>
      <PmFormInput multiline minRows={4} label={translate('projectManagement.home.field.description')} {...form.register('description')} />
    </PmStack>
  </PmDialog>;
}
