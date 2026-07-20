import { useEffect, useState } from 'react';

import type { ProjectManagementProjectReminderCreateRequest } from '../../../api/project-management/projectManagement.types';
import { useI18n } from '../../../core/i18n/I18nProvider';
import { PmButton, PmDialog, PmFormInput, PmStack, PmText } from '../../../ui/project-management';

function defaultReminderInput(): string {
  const value = new Date();
  value.setDate(value.getDate() + 1);
  value.setHours(9, 0, 0, 0);
  const offset = value.getTimezoneOffset() * 60000;
  return new Date(value.getTime() - offset).toISOString().slice(0, 16);
}

export function ProjectHomeReminderDialog({ open, projectName, pending, onClose, onSubmit }: { open: boolean; projectName: string; pending: boolean; onClose: () => void; onSubmit: (request: ProjectManagementProjectReminderCreateRequest) => void }) {
  const { translate } = useI18n();
  const [reminderAt, setReminderAt] = useState(defaultReminderInput);
  const [note, setNote] = useState('');
  useEffect(() => {
    if (open) {
      setReminderAt(defaultReminderInput());
      setNote('');
    }
  }, [open]);
  const submit = () => {
    const localDate = new Date(reminderAt);
    if (Number.isNaN(localDate.getTime()) || localDate.getTime() <= Date.now()) return;
    onSubmit({ reminderAt: localDate.toISOString(), timeZoneId: Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC', note: note.trim() || undefined, clientRequestId: typeof crypto !== 'undefined' && 'randomUUID' in crypto ? crypto.randomUUID() : `${Date.now()}-${Math.random()}` });
  };
  return <PmDialog actions={<><PmButton color="inherit" disabled={pending} onClick={onClose}>{translate('projectManagement.home.reminder.cancel')}</PmButton><PmButton disabled={pending} loading={pending} onClick={submit} variant="contained">{translate('projectManagement.home.reminder.submit')}</PmButton></>} onClose={onClose} open={open} title={translate('projectManagement.home.reminder.title')}>
    <PmStack spacing={1.5} sx={{ pt: 1 }}>
      <PmText color="text.secondary" fontSize=".82rem">{projectName}</PmText>
      <PmFormInput label={translate('projectManagement.home.reminder.time')} type="datetime-local" value={reminderAt} onChange={event => setReminderAt(event.target.value)} />
      <PmFormInput label={translate('projectManagement.home.reminder.note')} multiline minRows={3} value={note} onChange={event => setNote(event.target.value)} />
    </PmStack>
  </PmDialog>;
}
