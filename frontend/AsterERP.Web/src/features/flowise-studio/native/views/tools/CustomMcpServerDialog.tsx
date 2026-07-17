import AddIcon from '@mui/icons-material/Add';
import ChevronRightIcon from '@mui/icons-material/ChevronRight';
import ClearIcon from '@mui/icons-material/Clear';
import DeleteIcon from '@mui/icons-material/Delete';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import {
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControl,
  IconButton,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  TextField
} from '@mui/material';
import { useEffect, useMemo, useState } from 'react';

import { useI18n } from '../../../../../core/i18n/I18nProvider';
import { useThemeStore } from '../../../../../core/state';
import { PermissionButton } from '../../../../../shared/auth/PermissionButton';
import { flowisePermissions } from '../../../../../shared/auth/permissionCodes';
import { flowiseI18nKeys } from '../../../i18n/flowiseI18nKeys';
import { translateFlowiseStatus } from '../../../i18n/flowiseTranslate';
import type { FlowiseCustomMcpServerDto, FlowiseCustomMcpServerToolDto, FlowiseCustomMcpServerUpsertRequest } from '../../../types/customMcpServer.types';

const maskedValue = '************';
const legacyMaskedValue = '******';

interface HeaderDraft {
  key: string;
  value: string;
}

interface CustomMcpServerDialogProps {
  authorizing?: boolean;
  mode: 'add' | 'edit';
  open: boolean;
  saving?: boolean;
  server?: FlowiseCustomMcpServerDto | null;
  tools: FlowiseCustomMcpServerToolDto[];
  toolsLoading?: boolean;
  onAuthorize: (server: FlowiseCustomMcpServerDto) => void;
  onClose: () => void;
  onDelete: (server: FlowiseCustomMcpServerDto) => void;
  onSave: (request: FlowiseCustomMcpServerUpsertRequest, options: { reconnect: boolean }) => void;
}

interface FormState {
  authType: string;
  color: string;
  headers: HeaderDraft[];
  iconSrc: string;
  name: string;
  serverUrl: string;
}

const createEmptyForm = (): FormState => ({
  authType: 'none',
  color: '#4f46e5',
  headers: [{ key: '', value: '' }],
  iconSrc: '',
  name: '',
  serverUrl: ''
});

