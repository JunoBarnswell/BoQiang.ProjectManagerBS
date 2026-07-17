import AddIcon from '@mui/icons-material/Add';
import DeleteIcon from '@mui/icons-material/Delete';
import EditIcon from '@mui/icons-material/Edit';
import SmartToyIcon from '@mui/icons-material/SmartToy';
import { Box, Button, Card, CardActions, CardContent, Chip, Dialog, DialogActions, DialogContent, DialogTitle, Grid, Skeleton, Stack, TextField, Typography } from '@mui/material';
import { useState } from 'react';
import { Link } from 'react-router-dom';

import { useI18n } from '../../../../../core/i18n/I18nProvider';
import { useApiMutation } from '../../../../../core/query/useApiMutation';
import { useApiQuery } from '../../../../../core/query/useApiQuery';
import { flowisePermissions } from '../../../../../shared/auth/permissionCodes';
import { PermissionMuiButton } from '../../../../../shared/auth/PermissionMuiButton';
import { PermissionMuiIconButton } from '../../../../../shared/auth/PermissionMuiIconButton';
import { useConfirm } from '../../../../../shared/feedback/useConfirm';
import { useMessage } from '../../../../../shared/feedback/useMessage';
import { getErrorMessage } from '../../../../../shared/utils/errorMessage';
import { flowiseAssistantsApi } from '../../../api/assistants.api';
import { flowiseI18nKeys } from '../../../i18n/flowiseI18nKeys';
import type { FlowiseAssistantDto, FlowiseAssistantUpsertRequest } from '../../../types/assistant.types';
import assistantEmptySvg from '../../assets/images/assistant_empty.svg';
import { buildSourceQuery, formatSourceDate } from '../common/sourcePageUtils';

function createAssistantDraft(overrides: Partial<FlowiseAssistantUpsertRequest> = {}): FlowiseAssistantUpsertRequest {
  return {
    advancedMetadataJson: '{}',
    assistantKey: '',
    assistantType: 'custom',
    definition: {
      fileIds: [],
      instructions: '',
      model: '',
      responseFormat: 'auto',
      temperature: null,
      tools: [],
      topP: null
    },
    description: '',
    name: '',
    status: 'Enabled',
    workspaceId: '',
    ...overrides
  };
}

function toAssistantDraft(item: FlowiseAssistantDto): FlowiseAssistantUpsertRequest {
  return createAssistantDraft({
    advancedMetadataJson: item.advancedMetadataJson,
    assistantKey: item.assistantKey,
    assistantType: item.assistantType,
    definition: {
      fileIds: item.definition.fileIds ?? [],
      instructions: item.definition.instructions ?? '',
      model: item.definition.model ?? '',
      responseFormat: item.definition.responseFormat ?? 'auto',
      temperature: item.definition.temperature ?? null,
      tools: item.definition.tools ?? [],
      topP: item.definition.topP ?? null
    },
    description: item.description ?? '',
    name: item.name,
    status: item.status,
    workspaceId: item.workspaceId ?? ''
  });
}

function splitCsv(value: string): string[] {
  return value.split(',').map((item) => item.trim()).filter(Boolean);
}

function joinList(values: readonly string[] | undefined): string {
  return values?.join(', ') ?? '';
}

function nullableNumber(value: string): number | null {
  return value === '' ? null : Number(value);
}

