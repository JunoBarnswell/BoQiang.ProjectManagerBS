import { zodResolver } from '@hookform/resolvers/zod';
import { useEffect } from 'react';
import { useForm } from 'react-hook-form';
import { z } from 'zod';

import type { ProjectManagementResource, ProjectManagementResourceUpsertRequest } from '../../../api/project-management/projectManagement.types';
import { useI18n } from '../../../core/i18n/I18nProvider';
import { PmButton, PmDialog, PmFormInput, PmStack } from '../../../ui/project-management';

const schema = z.object({
  resourceName: z.string().trim().min(1),
  resourceUrl: z.string().trim().url(),
  description: z.string().optional(),
});

type FormValues = z.infer<typeof schema>;

export function ProjectManagementResourceDialog({
  open,
  resource,
  pending,
  onClose,
  onSubmit,
}: {
  open: boolean;
  resource?: ProjectManagementResource | null;
  pending: boolean;
  onClose: () => void;
  onSubmit: (value: ProjectManagementResourceUpsertRequest) => void;
}) {
  const { translate } = useI18n();
  const form = useForm<FormValues>({
    defaultValues: { resourceName: '', resourceUrl: '', description: '' },
    resolver: zodResolver(schema),
  });
  const editing = Boolean(resource);

  useEffect(() => {
    if (!open) return;
    form.reset(resource ? {
      resourceName: resource.resourceName,
      resourceUrl: resource.resourceUrl,
      description: resource.description ?? '',
    } : { resourceName: '', resourceUrl: '', description: '' });
  }, [form, open, resource]);

  const submit = form.handleSubmit(value => onSubmit({ ...value, versionNo: resource?.versionNo }));
  const invalid = form.formState.errors;

  return <PmDialog
    actions={<><PmButton color="inherit" disabled={pending} onClick={onClose}>{translate('projectManagement.home.cancel')}</PmButton><PmButton disabled={pending} loading={pending} onClick={() => void submit()} variant="contained">{translate('projectManagement.home.saveUpdate')}</PmButton></>}
    onClose={onClose}
    open={open}
    title={editing ? translate('projectManagement.home.resourceEdit') : translate('projectManagement.home.resourceCreate')}
  >
    <PmStack component="form" onSubmit={event => { event.preventDefault(); void submit(); }} spacing={2} sx={{ pt: 1 }}>
      <PmFormInput autoFocus label={translate('projectManagement.home.resourceName')} {...form.register('resourceName')} error={Boolean(invalid.resourceName)} helperText={invalid.resourceName ? translate('projectManagement.home.validationRequired') : undefined} />
      <PmFormInput label={translate('projectManagement.home.resourceUrl')} {...form.register('resourceUrl')} error={Boolean(invalid.resourceUrl)} helperText={invalid.resourceUrl ? translate('projectManagement.home.validationUrl') : undefined} />
      <PmFormInput multiline minRows={4} label={translate('projectManagement.home.resourceDescription')} {...form.register('description')} />
    </PmStack>
  </PmDialog>;
}