export function CustomMcpServerDialog({
  authorizing,
  mode,
  open,
  saving,
  server,
  tools,
  toolsLoading,
  onAuthorize,
  onClose,
  onDelete,
  onSave
}: CustomMcpServerDialogProps) {
  const { translate } = useI18n();
  const [editing, setEditing] = useState(mode === 'add');
  const [form, setForm] = useState<FormState>(createEmptyForm);
  const [serverUrlError, setServerUrlError] = useState('');
  const [toolSearch, setToolSearch] = useState('');
  const [expandedTools, setExpandedTools] = useState<Set<string>>(() => new Set());
  const [toolsSectionOpen, setToolsSectionOpen] = useState(true);
  const [headerError, setHeaderError] = useState('');
  const theme = useThemeStore((state) => state.theme);
  const iconTheme = theme === 'dark' ? 'dark' : 'light';

  useEffect(() => {
    if (!open) {
      return;
    }

    if (mode === 'add') {
      setForm(createEmptyForm());
      setEditing(true);
    } else if (server) {
      setForm({
        authType: server.authType || 'none',
        color: server.color || '#4f46e5',
        headers: parseHeaders(server.authConfigJson),
        iconSrc: server.iconSrc || '',
        name: server.name,
        serverUrl: server.serverUrl
      });
      setEditing(false);
    }

    setServerUrlError('');
    setHeaderError('');
    setToolSearch('');
    setExpandedTools(new Set());
    setToolsSectionOpen(true);
  }, [mode, open, server]);

  const filteredTools = useMemo(() => {
    const normalizedKeyword = toolSearch.trim().toLowerCase();
    if (!normalizedKeyword) {
      return tools;
    }

    return tools.filter((tool) => {
      const schema = parseToolSchema(tool.inputSchemaJson);
      const annotations = parseAnnotations(tool.annotationsJson);
      return [tool.name, tool.description, schema.title, annotations.title]
        .filter(Boolean)
        .some((value) => String(value).toLowerCase().includes(normalizedKeyword));
    });
  }, [toolSearch, tools]);

  const validateServerUrl = () => {
    if (!form.serverUrl.trim()) {
      setServerUrlError(translate(flowiseI18nKeys.customMcp.serverUrlRequired));
      return false;
    }

    try {
      const parsed = new URL(form.serverUrl);
      if (parsed.protocol !== 'http:' && parsed.protocol !== 'https:') {
        setServerUrlError(translate(flowiseI18nKeys.customMcp.serverUrlProtocolError));
        return false;
      }
    } catch {
      setServerUrlError(translate(flowiseI18nKeys.customMcp.serverUrlInvalid));
      return false;
    }

    setServerUrlError('');
    return true;
  };

  const submit = () => {
    if (!validateServerUrl()) {
      return;
    }
    const headerValidation = validateHeaders(form.headers, translate);
    if (headerValidation) {
      setHeaderError(headerValidation);
      return;
    }

    onSave(buildRequest(form), { reconnect: true });
  };

  const title = mode === 'add'
    ? translate(flowiseI18nKeys.customMcp.createTitle)
    : server?.name || translate(flowiseI18nKeys.customMcp.title);

  return (
    <Dialog fullWidth maxWidth="md" open={open} onClose={onClose}>
      <DialogTitle>{title}</DialogTitle>
      <DialogContent>
        <div className="flowise-custom-mcp-dialog">
          <div className="flowise-custom-mcp-dialog__titlebar">
            {mode === 'edit' && server ? <span className={`flowise-status flowise-status--${server.status.toLowerCase()}`}>{translateFlowiseStatus(server.status, translate)}</span> : null}
          </div>
          {editing ? (
            <CustomMcpServerEditForm
              form={form}
              headerError={headerError}
              serverUrlError={serverUrlError}
              onBlurServerUrl={validateServerUrl}
              onChange={(nextForm) => {
                setHeaderError('');
                setForm(nextForm);
              }}
            />
          ) : (
            <CustomMcpServerReadOnly server={server} />
          )}
          {mode === 'edit' && !editing ? (
            <DiscoveredToolsSection
              expandedTools={expandedTools}
              iconTheme={iconTheme}
              loading={toolsLoading}
              search={toolSearch}
              sectionOpen={toolsSectionOpen}
              tools={filteredTools}
              total={tools.length}
              onCollapseAll={() => setExpandedTools(new Set())}
              onExpandAll={() => setExpandedTools(new Set(filteredTools.map((tool) => tool.name)))}
              onSearch={setToolSearch}
              onToggle={(toolName) => setExpandedTools((current) => {
                const next = new Set(current);
                if (next.has(toolName)) {
                  next.delete(toolName);
                } else {
                  next.add(toolName);
                }
                return next;
              })}
              onToggleSection={() => setToolsSectionOpen((current) => !current)}
            />
          ) : null}
        </div>
      </DialogContent>
      <DialogActions>
        <CustomMcpDialogActions
          authorizing={authorizing}
          editing={editing}
          mode={mode}
          saving={saving}
          server={server}
          onAuthorize={onAuthorize}
          onCancelEdit={() => server ? setForm({
            authType: server.authType || 'none',
            color: server.color || '#4f46e5',
            headers: parseHeaders(server.authConfigJson),
            iconSrc: server.iconSrc || '',
            name: server.name,
            serverUrl: server.serverUrl
          }) : undefined}
          onClose={onClose}
          onDelete={onDelete}
          onEdit={() => setEditing(true)}
          onSave={submit}
          onStopEdit={() => setEditing(false)}
        />
      </DialogActions>
    </Dialog>
  );
}

