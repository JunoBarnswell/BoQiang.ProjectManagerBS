import { zodResolver } from '@hookform/resolvers/zod';
import { Box, FormHelperText, Grid, Stack } from '@mui/material';
import { useEffect } from 'react';
import { useForm } from 'react-hook-form';
import { z } from 'zod';

import type { ProjectManagementProjectUpsertRequest } from '../../../api/project-management/projectManagement.types';
import { useI18n } from '../../../core/i18n/I18nProvider';
import { PmButton, PmDialog, PmFormInput, PmFormSelect, PmMenuItem } from '../../../ui/project-management';

const schema = z.object({
  projectCode: z.string().trim().min(1),
  projectName: z.string().trim().min(1),
  status: z.string().min(1),
  priority: z.string().min(1),
  startDate: z.string().optional(),
  dueDate: z.string().optional(),
  description: z.string().optional(),
}).refine(value => !value.startDate || !value.dueDate || value.dueDate >= value.startDate, { message: 'Target date must not be earlier than start date', path: ['dueDate'] });

type FormValues = z.infer<typeof schema>;

export function ProjectCreateDialog({ open, initialValue, editing, pending, onClose, onSubmit }: { open: boolean; initialValue: ProjectManagementProjectUpsertRequest; editing: boolean; pending: boolean; onClose: () => void; onSubmit: (value: ProjectManagementProjectUpsertRequest) => void }) {
  const { translate } = useI18n();
  const form = useForm<FormValues>({ defaultValues: initialValue, resolver: zodResolver(schema) });
  useEffect(() => { if (open) form.reset(initialValue); }, [form, initialValue, open]);
  const submit = form.handleSubmit(value => onSubmit({ ...initialValue, ...value }));
  return <PmDialog actions={<><PmButton color="inherit" onClick={onClose}>{translate('projectManagement.home.cancel')}</PmButton><PmButton disabled={pending} loading={pending} onClick={() => void submit()} variant="contained">{editing ? translate('projectManagement.home.saveUpdate') : translate('projectManagement.home.submitCreate')}</PmButton></>} onClose={onClose} open={open} title={editing ? translate('projectManagement.home.edit') : translate('projectManagement.home.create')}>
    <Stack component="form" onSubmit={event => { event.preventDefault(); void submit(); }} spacing={2} sx={{ pt: 1 }}>
      <Box><PmFormInput autoFocus label={translate('projectManagement.home.field.name')} {...form.register('projectName')} error={Boolean(form.formState.errors.projectName)} helperText={form.formState.errors.projectName ? translate('projectManagement.home.validationRequired') : undefined} /></Box>
      <Grid container spacing={1.5}>
        <Grid size={{ xs: 12, sm: 6 }}><PmFormInput label={translate('projectManagement.home.field.code')} {...form.register('projectCode')} error={Boolean(form.formState.errors.projectCode)} /></Grid>
        <Grid size={{ xs: 12, sm: 3 }}><PmFormSelect label={translate('projectManagement.home.status')} value={form.watch('status')} onChange={event => form.setValue('status', String(event.target.value), { shouldDirty: true })}><PmMenuItem value="Planning">{translate('projectManagement.home.status.Planning')}</PmMenuItem><PmMenuItem value="Active">{translate('projectManagement.home.status.Active')}</PmMenuItem><PmMenuItem value="Paused">{translate('projectManagement.home.status.Paused')}</PmMenuItem></PmFormSelect></Grid>
        <Grid size={{ xs: 12, sm: 3 }}><PmFormSelect label={translate('projectManagement.home.priority')} value={form.watch('priority')} onChange={event => form.setValue('priority', String(event.target.value), { shouldDirty: true })}><PmMenuItem value="Low">{translate('projectManagement.home.priority.Low')}</PmMenuItem><PmMenuItem value="Medium">{translate('projectManagement.home.priority.Medium')}</PmMenuItem><PmMenuItem value="High">{translate('projectManagement.home.priority.High')}</PmMenuItem><PmMenuItem value="Urgent">{translate('projectManagement.home.priority.Urgent')}</PmMenuItem></PmFormSelect></Grid>
      </Grid>
      <Grid container spacing={1.5}>
        <Grid size={{ xs: 12, sm: 6 }}><PmFormInput label={translate('projectManagement.home.field.startDate')} type="date" {...form.register('startDate')} /></Grid>
        <Grid size={{ xs: 12, sm: 6 }}><PmFormInput label={translate('projectManagement.home.field.targetDate')} type="date" {...form.register('dueDate')} error={Boolean(form.formState.errors.dueDate)} /><FormHelperText error>{form.formState.errors.dueDate?.message}</FormHelperText></Grid>
      </Grid>
      <PmFormInput multiline minRows={5} label={translate('projectManagement.home.field.description')} {...form.register('description')} />
      <FormHelperText>{translate('projectManagement.home.formHint')}</FormHelperText>
    </Stack>
  </PmDialog>;
}
