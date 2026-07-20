import { zodResolver } from '@hookform/resolvers/zod';
import { useEffect } from 'react';
import { useForm } from 'react-hook-form';
import { z } from 'zod';

import { useI18n } from '../../../core/i18n/I18nProvider';
import { PmButton, PmDialog, PmFormInput } from '../../../ui/project-management';

const schema = z.object({ body: z.string().trim().min(1).max(10_000) });
type FormValues = z.infer<typeof schema>;

export function ProjectManagementProjectUpdateDialog({ open, pending, onClose, onSubmit }: { open: boolean; pending: boolean; onClose: () => void; onSubmit: (body: string) => void }) {
  const { translate } = useI18n();
  const form = useForm<FormValues>({ defaultValues: { body: '' }, resolver: zodResolver(schema) });
  useEffect(() => { if (open) form.reset({ body: '' }); }, [form, open]);
  const submit = form.handleSubmit(value => onSubmit(value.body));
  return <PmDialog actions={<><PmButton color="inherit" disabled={pending} onClick={onClose}>{translate('projectManagement.home.cancel')}</PmButton><PmButton disabled={pending} loading={pending} onClick={() => void submit()} variant="contained">{translate('projectManagement.home.postUpdate')}</PmButton></>} onClose={onClose} open={open} title={translate('projectManagement.home.postUpdate')}>
    <PmFormInput autoFocus multiline minRows={6} label={translate('projectManagement.home.updateBody')} {...form.register('body')} error={Boolean(form.formState.errors.body)} helperText={form.formState.errors.body ? translate('projectManagement.home.validationRequired') : undefined} />
  </PmDialog>;
}