function CustomMcpDialogActions({
  authorizing,
  editing,
  mode,
  saving,
  server,
  onAuthorize,
  onCancelEdit,
  onClose,
  onDelete,
  onEdit,
  onSave,
  onStopEdit
}: {
  authorizing?: boolean;
  editing: boolean;
  mode: 'add' | 'edit';
  saving?: boolean;
  server?: FlowiseCustomMcpServerDto | null;
  onAuthorize: (server: FlowiseCustomMcpServerDto) => void;
  onCancelEdit: () => void;
  onClose: () => void;
  onDelete: (server: FlowiseCustomMcpServerDto) => void;
  onEdit: () => void;
  onSave: () => void;
  onStopEdit: () => void;
}) {
  const { translate } = useI18n();

  if (mode === 'add' || editing) {
    return (
      <>
        <Button
          variant="outlined"
          onClick={() => {
            if (mode === 'edit') {
              onCancelEdit();
              onStopEdit();
            } else {
              onClose();
            }
          }}
        >
          {translate(flowiseI18nKeys.common.cancel)}
        </Button>
        <PermissionButton className="btn-primary" code={mode === 'add' ? flowisePermissions.toolsCreate : flowisePermissions.toolsUpdate} disabled={saving || authorizing} iconStart={false} type="button" onClick={onSave}>
          {mode === 'add' ? translate(flowiseI18nKeys.customMcp.addAndConnect) : translate(flowiseI18nKeys.customMcp.saveAndReconnect)}
        </PermissionButton>
      </>
    );
  }

  if (!server) {
    return <Button variant="outlined" onClick={onClose}>{translate(flowiseI18nKeys.actions.close)}</Button>;
  }

  return (
    <>
      <PermissionButton className="btn-ghost danger" code={flowisePermissions.toolsDelete} iconStart={false} onClick={() => onDelete(server)} type="button">
        {translate(flowiseI18nKeys.actions.delete)}
      </PermissionButton>
      <PermissionButton className="btn-secondary" code={[flowisePermissions.toolsUpdate, flowisePermissions.toolsCreate]} disabled={authorizing} iconStart={false} onClick={() => onAuthorize(server)} type="button">
        {translate(flowiseI18nKeys.customMcp.reconnect)}
      </PermissionButton>
      <PermissionButton className="btn-primary" code={flowisePermissions.toolsUpdate} iconStart={false} onClick={onEdit} type="button">
        {translate(flowiseI18nKeys.actions.edit)}
      </PermissionButton>
    </>
  );
}

function CustomMcpServerEditForm({
  form,
  headerError,
  serverUrlError,
  onBlurServerUrl,
  onChange
}: {
  form: FormState;
  headerError: string;
  serverUrlError: string;
  onBlurServerUrl: () => void;
  onChange: (form: FormState) => void;
}) {
  const { translate } = useI18n();
  const customHeaders = form.authType === 'headers' || form.authType === 'custom-headers';

  return (
    <Stack spacing={2} sx={{ pt: 1 }}>
      <TextField
        fullWidth
        label={translate(flowiseI18nKeys.fields.name)}
        placeholder={translate(flowiseI18nKeys.customMcp.namePlaceholder)}
        slotProps={{ htmlInput: { maxLength: 40 } }}
        value={form.name}
        onChange={(event) => onChange({ ...form, name: event.target.value })}
      />
      <TextField
        fullWidth
        error={Boolean(serverUrlError)}
        helperText={serverUrlError || undefined}
        label={translate(flowiseI18nKeys.customMcp.serverUrl)}
        placeholder={translate(flowiseI18nKeys.customMcp.serverUrlPlaceholder)}
        value={form.serverUrl}
        onBlur={onBlurServerUrl}
        onChange={(event) => onChange({ ...form, serverUrl: event.target.value })}
      />
      <TextField
        fullWidth
        label={translate(flowiseI18nKeys.customMcp.iconSrc)}
        placeholder={translate(flowiseI18nKeys.customMcp.iconSrcPlaceholder)}
        value={form.iconSrc}
        onChange={(event) => onChange({ ...form, iconSrc: event.target.value })}
      />
      <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
        <FormControl fullWidth>
          <InputLabel id="flowise-custom-mcp-auth-type-label">{translate(flowiseI18nKeys.customMcp.authType)}</InputLabel>
          <Select
            label={translate(flowiseI18nKeys.customMcp.authType)}
            labelId="flowise-custom-mcp-auth-type-label"
            value={form.authType}
            onChange={(event) => onChange({ ...form, authType: event.target.value })}
          >
            <MenuItem value="none">{translate(flowiseI18nKeys.customMcp.authTypeNone)}</MenuItem>
            <MenuItem value="headers">{translate(flowiseI18nKeys.customMcp.authTypeHeaders)}</MenuItem>
          </Select>
        </FormControl>
        <TextField
          fullWidth
          label={translate(flowiseI18nKeys.customMcp.color)}
          type="color"
          value={form.color || '#4f46e5'}
          onChange={(event) => onChange({ ...form, color: event.target.value })}
        />
      </Stack>
      {customHeaders ? (
        <>
          <HeaderEditor headers={form.headers} onChange={(headers) => onChange({ ...form, headers })} />
          {headerError ? <span className="flowise-native-form-error">{headerError}</span> : null}
        </>
      ) : null}
    </Stack>
  );
}

