import { Avatar, Button, ButtonBase, IconButton, ListItemIcon, ListItemText, Menu, MenuItem, TextField } from '@mui/material';
import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';

import { useI18n } from '../../../core/i18n/I18nProvider';
import { PermissionButton } from '../../../shared/auth/PermissionButton';
import { flowisePermissions } from '../../../shared/auth/permissionCodes';
import { AppIcon } from '../../../shared/icons/AppIcon';
import { flowiseI18nKeys } from '../i18n/flowiseI18nKeys';
import type { FlowiseCanvasMode } from '../types/canvas.types';

import type { FlowiseCanvasHeaderDialogKind } from './FlowiseCanvasHeaderDialogs';

interface FlowiseCanvasHeaderProps {
  chatflowName?: string | null;
  dirty: boolean;
  mode: FlowiseCanvasMode;
  renaming?: boolean;
  saving?: boolean;
  title: string;
  upsertAvailable: boolean;
  onDuplicate?: () => void;
  onOpenChat: () => void;
  onOpenDialog: (dialog: FlowiseCanvasHeaderDialogKind) => void;
  onOpenValidation: () => void;
  onRename?: (name: string) => Promise<unknown> | unknown;
  onRun: () => void;
  onSave: () => void;
}

export function FlowiseCanvasHeader({
  chatflowName,
  dirty,
  mode,
  renaming,
  saving,
  title,
  upsertAvailable,
  onDuplicate,
  onOpenChat,
  onOpenDialog,
  onOpenValidation,
  onRename,
  onRun,
  onSave
}: FlowiseCanvasHeaderProps) {
  const { translate } = useI18n();
  const backTo = mode.includes('agentflow') ? '/flowise/workflows' : mode.includes('marketplace') ? '/flowise/marketplaces' : '/flowise/chatflows';
  const [editingName, setEditingName] = useState(false);
  const [draftName, setDraftName] = useState(chatflowName ?? '');
  const [settingsAnchor, setSettingsAnchor] = useState<HTMLElement | null>(null);
  const displayedName = chatflowName ?? title;
  const displayedMode = mode.includes('agentflow')
    ? translate(flowiseI18nKeys.pages.workflows)
    : mode.includes('marketplace')
      ? translate(flowiseI18nKeys.pages.marketplaces)
      : translate(flowiseI18nKeys.pages.chatflows);

  useEffect(() => {
    setDraftName(chatflowName ?? '');
    setEditingName(false);
  }, [chatflowName]);

  const submitName = async () => {
    const nextName = draftName.trim();
    if (!nextName || nextName === chatflowName || !onRename) {
      setEditingName(false);
      return;
    }

    await onRename(nextName);
    setEditingName(false);
  };

  return (
    <header className="flowise-canvas-header">
      <div className="flowise-canvas-header__main">
        <Link className="btn-ghost" to={backTo}>
          <AppIcon name="arrow-left" />
        </Link>
        <div className="flowise-canvas-header__title">
          {editingName ? (
            <div className="flowise-canvas-header__rename">
              <TextField
                autoFocus
                disabled={renaming}
                size="small"
                value={draftName}
                onChange={(event) => setDraftName(event.target.value)}
                onKeyDown={(event) => {
                  if (event.key === 'Enter') {
                    void submitName();
                    return;
                  }

                  if (event.key === 'Escape') {
                    setDraftName(chatflowName ?? '');
                    setEditingName(false);
                  }
                }}
              />
              <IconButton disabled={renaming || draftName.trim().length === 0} size="small" title={translate(flowiseI18nKeys.common.save)} onClick={() => void submitName()}>
                <AppIcon name="check" />
              </IconButton>
              <IconButton disabled={renaming} size="small" title={translate(flowiseI18nKeys.common.cancel)} onClick={() => {
                setDraftName(chatflowName ?? '');
                setEditingName(false);
              }}>
                <AppIcon name="x" />
              </IconButton>
            </div>
          ) : (
            <div className="flowise-canvas-header__name-row">
              <h2>{dirty ? <strong>*</strong> : null} {displayedName}</h2>
              {chatflowName && onRename ? (
                <IconButton size="small" title={translate(flowiseI18nKeys.actions.rename)} onClick={() => setEditingName(true)}>
                  <AppIcon name="edit" />
                </IconButton>
              ) : null}
            </div>
          )}
          <span>{displayedMode}</span>
        </div>
      </div>
      <div className="flowise-canvas-header__actions">
        {chatflowName ? (
          <FlowiseCanvasHeaderAction icon="code" label={translate(flowiseI18nKeys.actions.apiCode)} onClick={() => onOpenDialog('apiCode')} />
        ) : null}
        <FlowiseCanvasHeaderAction icon="chat-circle-text" label={translate(flowiseI18nKeys.detail.messages)} onClick={() => onOpenDialog('messages')} />
        <FlowiseCanvasHeaderAction icon="user" label={translate(flowiseI18nKeys.detail.leads)} onClick={() => onOpenDialog('leads')} />
        <FlowiseCanvasHeaderAction icon="calendar" label={translate(flowiseI18nKeys.canvas.schedule)} onClick={() => onOpenDialog('schedule')} />
        <FlowiseCanvasHeaderAction icon="plugs-connected" label={translate(flowiseI18nKeys.actions.webhook)} onClick={() => onOpenDialog('webhook')} />
        <FlowiseCanvasHeaderAction icon="check" label={translate(flowiseI18nKeys.canvas.validation)} onClick={onOpenValidation} />
        <FlowiseCanvasHeaderAction icon="chat-circle-text" label={translate(flowiseI18nKeys.actions.chatTest)} onClick={onOpenChat} />
        <FlowiseCanvasHeaderAction icon="play" label={translate(flowiseI18nKeys.actions.run)} onClick={onRun} />
        <PermissionButton className="flowise-canvas-header__icon-save" code={flowisePermissions.edit} disabled={saving} iconStart={false} type="button" onClick={onSave}>
          <AppIcon name="floppy-disk" />
        </PermissionButton>
        <ButtonBase className="flowise-canvas-header__icon-action" title={translate(flowiseI18nKeys.actions.settings)} onClick={(event) => setSettingsAnchor(event.currentTarget)}>
          <Avatar variant="rounded">
            <AppIcon name="settings" />
          </Avatar>
        </ButtonBase>
        <Menu anchorEl={settingsAnchor} open={Boolean(settingsAnchor)} onClose={() => setSettingsAnchor(null)}>
          <FlowiseCanvasSettingsItem icon="sliders-horizontal" label={translate(flowiseI18nKeys.detail.configuration)} onClick={() => {
            setSettingsAnchor(null);
            onOpenDialog('configuration');
          }} />
          <FlowiseCanvasSettingsItem icon="download-simple" label={translate(flowiseI18nKeys.actions.template)} onClick={() => {
            setSettingsAnchor(null);
            onOpenDialog('exportTemplate');
          }} />
          <FlowiseCanvasSettingsItem icon="upload-simple" label={translate(flowiseI18nKeys.actions.share)} onClick={() => {
            setSettingsAnchor(null);
            onOpenDialog('share');
          }} />
          {upsertAvailable ? (
            <>
              <FlowiseCanvasSettingsItem icon="database" label={translate(flowiseI18nKeys.actions.upsert)} onClick={() => {
                setSettingsAnchor(null);
                onOpenDialog('upsert');
              }} />
              <FlowiseCanvasSettingsItem icon="clock-counter-clockwise" label={translate(flowiseI18nKeys.detail.upsertHistory)} onClick={() => {
                setSettingsAnchor(null);
                onOpenDialog('upsertHistory');
              }} />
            </>
          ) : null}
          <FlowiseCanvasSettingsItem icon="copy" label={translate(flowiseI18nKeys.actions.duplicate)} disabled={!onDuplicate} onClick={() => {
            setSettingsAnchor(null);
            onDuplicate?.();
          }} />
        </Menu>
      </div>
    </header>
  );
}

function FlowiseCanvasHeaderAction({
  icon,
  label,
  onClick
}: {
  icon: string;
  label: string;
  onClick: () => void;
}) {
  return (
    <Button className="flowise-canvas-header__text-action" size="small" startIcon={<AppIcon name={icon} />} type="button" variant="outlined" onClick={onClick}>
      {label}
    </Button>
  );
}

function FlowiseCanvasSettingsItem({
  disabled,
  icon,
  label,
  onClick
}: {
  disabled?: boolean;
  icon: string;
  label: string;
  onClick: () => void;
}) {
  return (
    <MenuItem disabled={disabled} onClick={onClick}>
      <ListItemIcon>
        <AppIcon name={icon} />
      </ListItemIcon>
      <ListItemText>{label}</ListItemText>
    </MenuItem>
  );
}
