import AddIcon from '@mui/icons-material/Add';
import DeleteIcon from '@mui/icons-material/Delete';
import EditIcon from '@mui/icons-material/Edit';
import StorefrontIcon from '@mui/icons-material/Storefront';
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
import { flowiseNativeResourcesApi } from '../../../api/nativeResources.api';
import { flowiseI18nKeys } from '../../../i18n/flowiseI18nKeys';
import type { FlowiseResourceDto, FlowiseResourceUpsertRequest } from '../../../types/shared.types';
import workflowEmptySvg from '../../assets/images/workflow_empty.svg';
import { buildSourceQuery, createResourceDraft, splitTags, toResourceDraft } from '../common/sourcePageUtils';

export function FlowiseMarketplacesNativePage() {
  const confirm = useConfirm();
  const message = useMessage();
  const { translate } = useI18n();
  const [keyword, setKeyword] = useState('');
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<FlowiseResourceDto | null>(null);
  const [draft, setDraft] = useState<FlowiseResourceUpsertRequest>(() => createResourceDraft({ category: 'Chatflow', definitionJson: '{"nodes":[],"edges":[]}' }));
  const query = useApiQuery({
    keepPreviousData: true,
    queryKey: ['flowise-source-marketplaces', keyword],
    queryFn: ({ signal }) => flowiseNativeResourcesApi.marketplaces.list(buildSourceQuery(keyword, '', 1, 48), signal)
  });
  const upsertMutation = useApiMutation({
    mutationFn: (request: FlowiseResourceUpsertRequest) => editing ? flowiseNativeResourcesApi.marketplaces.update(editing.id, request) : flowiseNativeResourcesApi.marketplaces.create(request),
    onError: (error) => message.error(getErrorMessage(error, translate(flowiseI18nKeys.messages.templateSaveFailed))),
    onSuccess: async () => { setDialogOpen(false); setEditing(null); await query.refetch(); }
  });
  const deleteMutation = useApiMutation({ mutationFn: (id: string) => flowiseNativeResourcesApi.marketplaces.delete(id), onSuccess: async () => query.refetch() });
  const rows = query.data?.data.items ?? [];

  const openDialog = (item?: FlowiseResourceDto) => {
    setEditing(item ?? null);
    setDraft(item ? toResourceDraft(item) : createResourceDraft({ category: 'Chatflow', definitionJson: '{"nodes":[],"edges":[]}' }));
    setDialogOpen(true);
  };
  const deleteTemplate = (item: FlowiseResourceDto) => confirm({
    title: translate(flowiseI18nKeys.messages.deleteConfirmTitle),
    content: translate(flowiseI18nKeys.source.marketplaces.deleteConfirm).replace('{name}', item.displayName),
    confirmText: translate(flowiseI18nKeys.actions.delete),
    onConfirm: async () => {
      await deleteMutation.mutateAsync(item.id);
    }
  });

  return (
    <section className="flowise-source-page">
      <header className="flowise-source-view-header">
        <div>
          <h1>{translate(flowiseI18nKeys.pages.marketplaces)}</h1>
          <p>{translate('flowise.native.marketplaces.description')}</p>
        </div>
        <PermissionMuiButton code={flowisePermissions.marketplacesEdit} startIcon={<AddIcon />} variant="contained" onClick={() => openDialog()}>
          {translate(flowiseI18nKeys.source.marketplaces.addTemplate)}
        </PermissionMuiButton>
      </header>
      <Stack className="flowise-source-toolbar">
        <TextField
          fullWidth
          placeholder={translate(flowiseI18nKeys.source.marketplaces.search)}
          size="small"
          value={keyword}
          onChange={(event) => setKeyword(event.target.value)}
        />
      </Stack>
      {!query.isLoading && rows.length === 0 ? <Box className="flowise-source-empty"><img alt="WorkflowEmptySVG" src={workflowEmptySvg} /><div>{translate('flowise.native.marketplaces.emptyText')}</div></Box> : null}
      <Grid container spacing={2}>
        {query.isLoading ? [0, 1, 2, 3].map((index) => <Grid size={{ xs: 12, md: 3 }} key={index}><Skeleton height={190} variant="rounded" /></Grid>) : rows.map((item) => (
          <Grid size={{ xs: 12, sm: 6, lg: 3 }} key={item.id}>
            <Card className="flowise-source-card">
              <CardContent>
                <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}><StorefrontIcon color="primary" /><Typography variant="h6">{item.displayName}</Typography></Stack>
                <Typography color="text.secondary" sx={{ mt: 1 }}>{item.description || item.resourceKey}</Typography>
                <Stack direction="row" sx={{ flexWrap: 'wrap', gap: 0.75, mt: 2 }}>{splitTags(item.category).map((tag) => <Chip key={tag} label={tag} size="small" />)}</Stack>
              </CardContent>
              <CardActions>
                <Button component={Link} to={`/flowise/marketplace/${item.id}`}>{translate(flowiseI18nKeys.actions.open)}</Button>
                <PermissionMuiIconButton code={flowisePermissions.marketplacesEdit} color="primary" title={translate(flowiseI18nKeys.actions.edit)} onClick={() => openDialog(item)}>
                  <EditIcon fontSize="small" />
                </PermissionMuiIconButton>
                <PermissionMuiIconButton code={flowisePermissions.marketplacesEdit} color="error" title={translate(flowiseI18nKeys.actions.delete)} onClick={() => deleteTemplate(item)}>
                  <DeleteIcon fontSize="small" />
                </PermissionMuiIconButton>
              </CardActions>
            </Card>
          </Grid>
        ))}
      </Grid>
      <Dialog fullWidth maxWidth="md" open={dialogOpen} onClose={() => setDialogOpen(false)}>
        <DialogTitle>{editing ? translate('flowise.native.marketplaces.editTitle') : translate('flowise.native.marketplaces.createTitle')}</DialogTitle>
        <DialogContent><Stack spacing={2} sx={{ pt: 1 }}>
          <TextField label={translate(flowiseI18nKeys.fields.name)} value={draft.displayName} onChange={(event) => setDraft({ ...draft, displayName: event.target.value })} />
          <TextField label={translate(flowiseI18nKeys.fields.key)} value={draft.resourceKey} onChange={(event) => setDraft({ ...draft, resourceKey: event.target.value })} />
          <TextField label={translate(flowiseI18nKeys.fields.category)} value={draft.category ?? ''} onChange={(event) => setDraft({ ...draft, category: event.target.value })} />
          <TextField multiline label={translate(flowiseI18nKeys.source.marketplaces.templateFlowJson)} minRows={8} value={draft.definitionJson ?? '{}'} onChange={(event) => setDraft({ ...draft, definitionJson: event.target.value })} />
          <TextField multiline label={translate(flowiseI18nKeys.fields.description)} minRows={2} value={draft.description ?? ''} onChange={(event) => setDraft({ ...draft, description: event.target.value })} />
        </Stack></DialogContent>
        <DialogActions>
          <Button onClick={() => setDialogOpen(false)}>{translate(flowiseI18nKeys.common.cancel)}</Button>
          <PermissionMuiButton code={flowisePermissions.marketplacesEdit} disabled={!draft.displayName || !draft.resourceKey || upsertMutation.isPending} variant="contained" onClick={() => upsertMutation.mutate(draft)}>
            {translate(flowiseI18nKeys.common.save)}
          </PermissionMuiButton>
        </DialogActions>
      </Dialog>
    </section>
  );
}