function HeaderEditor({ headers, onChange }: { headers: HeaderDraft[]; onChange: (headers: HeaderDraft[]) => void }) {
  const { translate } = useI18n();
  const safeHeaders = headers.length > 0 ? headers : [{ key: '', value: '' }];

  return (
    <div className="flowise-custom-mcp-headers">
      <div className="flowise-custom-mcp-headers__head">
        <span>{translate(flowiseI18nKeys.customMcp.headerKey)}</span>
        <span>{translate(flowiseI18nKeys.customMcp.headerValue)}</span>
      </div>
      {safeHeaders.map((header, index) => (
        <div className="flowise-custom-mcp-headers__row" key={`${index}-${header.key}`}>
          <TextField
            fullWidth
            placeholder={translate(flowiseI18nKeys.customMcp.headerKeyPlaceholder)}
            size="small"
            value={header.key}
            onChange={(event) => onChange(safeHeaders.map((item, itemIndex) => itemIndex === index ? { ...item, key: event.target.value } : item))}
          />
          <TextField
            fullWidth
            placeholder={translate(flowiseI18nKeys.customMcp.headerValuePlaceholder)}
            size="small"
            value={header.value}
            onChange={(event) => onChange(safeHeaders.map((item, itemIndex) => itemIndex === index ? { ...item, value: event.target.value } : item))}
          />
          {safeHeaders.length > 1 ? (
            <IconButton color="error" size="small" title={translate(flowiseI18nKeys.actions.remove)} onClick={() => onChange(safeHeaders.filter((_, itemIndex) => itemIndex !== index))}>
              <DeleteIcon fontSize="small" />
            </IconButton>
          ) : null}
        </div>
      ))}
      <Button startIcon={<AddIcon />} variant="outlined" onClick={() => onChange([...safeHeaders, { key: '', value: '' }])}>
        {translate(flowiseI18nKeys.customMcp.addHeader)}
      </Button>
    </div>
  );
}

