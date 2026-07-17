import { useEffect, useMemo, useState, type ReactNode } from 'react';

import { translateCurrentLocale, useI18n } from '../../../../core/i18n/I18nProvider';
import { PermissionButton } from '../../../../shared/auth/PermissionButton';
import { AppIcon } from '../../../../shared/icons/AppIcon';
import type {
  KnowledgeGraphEdgeFormValue,
  KnowledgeGraphModalKey,
  KnowledgeGraphNodeFormValue,
  KnowledgeGraphOption
} from '../types';

interface KnowledgeGraphEntityFormsProps {
  activeModal: KnowledgeGraphModalKey;
  edgeFormValue: KnowledgeGraphEdgeFormValue;
  nodeFormValue: KnowledgeGraphNodeFormValue;
  nodeOptions: KnowledgeGraphOption[];
  savingEdge: boolean;
  savingNode: boolean;
  onClose: () => void;
  onSaveEdge: (value: KnowledgeGraphEdgeFormValue) => void;
  onSaveNode: (value: KnowledgeGraphNodeFormValue) => void;
}

export function KnowledgeGraphEntityForms({
  activeModal,
  edgeFormValue,
  nodeFormValue,
  nodeOptions,
  onClose,
  onSaveEdge,
  onSaveNode,
  savingEdge,
  savingNode
}: KnowledgeGraphEntityFormsProps) {
  return (
    <>
      <KnowledgeGraphNodeForm
        open={activeModal === 'nodeForm'}
        saving={savingNode}
        value={nodeFormValue}
        onClose={onClose}
        onSave={onSaveNode}
      />
      <KnowledgeGraphEdgeForm
        nodeOptions={nodeOptions}
        open={activeModal === 'edgeForm'}
        saving={savingEdge}
        value={edgeFormValue}
        onClose={onClose}
        onSave={onSaveEdge}
      />
    </>
  );
}

function KnowledgeGraphNodeForm({
  open,
  saving,
  value,
  onClose,
  onSave
}: {
  open: boolean;
  saving: boolean;
  value: KnowledgeGraphNodeFormValue;
  onClose: () => void;
  onSave: (value: KnowledgeGraphNodeFormValue) => void;
}) {
  const [draft, setDraft] = useState(value);
  useEffect(() => setDraft(value), [value]);
  const jsonError = useMemo(() => validateJsonObject(draft.metadataJson), [draft.metadataJson]);
  const { translate } = useI18n();

  return (
    <KnowledgeGraphDrawer
      disabled={saving || Boolean(jsonError)}
      open={open}
      saving={saving}
      title={draft.id ? translate('kg.entity.node.editTitle') : translate('kg.entity.node.createTitle')}
      onClose={onClose}
      onSave={() => onSave(draft)}
    >
      <div className="kg-form-grid">
        <TextField label={translate('kg.entity.node.field.nodeCode')} value={draft.nodeCode} onChange={(nodeCode) => setDraft((current) => ({ ...current, nodeCode }))} />
        <TextField label={translate('kg.entity.node.field.title')} value={draft.title} onChange={(title) => setDraft((current) => ({ ...current, title }))} />
        <TextField label={translate('kg.entity.node.field.nodeType')} value={draft.nodeType} onChange={(nodeType) => setDraft((current) => ({ ...current, nodeType }))} />
        <TextField label={translate('kg.entity.node.field.status')} value={draft.status} onChange={(status) => setDraft((current) => ({ ...current, status }))} />
        <TextField label={translate('kg.entity.node.field.sourceId')} value={draft.sourceId} onChange={(sourceId) => setDraft((current) => ({ ...current, sourceId }))} />
        <NumberField label={translate('kg.entity.node.field.weight')} value={draft.weight} onChange={(weight) => setDraft((current) => ({ ...current, weight }))} />
        <NumberField label={translate('kg.entity.node.field.positionX')} value={draft.positionX} onChange={(positionX) => setDraft((current) => ({ ...current, positionX }))} />
        <NumberField label={translate('kg.entity.node.field.positionY')} value={draft.positionY} onChange={(positionY) => setDraft((current) => ({ ...current, positionY }))} />
        <TextField label={translate('kg.entity.node.field.tags')} value={draft.tags} wide onChange={(tags) => setDraft((current) => ({ ...current, tags }))} />
        <TextArea label={translate('kg.entity.node.field.description')} value={draft.description} wide onChange={(description) => setDraft((current) => ({ ...current, description }))} />
        <TextArea label={translate('kg.entity.node.field.metadataJson')} value={draft.metadataJson} wide onChange={(metadataJson) => setDraft((current) => ({ ...current, metadataJson }))} />
      </div>
      {jsonError ? <p className="kg-form-error">{jsonError}</p> : null}
    </KnowledgeGraphDrawer>
  );
}

