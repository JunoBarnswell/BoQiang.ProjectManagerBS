import BookmarksOutlinedIcon from '@mui/icons-material/BookmarksOutlined';
import CategoryIcon from '@mui/icons-material/Category';
import DeleteIcon from '@mui/icons-material/Delete';
import DownloadingIcon from '@mui/icons-material/Downloading';
import EditIcon from '@mui/icons-material/Edit';
import FileCopyIcon from '@mui/icons-material/FileCopy';
import KeyboardArrowDownIcon from '@mui/icons-material/KeyboardArrowDown';
import MicNoneOutlinedIcon from '@mui/icons-material/MicNoneOutlined';
import PictureInPictureAltIcon from '@mui/icons-material/PictureInPictureAlt';
import ThumbsUpDownOutlinedIcon from '@mui/icons-material/ThumbsUpDownOutlined';
import VpnLockOutlinedIcon from '@mui/icons-material/VpnLockOutlined';
import Button from '@mui/material/Button';
import Divider from '@mui/material/Divider';
import Menu, { type MenuProps } from '@mui/material/Menu';
import MenuItem, { type MenuItemProps } from '@mui/material/MenuItem';
import { alpha, styled } from '@mui/material/styles';
import { useId, useState } from 'react';

import { usePermission } from '../../../../../core/auth/usePermission';
import { useI18n } from '../../../../../core/i18n/I18nProvider';
import { useMessage } from '../../../../../shared/feedback/useMessage';
import { flowiseI18nKeys } from '../../../i18n/flowiseI18nKeys';
import type { FlowiseChatflowDto, FlowiseChatflowUpsertRequest } from '../../../types/chatflow.types';
import type { FlowiseResourceDto } from '../../../types/shared.types';
import { AllowedDomainsDialog } from '../dialog/AllowedDomainsDialog';
import { ChatFeedbackDialog } from '../dialog/ChatFeedbackDialog';
import { ExportAsTemplateDialog, type ExportAsTemplatePayload } from '../dialog/ExportAsTemplateDialog';
import { SaveChatflowDialog } from '../dialog/SaveChatflowDialog';
import { SpeechToTextDialog } from '../dialog/SpeechToTextDialog';
import { StarterPromptsDialog } from '../dialog/StarterPromptsDialog';
import { TagDialog } from '../dialog/TagDialog';

type FlowListDialogKind = 'allowedDomains' | 'category' | 'chatFeedback' | 'rename' | 'speechToText' | 'starterPrompts';
export type FlowListSaveAction = 'config' | 'domains' | 'update';

export interface FlowListMenuPermissions {
  config: string;
  delete: string;
  domains: string;
  duplicate: string;
  export: string;
  templateExport: string;
  update: string;
}

interface FlowListMenuProps {
  credentials: FlowiseResourceDto[];
  item: FlowiseChatflowDto;
  permissions: FlowListMenuPermissions;
  saving: boolean;
  variant?: 'ghost' | 'secondary';
  onDelete: (item: FlowiseChatflowDto) => void;
  onDuplicate: (item: FlowiseChatflowDto) => void;
  onExport: (item: FlowiseChatflowDto) => void;
  onExportTemplate: (item: FlowiseChatflowDto, payload: ExportAsTemplatePayload) => Promise<void>;
  onSaveFlow: (item: FlowiseChatflowDto, patch: Partial<FlowiseChatflowUpsertRequest>, action: FlowListSaveAction) => Promise<void>;
}