function CustomMcpServerReadOnly({ server }: { server?: FlowiseCustomMcpServerDto | null }) {
  const { translate } = useI18n();
  if (!server) {
    return null;
  }

  const headers = parseHeaders(server.authConfigJson).filter((header) => header.key);

  return (
    <div className="flowise-custom-mcp-readonly">
      <ReadOnlyField label={translate(flowiseI18nKeys.fields.name)} value={server.name} />
      <ReadOnlyField label={translate(flowiseI18nKeys.customMcp.serverUrl)} value={server.serverUrl} />
      {server.iconSrc ? <ReadOnlyField label={translate(flowiseI18nKeys.customMcp.iconSrc)} value={server.iconSrc} /> : null}
      <ReadOnlyField label={translate(flowiseI18nKeys.customMcp.authType)} value={server.authType === 'none' ? translate(flowiseI18nKeys.customMcp.authTypeNone) : translate(flowiseI18nKeys.customMcp.authTypeHeaders)} />
      {headers.length > 0 ? (
        <div className="flowise-custom-mcp-readonly__headers">
          <span>{translate(flowiseI18nKeys.customMcp.headers)}</span>
          {headers.map((header) => <code key={header.key}>{header.key}: {maskedValue}</code>)}
        </div>
      ) : null}
      {server.errorMessage ? <div className="flowise-native-form-error">{server.errorMessage}</div> : null}
    </div>
  );
}