function KnowledgeGraphEdgeForm({
  nodeOptions,
  open,
  saving,
  value,
  onClose,
  onSave
}: {
  nodeOptions: KnowledgeGraphOption[];
  open: boolean;
  saving: boolean;
  value: KnowledgeGraphEdgeFormValue;
  onClose: () => void;
  onSave: (value: KnowledgeGraphEdgeFormValue) => void;
}) {
  const [draft, setDraft] = useState(value);
  useEffect(() => setDraft(value), [value]);
  const jsonError = useMemo(() => validateJsonObject(draft.metadataJson), [draft.metadataJson]);
  const sameEndpoint = draft.sourceNodeId !== '' && draft.sourceNodeId === draft.targetNodeId;
  const { translate } = useI18n();

  return (
    <KnowledgeGraphDrawer
      disabled={saving || Boolean(jsonError) || sameEndpoint}
      open={open}
      saving={saving}
      title={draft.id ? translate('kg.entity.edge.editTitle') : translate('kg.entity.edge.createTitle')}
      onClose={onClose}
      onSave={() => onSave(draft)}
    >
      <div className="kg-form-grid">
        <SelectField
          label={translate('kg.entity.edge.field.sourceNodeId')}
          options={nodeOptions}
          value={draft.sourceNodeId}
          onChange={(sourceNodeId) => setDraft((current) => ({ ...current, sourceNodeId }))}
        />
        <SelectField
          label={translate('kg.entity.edge.field.targetNodeId')}
          options={nodeOptions}
          value={draft.targetNodeId}
          onChange={(targetNodeId) => setDraft((current) => ({ ...current, targetNodeId }))}
        />
        <TextField label={translate('kg.entity.edge.field.relationCode')} value={draft.relationCode} onChange={(relationCode) => setDraft((current) => ({ ...current, relationCode }))} />
        <TextField label={translate('kg.entity.edge.field.title')} value={draft.title} onChange={(title) => setDraft((current) => ({ ...current, title }))} />
        <TextField label={translate('kg.entity.edge.field.relationType')} value={draft.relationType} onChange={(relationType) => setDraft((current) => ({ ...current, relationType }))} />
        <TextField label={translate('kg.entity.edge.field.status')} value={draft.status} onChange={(status) => setDraft((current) => ({ ...current, status }))} />
        <NumberField label={translate('kg.entity.edge.field.weight')} value={draft.weight} onChange={(weight) => setDraft((current) => ({ ...current, weight }))} />
        <TextArea label={translate('kg.entity.edge.field.description')} value={draft.description} wide onChange={(description) => setDraft((current) => ({ ...current, description }))} />
        <TextArea label={translate('kg.entity.edge.field.metadataJson')} value={draft.metadataJson} wide onChange={(metadataJson) => setDraft((current) => ({ ...current, metadataJson }))} />
      </div>
      {sameEndpoint ? <p className="kg-form-error">{translate('kg.entity.edge.sameEndpoint')}</p> : null}
      {jsonError ? <p className="kg-form-error">{jsonError}</p> : null}
    </KnowledgeGraphDrawer>
  );
}

function KnowledgeGraphDrawer({
  children,
  disabled,
  open,
  saving,
  title,
  onClose,
  onSave
}: {
  children: ReactNode;
  disabled: boolean;
  open: boolean;
  saving: boolean;
  title: string;
  onClose: () => void;
  onSave: () => void;
}) {
  const { translate } = useI18n();

  if (!open) {
    return null;
  }

  return (
    <div className="kg-drawer-layer" role="presentation" onClick={onClose}>
      <section aria-label={title} aria-modal="true" className="kg-drawer" role="dialog" onClick={(event) => event.stopPropagation()}>
        <header>
          <div>
            <AppIcon name="database" />
            <h2>{title}</h2>
          </div>
          <button className="kg-icon-button" type="button" onClick={onClose}>
            <AppIcon name="x" />
          </button>
        </header>
        <div className="kg-drawer-body">{children}</div>
        <footer>
          <button className="ghost-button" disabled={saving} type="button" onClick={onClose}>{translate('common.cancel')}</button>
          <PermissionButton className="primary-button" code="ai:knowledge:graph:edit" disabled={disabled} fallback="disable" type="button" onClick={onSave}>
            {saving ? translate('common.loading') : translate('common.save')}
          </PermissionButton>
        </footer>
      </section>
    </div>
  );
}

function TextField({ label, value, wide, onChange }: { label: string; value: string; wide?: boolean; onChange: (value: string) => void }) {
  return (
    <label className={['kg-field', wide ? 'kg-field--wide' : ''].filter(Boolean).join(' ')}>
      <span>{label}</span>
      <input value={value} onChange={(event) => onChange(event.target.value)} />
    </label>
  );
}

function NumberField({ label, value, onChange }: { label: string; value: number; onChange: (value: number) => void }) {
  return (
    <label className="kg-field">
      <span>{label}</span>
      <input type="number" value={value} onChange={(event) => onChange(Number(event.target.value))} />
    </label>
  );
}

function SelectField({
  label,
  options,
  value,
  onChange
}: {
  label: string;
  options: KnowledgeGraphOption[];
  value: string;
  onChange: (value: string) => void;
}) {
  return (
    <label className="kg-field">
      <span>{label}</span>
      <select value={value} onChange={(event) => onChange(event.target.value)}>
        <option value="">{translateCurrentLocale('common.select')}</option>
        {options.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
      </select>
    </label>
  );
}

function TextArea({ label, value, wide, onChange }: { label: string; value: string; wide?: boolean; onChange: (value: string) => void }) {
  return (
    <label className={['kg-field', wide ? 'kg-field--wide' : ''].filter(Boolean).join(' ')}>
      <span>{label}</span>
      <textarea value={value} onChange={(event) => onChange(event.target.value)} />
    </label>
  );
}

function validateJsonObject(value: string): string | null {
  try {
    const parsed = JSON.parse(value || '{}') as unknown;
    if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) {
      return translateCurrentLocale('kg.entity.json.mustBeObject');
    }
    return null;
  } catch {
    return translateCurrentLocale('kg.entity.json.invalid');
  }
}
