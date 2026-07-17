import ContentCopyIcon from '@mui/icons-material/ContentCopy';
import DeleteIcon from '@mui/icons-material/Delete';
import EditIcon from '@mui/icons-material/Edit';
import ExpandLessIcon from '@mui/icons-material/ExpandLess';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import VisibilityIcon from '@mui/icons-material/Visibility';
import VisibilityOffIcon from '@mui/icons-material/VisibilityOff';
import {
  Chip,
  Collapse,
  IconButton,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Tooltip,
  Typography
} from '@mui/material';
import { useState } from 'react';

import { usePermission } from '../../../../../core/auth/usePermission';
import { useI18n } from '../../../../../core/i18n/I18nProvider';
import { flowisePermissions } from '../../../../../shared/auth/permissionCodes';
import { PermissionMuiIconButton } from '../../../../../shared/auth/PermissionMuiIconButton';
import { flowiseI18nKeys } from '../../../i18n/flowiseI18nKeys';
import type { FlowiseResourceDto } from '../../../types/shared.types';
import { formatSourceDateOnly, parseJsonRecord, readStringList } from '../common/sourcePageUtils';

interface APIKeyRowProps {
  item: FlowiseResourceDto;
  revealedValue?: string;
  onCopy: (value: string) => void;
  onDelete: (item: FlowiseResourceDto) => void;
  onEdit: (item: FlowiseResourceDto) => void;
}

export function APIKeyRow({ item, revealedValue, onCopy, onDelete, onEdit }: APIKeyRowProps) {
  const { translate } = useI18n();
  const canEdit = usePermission(flowisePermissions.apiKeysEdit).hasPermission;
  const [expanded, setExpanded] = useState(false);
  const [visible, setVisible] = useState(false);
  const metadata = parseJsonRecord(item.metadataJson);
  const permissions = readStringList(metadata.permissions);
  const chatFlows = Array.isArray(metadata.chatFlows) ? metadata.chatFlows as Array<Record<string, unknown>> : [];
  const secret = visible && revealedValue ? revealedValue : item.secretMask ?? '******';

  return (
    <>
      <TableRow hover>
        <TableCell sx={{ width: '15%' }}>{item.displayName}</TableCell>
        <TableCell sx={{ width: '25%' }}>
          <Stack direction="row" spacing={0.5} sx={{ alignItems: 'center' }}>
            <Typography className="flowise-source-secret" component="span">{secret}</Typography>
            <Tooltip title={translate(flowiseI18nKeys.actions.copy)}>
              <IconButton color="success" disabled={!revealedValue && !item.secretMask} size="small" onClick={() => onCopy(revealedValue ?? item.secretMask ?? '')}>
                <ContentCopyIcon fontSize="small" />
              </IconButton>
            </Tooltip>
            <Tooltip title={translate(visible ? flowiseI18nKeys.actions.close : flowiseI18nKeys.actions.reveal)}>
              <span>
                <IconButton disabled={!revealedValue} size="small" onClick={() => setVisible((current) => !current)}>
                  {visible ? <VisibilityOffIcon fontSize="small" /> : <VisibilityIcon fontSize="small" />}
                </IconButton>
              </span>
            </Tooltip>
          </Stack>
        </TableCell>
        <TableCell sx={{ width: '25%' }}>
          <Stack direction="row" sx={{ flexWrap: 'wrap', gap: 0.75 }}>
            {permissions.length ? permissions.map((permission) => <Chip key={permission} label={permission} size="small" />) : <span>-</span>}
          </Stack>
        </TableCell>
        <TableCell>
          <Stack direction="row" spacing={0.5} sx={{ alignItems: 'center' }}>
            <span>{chatFlows.length}</span>
            {chatFlows.length ? (
              <IconButton aria-label={translate(flowiseI18nKeys.actions.expand)} size="small" onClick={() => setExpanded((current) => !current)}>
                {expanded ? <ExpandLessIcon fontSize="small" /> : <ExpandMoreIcon fontSize="small" />}
              </IconButton>
            ) : null}
          </Stack>
        </TableCell>
        <TableCell>{formatSourceDateOnly(item.updatedTime ?? item.createdTime)}</TableCell>
        {canEdit ? (
          <>
            <TableCell align="center">
              <PermissionMuiIconButton code={flowisePermissions.apiKeysEdit} color="primary" title={translate(flowiseI18nKeys.actions.edit)} onClick={() => onEdit(item)}>
                <EditIcon fontSize="small" />
              </PermissionMuiIconButton>
            </TableCell>
            <TableCell align="center">
              <PermissionMuiIconButton code={flowisePermissions.apiKeysEdit} color="error" title={translate(flowiseI18nKeys.actions.delete)} onClick={() => onDelete(item)}>
                <DeleteIcon fontSize="small" />
              </PermissionMuiIconButton>
            </TableCell>
          </>
        ) : null}
      </TableRow>
      {expanded ? (
        <TableRow>
          <TableCell colSpan={canEdit ? 7 : 5} sx={{ p: 2 }}>
            <Collapse in={expanded} timeout="auto" unmountOnExit>
              <Table className="flowise-source-inner-table" size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>{translate(flowiseI18nKeys.source.fields.chatflowName)}</TableCell>
                    <TableCell>{translate(flowiseI18nKeys.source.fields.modifiedOn)}</TableCell>
                    <TableCell>{translate(flowiseI18nKeys.fields.category)}</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {chatFlows.map((flow, index) => (
                    <TableRow key={`${String(flow.id ?? flow.flowName ?? index)}`}>
                      <TableCell>{String(flow.flowName ?? flow.name ?? '-')}</TableCell>
                      <TableCell>{formatSourceDateOnly(String(flow.updatedDate ?? flow.updatedTime ?? ''))}</TableCell>
                      <TableCell>{String(flow.category ?? '-')}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </Collapse>
          </TableCell>
        </TableRow>
      ) : null}
    </>
  );
}