function ReadOnlyField({ label, value }: { label: string; value: string }) {
  return (
    <div className="flowise-custom-mcp-readonly__field">
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}

function DiscoveredToolsSection({
  expandedTools,
  iconTheme,
  loading,
  search,
  sectionOpen,
  tools,
  total,
  onCollapseAll,
  onExpandAll,
  onSearch,
  onToggle,
  onToggleSection
}: {
  expandedTools: Set<string>;
  iconTheme: 'dark' | 'light';
  loading?: boolean;
  search: string;
  sectionOpen: boolean;
  tools: FlowiseCustomMcpServerToolDto[];
  total: number;
  onCollapseAll: () => void;
  onExpandAll: () => void;
  onSearch: (search: string) => void;
  onToggle: (toolName: string) => void;
  onToggleSection: () => void;
}) {
  const { translate } = useI18n();
  const hasTools = tools.length > 0;

  return (
    <section className="flowise-custom-mcp-discovered">
      <header>
        <Button className="flowise-custom-mcp-discovered__summary" startIcon={sectionOpen ? <ExpandMoreIcon /> : <ChevronRightIcon />} onClick={onToggleSection}>
          <span>{translate(flowiseI18nKeys.customMcp.discoveredTools)}</span>
          <strong>{search ? `${tools.length}/${total}` : total}</strong>
        </Button>
        {sectionOpen ? (
          <div className="flowise-custom-mcp-discovered__actions">
            {hasTools ? (
              <>
                <Button variant="text" onClick={onExpandAll}>{translate(flowiseI18nKeys.customMcp.expandAll)}</Button>
                <Button variant="text" onClick={onCollapseAll}>{translate(flowiseI18nKeys.customMcp.collapseAll)}</Button>
              </>
            ) : null}
            {total > 5 ? (
              <div className="flowise-custom-mcp-discovered__search">
                <TextField
                  placeholder={translate(flowiseI18nKeys.customMcp.toolSearchPlaceholder)}
                  size="small"
                  value={search}
                  onChange={(event) => onSearch(event.target.value)}
                />
                {search ? (
                  <IconButton size="small" title={translate(flowiseI18nKeys.actions.clear)} onClick={() => onSearch('')}>
                    <ClearIcon fontSize="small" />
                  </IconButton>
                ) : null}
              </div>
            ) : null}
          </div>
        ) : null}
      </header>
      {sectionOpen && loading ? <div className="flowise-native-loading">{translate(flowiseI18nKeys.actions.refresh)}</div> : null}
      {sectionOpen && !loading && tools.length === 0 ? <div className="flowise-native-empty">{translate(flowiseI18nKeys.customMcp.noTools)}</div> : null}
      {sectionOpen && !loading && tools.length > 0 ? (
        <div className="flowise-custom-mcp-tools">
          {tools.map((tool) => (
            <DiscoveredToolRow
              expanded={expandedTools.has(tool.name)}
              iconTheme={iconTheme}
              key={tool.name}
              tool={tool}
              onToggle={() => onToggle(tool.name)}
            />
          ))}
        </div>
      ) : null}
    </section>
  );
}

function DiscoveredToolRow({ expanded, iconTheme, tool, onToggle }: { expanded: boolean; iconTheme: 'dark' | 'light'; tool: FlowiseCustomMcpServerToolDto; onToggle: () => void }) {
  const { translate } = useI18n();
  const schema = parseToolSchema(tool.inputSchemaJson);
  const annotations = parseAnnotations(tool.annotationsJson);
  const icon = pickToolIcon(tool.iconsJson, iconTheme);
  const properties = schema.properties ?? {};
  const required = new Set(Array.isArray(schema.required) ? schema.required : []);
  const propertyNames = Object.keys(properties);
  const toolTitle = annotations.title || schema.title || tool.description || '-';

  return (
    <article className={expanded ? 'flowise-custom-mcp-tool expanded' : 'flowise-custom-mcp-tool'}>
      <Button className="flowise-custom-mcp-tool__button" startIcon={expanded ? <ExpandMoreIcon /> : <ChevronRightIcon />} onClick={onToggle}>
        {icon ? <img alt="" className="flowise-custom-mcp-tool__icon" src={icon} /> : <span className="flowise-custom-mcp-tool__icon-placeholder" />}
        <strong>{tool.name}</strong>
        <em>{toolTitle}</em>
        <ToolAnnotationChips annotations={annotations} compact />
        <small title={translate(flowiseI18nKeys.customMcp.parameterCountTooltip).replace('{count}', String(propertyNames.length))}>
          {translate(flowiseI18nKeys.customMcp.parameterCount).replace('{count}', String(propertyNames.length))}
        </small>
      </Button>
      {expanded ? (
        <div className="flowise-custom-mcp-tool__body">
          {tool.description ? <p>{tool.description}</p> : null}
          {propertyNames.length > 0 ? (
            <div className="flowise-custom-mcp-tool__params">
              <span>{translate(flowiseI18nKeys.customMcp.parameters)}</span>
              {propertyNames.map((propertyName) => {
                const property = properties[propertyName] ?? {};
                return (
                  <div className="flowise-custom-mcp-tool__param" key={propertyName}>
                    <code>{propertyName}</code>
                    <b className={required.has(propertyName) ? 'flowise-custom-mcp-tool__required required' : 'flowise-custom-mcp-tool__required optional'}>
                      {required.has(propertyName) ? translate(flowiseI18nKeys.customMcp.required) : translate(flowiseI18nKeys.customMcp.optional)}
                    </b>
                    {property.type ? <TypeChip type={String(property.type)} /> : null}
                    {Array.isArray(property.enum) && property.enum.length > 0 ? (
                      <div className="flowise-custom-mcp-tool__enum">
                        <span>{translate(flowiseI18nKeys.customMcp.enumValues)}</span>
                        {property.enum.map((value) => <i key={String(value)}>{String(value)}</i>)}
                      </div>
                    ) : null}
                    {property.default !== undefined ? <small>{translate(flowiseI18nKeys.customMcp.defaultValue)}: {JSON.stringify(property.default)}</small> : null}
                    {property.description ? <p>{String(property.description)}</p> : null}
                  </div>
                );
              })}
            </div>
          ) : null}
        </div>
      ) : null}
    </article>
  );
}

function ToolAnnotationChips({ annotations, compact }: { annotations: ToolAnnotations; compact?: boolean }) {
  const { translate } = useI18n();
  const chips = [
    annotations.readOnlyHint ? { className: 'safe', icon: 'i', label: translate(flowiseI18nKeys.customMcp.readOnlyHint), tooltip: translate(flowiseI18nKeys.customMcp.readOnlyHintTooltip) } : null,
    annotations.destructiveHint ? { className: 'danger', icon: '!', label: translate(flowiseI18nKeys.customMcp.destructiveHint), tooltip: translate(flowiseI18nKeys.customMcp.destructiveHintTooltip) } : null,
    annotations.openWorldHint ? { className: 'external', icon: '↗', label: translate(flowiseI18nKeys.customMcp.openWorldHint), tooltip: translate(flowiseI18nKeys.customMcp.openWorldHintTooltip) } : null
  ].filter(Boolean) as Array<{ className: string; icon: string; label: string; tooltip: string }>;

  if (chips.length === 0) {
    return null;
  }

  return (
    <div className={compact ? 'flowise-custom-mcp-tool__chips compact' : 'flowise-custom-mcp-tool__chips'}>
      {chips.map((chip) => (
        <span className={`flowise-custom-mcp-tool__chip ${chip.className}`} key={chip.label} title={chip.tooltip}>
          <i aria-hidden="true">{chip.icon}</i>
          {chip.label}
        </span>
      ))}
    </div>
  );
}

function TypeChip({ type }: { type: string }) {
  return <small className={`flowise-custom-mcp-tool__type type-${normalizeTypeClass(type)}`}>{type}</small>;
}

function buildRequest(form: FormState): FlowiseCustomMcpServerUpsertRequest {
  return {
    authConfigJson: form.authType === 'none' ? null : JSON.stringify({
      headers: Object.fromEntries(form.headers.filter((header) => header.key.trim()).map((header) => [header.key.trim(), header.value]))
    }),
    authType: form.authType,
    color: form.color || null,
    iconSrc: form.iconSrc || null,
    name: form.name.trim(),
    serverUrl: form.serverUrl.trim(),
    status: 'Enabled'
  };
}

function validateHeaders(headers: HeaderDraft[], translate: (key: string) => string) {
  for (const header of headers) {
    if (header.value && header.value !== maskedValue && header.value.includes(maskedValue)) {
      return translate(flowiseI18nKeys.customMcp.partialMaskError).replace('{key}', header.key || '?');
    }
  }

  return '';
}

function parseHeaders(authConfigJson?: string | null): HeaderDraft[] {
  if (!authConfigJson) {
    return [{ key: '', value: '' }];
  }

  try {
    const parsed = JSON.parse(authConfigJson) as { headers?: Record<string, string> };
    const headers = Object.entries(parsed.headers ?? {}).map(([key, value]) => ({ key, value: normalizeMaskedValue(value) }));
    return headers.length > 0 ? headers : [{ key: '', value: '' }];
  } catch {
    return [{ key: '', value: '' }];
  }
}

function normalizeMaskedValue(value?: string | null) {
  if (!value) {
    return '';
  }

  return value === legacyMaskedValue || value === maskedValue ? maskedValue : value;
}

interface ToolAnnotations {
  destructiveHint?: boolean;
  openWorldHint?: boolean;
  readOnlyHint?: boolean;
  title?: string;
}

function parseToolSchema(inputSchemaJson: string): { properties?: Record<string, { default?: unknown; description?: unknown; enum?: unknown[]; type?: unknown }>; required?: string[]; title?: string } {
  if (!inputSchemaJson) {
    return {};
  }

  try {
    return JSON.parse(inputSchemaJson);
  } catch {
    return {};
  }
}

function parseAnnotations(annotationsJson: string): ToolAnnotations {
  if (!annotationsJson) {
    return {};
  }

  try {
    return JSON.parse(annotationsJson);
  } catch {
    return {};
  }
}

function normalizeTypeClass(type: string) {
  const normalized = type.trim().toLowerCase();
  return normalized === 'string' || normalized === 'number' || normalized === 'integer' || normalized === 'boolean' || normalized === 'array' || normalized === 'object'
    ? normalized
    : 'other';
}

function pickToolIcon(iconsJson: string, iconTheme: 'dark' | 'light') {
  if (!iconsJson) {
    return '';
  }

  try {
    const icons = JSON.parse(iconsJson) as Array<{ src?: string; theme?: string }>;
    return icons.find((icon) => icon.theme === iconTheme)?.src
      || icons.find((icon) => !icon.theme)?.src
      || icons.find((icon) => icon.theme === 'light')?.src
      || icons[0]?.src
      || '';
  } catch {
    return '';
  }
}
