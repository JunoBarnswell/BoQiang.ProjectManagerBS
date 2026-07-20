import { PmBox, PmText } from '../../../ui/project-management';

import type { ProjectManagementProjectConflict } from './projectManagementProjectConflict';

export function ProjectManagementProjectConflictPanel({ conflict, translate }: { conflict: ProjectManagementProjectConflict; translate: (key: string) => string }) {
  return <PmBox role="alert" sx={{ display: 'grid', gap: 1, p: 1.5, mt: 1.5, border: 1, borderColor: 'warning.main', borderRadius: 1, bgcolor: 'warning.light' }}>
    <PmText fontSize=".78rem" fontWeight={700}>{translate('projectManagement.home.conflictDetails')}</PmText>
    <PmText color="text.secondary" fontSize=".72rem">{translate('projectManagement.home.conflictDetailsHint')}</PmText>
    {conflict.fieldConflicts.map(field => <PmBox key={field.field} sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '110px 1fr 1fr' }, gap: .75, fontSize: '.72rem' }}><PmText fontSize="inherit" fontWeight={600}>{field.displayName}</PmText><PmText fontSize="inherit">{translate('projectManagement.home.conflictServer')}: {formatValue(field.serverValue)}</PmText><PmText fontSize="inherit">{translate('projectManagement.home.conflictLocal')}: {formatValue(field.localValue)}</PmText></PmBox>)}
  </PmBox>;
}

function formatValue(value: unknown): string {
  if (value === null || value === undefined || value === '') return '—';
  if (typeof value === 'object') return JSON.stringify(value);
  return String(value);
}