export function FlowListMenu({
  item,
  credentials,
  permissions,
  saving,
  variant = 'secondary',
  onDelete,
  onDuplicate,
  onExport,
  onExportTemplate,
  onSaveFlow
}: FlowListMenuProps) {
  const { translate } = useI18n();
  const message = useMessage();
  const menuId = useId();
  const [anchorEl, setAnchorEl] = useState<HTMLElement | null>(null);
  const [dialog, setDialog] = useState<FlowListDialogKind | 'exportTemplate' | null>(null);
  const chatbotConfig = safeParseRecord(item.chatbotConfig);
  const open = Boolean(anchorEl);
  const { hasPermission: hasMenuPermission } = usePermission(Object.values(permissions));

  if (!hasMenuPermission) {
    return null;
  }

  const openDialog = (kind: FlowListDialogKind | 'exportTemplate') => {
    setAnchorEl(null);
    setDialog(kind);
  };

  const closeDialog = () => {
    setDialog(null);
  };

  const selectMenuAction = (action: () => void) => {
    setAnchorEl(null);
    action();
  };

  const savePatch = async (patch: Partial<FlowiseChatflowUpsertRequest>, action: FlowListSaveAction) => {
    try {
      await onSaveFlow(item, patch, action);
      closeDialog();
    } catch {
      message.error(translate(flowiseI18nKeys.messages.saveFailed));
    }
  };

  return (
    <div className="flowise-native-options">
      <Button
        aria-controls={open ? menuId : undefined}
        aria-expanded={open ? 'true' : undefined}
        aria-haspopup="true"
        className={variant === 'ghost' ? 'btn-ghost' : 'btn-secondary'}
        disableElevation
        disabled={saving}
        endIcon={<KeyboardArrowDownIcon />}
        id={`${menuId}-button`}
        type="button"
        onClick={(event) => setAnchorEl(event.currentTarget)}
      >
        {translate(flowiseI18nKeys.actions.options)}
      </Button>
      <StyledMenu
        anchorEl={anchorEl}
        id={menuId}
        open={open}
        slotProps={{ list: { 'aria-labelledby': `${menuId}-button` } }}
        onClose={() => setAnchorEl(null)}
      >
        <FlowListPermissionMenuItem permission={permissions.update} onClick={() => openDialog('rename')}>
          <EditIcon />
          {translate(flowiseI18nKeys.actions.rename)}
        </FlowListPermissionMenuItem>
        <FlowListPermissionMenuItem permission={permissions.duplicate} onClick={() => selectMenuAction(() => onDuplicate(item))}>
          <FileCopyIcon />
          {translate(flowiseI18nKeys.actions.duplicate)}
        </FlowListPermissionMenuItem>
        <FlowListPermissionMenuItem permission={permissions.export} onClick={() => selectMenuAction(() => onExport(item))}>
          <DownloadingIcon />
          {translate(flowiseI18nKeys.actions.export)}
        </FlowListPermissionMenuItem>
        <FlowListPermissionMenuItem permission={permissions.templateExport} onClick={() => openDialog('exportTemplate')}>
          <BookmarksOutlinedIcon />
          {translate(flowiseI18nKeys.actions.saveAsTemplate)}
        </FlowListPermissionMenuItem>
        <Divider sx={{ my: 0.5 }} />
        <FlowListPermissionMenuItem permission={permissions.config} onClick={() => openDialog('starterPrompts')}>
          <PictureInPictureAltIcon />
          {translate(flowiseI18nKeys.detail.starterPrompts)}
        </FlowListPermissionMenuItem>
        <FlowListPermissionMenuItem permission={permissions.config} onClick={() => openDialog('chatFeedback')}>
          <ThumbsUpDownOutlinedIcon />
          {translate(flowiseI18nKeys.configuration.chatFeedback)}
        </FlowListPermissionMenuItem>
        <FlowListPermissionMenuItem permission={permissions.domains} onClick={() => openDialog('allowedDomains')}>
          <VpnLockOutlinedIcon />
          {translate(flowiseI18nKeys.configuration.allowedDomains)}
        </FlowListPermissionMenuItem>
        <FlowListPermissionMenuItem permission={permissions.config} onClick={() => openDialog('speechToText')}>
          <MicNoneOutlinedIcon />
          {translate(flowiseI18nKeys.configuration.speechToText)}
        </FlowListPermissionMenuItem>
        <FlowListPermissionMenuItem permission={permissions.update} onClick={() => openDialog('category')}>
          <CategoryIcon />
          {translate(flowiseI18nKeys.actions.updateCategory)}
        </FlowListPermissionMenuItem>
        <Divider sx={{ my: 0.5 }} />
        <FlowListPermissionMenuItem danger permission={permissions.delete} onClick={() => selectMenuAction(() => onDelete(item))}>
          <DeleteIcon />
          {translate(flowiseI18nKeys.actions.delete)}
        </FlowListPermissionMenuItem>
      </StyledMenu>
      <SaveChatflowDialog
        initialName={item.name}
        open={dialog === 'rename'}
        saving={saving}
        title={translate(flowiseI18nKeys.actions.rename)}
        onClose={closeDialog}
        onConfirm={(name) => void savePatch({ name: name.trim() || item.name }, 'update')}
      />
      <TagDialog
        initialCategory={item.category}
        open={dialog === 'category'}
        saving={saving}
        onClose={closeDialog}
        onSubmit={(categories) => void savePatch({ category: categories.join(';') || null }, 'update')}
      />
      <StarterPromptsDialog
        initialPrompts={readStarterPromptRows(chatbotConfig.starterPrompts)}
        open={dialog === 'starterPrompts'}
        saving={saving}
        title={`${translate(flowiseI18nKeys.detail.starterPrompts)} - ${item.name}`}
        onClose={closeDialog}
        onConfirm={(prompts) => void savePatch({
          chatbotConfig: stringifyJsonRecord({
            ...chatbotConfig,
            starterPrompts: writeStarterPromptRows(prompts)
          })
        }, 'config')}
      />
      <ChatFeedbackDialog
        enabled={Boolean(readRecord(chatbotConfig.chatFeedback).status)}
        open={dialog === 'chatFeedback'}
        saving={saving}
        title={`${translate(flowiseI18nKeys.configuration.chatFeedback)} - ${item.name}`}
        onClose={closeDialog}
        onConfirm={(enabled) => {
          const chatFeedback = readRecord(chatbotConfig.chatFeedback);
          void savePatch({
            chatbotConfig: stringifyJsonRecord({
              ...chatbotConfig,
              chatFeedback: {
                ...chatFeedback,
                status: enabled
              }
            })
          }, 'config');
        }}
      />
      <AllowedDomainsDialog
        domains={readStringArray(chatbotConfig.allowedOrigins)}
        errorMessage={typeof chatbotConfig.allowedOriginsError === 'string' ? chatbotConfig.allowedOriginsError : ''}
        open={dialog === 'allowedDomains'}
        saving={saving}
        title={`${translate(flowiseI18nKeys.configuration.allowedDomains)} - ${item.name}`}
        onClose={closeDialog}
        onConfirm={(payload) => void savePatch({
          chatbotConfig: stringifyJsonRecord({
            ...chatbotConfig,
            allowedOrigins: payload.domains,
            allowedOriginsError: payload.errorMessage
          })
        }, 'domains')}
      />
      <SpeechToTextDialog
        credentials={credentials}
        initialJson={formatJson(item.speechToText || '{}')}
        open={dialog === 'speechToText'}
        saving={saving}
        title={`${translate(flowiseI18nKeys.configuration.speechToText)} - ${item.name}`}
        onClose={closeDialog}
        onConfirm={(json) => void savePatch({ speechToText: stringifyJsonRecord(safeParseRecord(json || '{}')) }, 'config')}
      />
      <ExportAsTemplateDialog
        item={item}
        open={dialog === 'exportTemplate'}
        saving={saving}
        onClose={closeDialog}
        onConfirm={(payload) => {
          void onExportTemplate(item, payload);
          closeDialog();
        }}
      />
    </div>
  );
}