export function FlowiseAssistantsNativePage() {
  const { translate } = useI18n();
  const confirm = useConfirm();
  const message = useMessage();
  const [keyword, setKeyword] = useState('');
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<FlowiseAssistantDto | null>(null);
  const [draft, setDraft] = useState<FlowiseAssistantUpsertRequest>(() => createAssistantDraft());
  const query = useApiQuery({
    keepPreviousData: true,
    queryKey: ['flowise-source-assistants', keyword],
    queryFn: ({ signal }) => flowiseAssistantsApi.list(buildSourceQuery(keyword, '', 1, 24), signal)
  });
  const upsertMutation = useApiMutation({
    mutationFn: (request: FlowiseAssistantUpsertRequest) => editing ? flowiseAssistantsApi.update(editing.id, request) : flowiseAssistantsApi.create(request),
    onError: (error) => message.error(getErrorMessage(error, translate(flowiseI18nKeys.messages.saveFailed))),
    onSuccess: async () => { setDialogOpen(false); setEditing(null); await query.refetch(); }
  });
  const deleteMutation = useApiMutation({
    mutationFn: (id: string) => flowiseAssistantsApi.delete(id),
    onSuccess: async () => query.refetch()
  });
  const rows = query.data?.data.items ?? [];

  const openDialog = (item?: FlowiseAssistantDto) => {
    setEditing(item ?? null);
    setDraft(item ? toAssistantDraft(item) : createAssistantDraft());
    setDialogOpen(true);
  };

  const deleteAssistant = (item: FlowiseAssistantDto) => confirm({
    title: translate(flowiseI18nKeys.messages.deleteConfirmTitle),
    content: `Delete assistant ${item.name}?`,
    confirmText: translate(flowiseI18nKeys.actions.delete),
    onConfirm: async () => {
      await deleteMutation.mutateAsync(item.id);
    }
  });

  return (
    <section className="flowise-source-page">
      <header className="flowise-source-view-header">
        <div><h1>Assistants</h1><p>Create and manage OpenAI and custom assistants</p></div>
        <PermissionMuiButton code={flowisePermissions.assistantsEdit} startIcon={<AddIcon />} variant="contained" onClick={() => openDialog()}>
          Add Assistant
        </PermissionMuiButton>
      </header>
      <Stack className="flowise-source-toolbar"><TextField fullWidth placeholder="Search Assistants" size="small" value={keyword} onChange={(event) => setKeyword(event.target.value)} /></Stack>
      {!query.isLoading && rows.length === 0 ? <Box className="flowise-source-empty"><img alt="AssistantEmptySVG" src={assistantEmptySvg} /><div>No Assistants Yet</div></Box> : null}
      <Grid container spacing={2}>
        {query.isLoading ? [0, 1, 2].map((index) => <Grid size={{ xs: 12, md: 4 }} key={index}><Skeleton height={180} variant="rounded" /></Grid>) : rows.map((item) => (
            <Grid size={{ xs: 12, md: 4 }} key={item.id}>
              <Card className="flowise-source-card">
                <CardContent>
                  <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}><SmartToyIcon color="primary" /><Typography variant="h6">{item.name}</Typography></Stack>
                  <Typography color="text.secondary" sx={{ mt: 1 }}>{item.description || item.assistantKey}</Typography>
                  <Stack direction="row" sx={{ flexWrap: 'wrap', gap: 0.75, mt: 2 }}>
                    <Chip label={item.assistantType || 'custom'} size="small" />
                    <Chip label={item.definition.model || item.status} size="small" />
                    <Chip label={formatSourceDate(item.updatedTime ?? item.createdTime)} size="small" />
                  </Stack>
                </CardContent>
                <CardActions>
                  <Button component={Link} to={`/flowise/assistants/custom/${item.id}`}>Open</Button>
                  <PermissionMuiIconButton code={flowisePermissions.assistantsEdit} color="primary" onClick={() => openDialog(item)}>
                    <EditIcon fontSize="small" />
                  </PermissionMuiIconButton>
                  <PermissionMuiIconButton code={flowisePermissions.assistantsEdit} color="error" onClick={() => deleteAssistant(item)}>
                    <DeleteIcon fontSize="small" />
                  </PermissionMuiIconButton>
                </CardActions>
              </Card>
            </Grid>
          ))}
      </Grid>
      <Dialog fullWidth maxWidth="md" open={dialogOpen} onClose={() => setDialogOpen(false)}>
        <DialogTitle>{editing ? 'Edit Assistant' : 'Add Assistant'}</DialogTitle>
        <DialogContent><Stack spacing={2} sx={{ pt: 1 }}>
          <TextField label="Name" value={draft.name} onChange={(event) => setDraft({ ...draft, name: event.target.value })} />
          <TextField label="Key" value={draft.assistantKey} onChange={(event) => setDraft({ ...draft, assistantKey: event.target.value })} />
          <TextField label="Type" value={draft.assistantType ?? ''} onChange={(event) => setDraft({ ...draft, assistantType: event.target.value })} />
          <TextField label="Model" value={draft.definition.model ?? ''} onChange={(event) => setDraft({ ...draft, definition: { ...draft.definition, model: event.target.value } })} />
          <TextField multiline label="Instructions" minRows={4} value={draft.definition.instructions ?? ''} onChange={(event) => setDraft({ ...draft, definition: { ...draft.definition, instructions: event.target.value } })} />
          <TextField label="Tools" value={joinList(draft.definition.tools)} onChange={(event) => setDraft({ ...draft, definition: { ...draft.definition, tools: splitCsv(event.target.value) } })} />
          <TextField label="File IDs" value={joinList(draft.definition.fileIds)} onChange={(event) => setDraft({ ...draft, definition: { ...draft.definition, fileIds: splitCsv(event.target.value) } })} />
          <Grid container spacing={2}>
            <Grid size={{ xs: 12, md: 4 }}><TextField fullWidth label="Response Format" value={draft.definition.responseFormat ?? ''} onChange={(event) => setDraft({ ...draft, definition: { ...draft.definition, responseFormat: event.target.value } })} /></Grid>
            <Grid size={{ xs: 12, md: 4 }}><TextField fullWidth label="Temperature" type="number" value={draft.definition.temperature ?? ''} onChange={(event) => setDraft({ ...draft, definition: { ...draft.definition, temperature: nullableNumber(event.target.value) } })} /></Grid>
            <Grid size={{ xs: 12, md: 4 }}><TextField fullWidth label="Top P" type="number" value={draft.definition.topP ?? ''} onChange={(event) => setDraft({ ...draft, definition: { ...draft.definition, topP: nullableNumber(event.target.value) } })} /></Grid>
          </Grid>
          <TextField multiline label="Advanced Metadata JSON" minRows={3} value={draft.advancedMetadataJson ?? '{}'} onChange={(event) => setDraft({ ...draft, advancedMetadataJson: event.target.value })} />
          <TextField multiline label="Description" minRows={2} value={draft.description ?? ''} onChange={(event) => setDraft({ ...draft, description: event.target.value })} />
        </Stack></DialogContent>
        <DialogActions>
          <Button onClick={() => setDialogOpen(false)}>Cancel</Button>
          <PermissionMuiButton code={flowisePermissions.assistantsEdit} disabled={!draft.name || !draft.assistantKey || upsertMutation.isPending} variant="contained" onClick={() => upsertMutation.mutate(draft)}>
            Save
          </PermissionMuiButton>
        </DialogActions>
      </Dialog>
    </section>
  );
}