function safeParseRecord(jsonValue?: string | null): Record<string, unknown> {
  if (!jsonValue) {
    return {};
  }

  try {
    const parsed = JSON.parse(jsonValue) as unknown;
    return readRecord(parsed);
  } catch {
    return {};
  }
}

function readRecord(value: unknown): Record<string, unknown> {
  return value && typeof value === 'object' && !Array.isArray(value) ? value as Record<string, unknown> : {};
}

function stringifyJsonRecord(value: Record<string, unknown>) {
  return JSON.stringify(value);
}

function formatJson(jsonValue: string) {
  return JSON.stringify(safeParseRecord(jsonValue), null, 2);
}

function readStringArray(value: unknown): string[] {
  if (Array.isArray(value)) {
    return value.map((item) => String(item).trim()).filter(Boolean);
  }

  if (typeof value === 'string') {
    return value.split(/[\n,;]/).map((item) => item.trim()).filter(Boolean);
  }

  return [];
}

function readStarterPromptRows(value: unknown): string[] {
  if (Array.isArray(value)) {
    return value.map(readPromptValue).filter(Boolean);
  }

  if (value && typeof value === 'object') {
    return Object.values(value as Record<string, unknown>).map(readPromptValue).filter(Boolean);
  }

  return typeof value === 'string' ? value.split(/\r?\n/).map((item) => item.trim()).filter(Boolean) : [];
}

function readPromptValue(value: unknown): string {
  if (typeof value === 'string') {
    return value.trim();
  }

  if (value && typeof value === 'object' && typeof (value as { prompt?: unknown }).prompt === 'string') {
    return (value as { prompt: string }).prompt.trim();
  }

  return '';
}

function writeStarterPromptRows(prompts: string[]): Record<string, { prompt: string }> {
  return prompts
    .map((prompt) => prompt.trim())
    .filter(Boolean)
    .reduce<Record<string, { prompt: string }>>((acc, prompt, index) => {
      acc[index] = { prompt };
      return acc;
    }, {});
}

function FlowListPermissionMenuItem({
  children,
  danger = false,
  permission,
  sx,
  ...menuItemProps
}: MenuItemProps & {
  danger?: boolean;
  permission: string | string[];
}) {
  const { hasPermission } = usePermission(permission);

  if (!hasPermission) {
    return null;
  }

  return (
    <MenuItem
      {...menuItemProps}
      disableRipple
      sx={{
        ...(danger ? { color: 'error.main' } : null),
        ...sx
      }}
    >
      {children}
    </MenuItem>
  );
}

const StyledMenu = styled((props: MenuProps) => (
  <Menu
    anchorOrigin={{
      horizontal: 'right',
      vertical: 'bottom'
    }}
    elevation={0}
    transformOrigin={{
      horizontal: 'right',
      vertical: 'top'
    }}
    {...props}
  />
))(({ theme }) => ({
  '& .MuiPaper-root': {
    borderRadius: 6,
    boxShadow:
      'rgb(255, 255, 255) 0px 0px 0px 0px, rgba(0, 0, 0, 0.05) 0px 0px 0px 1px, rgba(0, 0, 0, 0.1) 0px 10px 15px -3px, rgba(0, 0, 0, 0.05) 0px 4px 6px -2px',
    marginTop: theme.spacing(1),
    minWidth: 180,
    '& .MuiMenu-list': {
      padding: '4px 0'
    },
    '& .MuiMenuItem-root': {
      '& .MuiSvgIcon-root': {
        color: theme.palette.text.secondary,
        fontSize: 18,
        marginRight: theme.spacing(1.5)
      },
      '&:active': {
        backgroundColor: alpha(theme.palette.primary.main, theme.palette.action.selectedOpacity)
      }
    }
  }
}));
