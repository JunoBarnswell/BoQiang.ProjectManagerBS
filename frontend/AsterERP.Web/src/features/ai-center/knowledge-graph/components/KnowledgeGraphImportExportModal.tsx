import { useState } from 'react';

import { useI18n } from '../../../../core/i18n/I18nProvider';
import { PermissionButton } from '../../../../shared/auth/PermissionButton';
import { AppIcon } from '../../../../shared/icons/AppIcon';
import type {
  KnowledgeGraphApiImportResult,
  KnowledgeGraphExchangeDraft,
  KnowledgeGraphModalKey
} from '../types';

interface KnowledgeGraphImportExportModalProps {
  activeModal: KnowledgeGraphModalKey;
  draft: KnowledgeGraphExchangeDraft;
  exporting: boolean;
  importing: boolean;
  onChange: (patch: Partial<KnowledgeGraphExchangeDraft>) => void;
  onClose: () => void;
  onExport: (format: 'json' | 'mermaid') => Promise<string>;
  onImport: (content: string, fileName: string) => Promise<KnowledgeGraphApiImportResult | null>;
}

export function KnowledgeGraphImportExportModal({
  activeModal,
  draft,
  exporting,
  importing,
  onChange,
  onClose,
  onExport,
  onImport
}: KnowledgeGraphImportExportModalProps) {
  const { translate } = useI18n();
  const [exportContent, setExportContent] = useState('');
  const [importSummary, setImportSummary] = useState('');

  if (activeModal !== 'exchange') {
    return null;
  }

  const handleExport = async () => {
    const content = await onExport(draft.format);
    setExportContent(content);
  };

  const handleImport = async () => {
    const result = await onImport(draft.importContent, draft.fileName);
    if (!result) {
      return;
    }

    const resultRecord = result as unknown as Record<string, unknown>;
    setImportSummary(
      [
        getResultPart(resultRecord, 'nodeCount', translate('kg.exchange.result.nodeCount')),
        getResultPart(resultRecord, 'edgeCount', translate('kg.exchange.result.edgeCount')),
        getResultPart(resultRecord, 'skippedCount', translate('kg.exchange.result.skippedCount'))
      ].filter(Boolean).join(', ') || translate('kg.exchange.importComplete')
    );
  };

  return (
    <div className="kg-modal-layer" role="presentation" onClick={onClose}>
      <section aria-label={translate('kg.exchange.ariaLabel')} aria-modal="true" className="kg-modal" role="dialog" onClick={(event) => event.stopPropagation()}>
        <header>
          <div>
            <AppIcon name="download" />
            <h2>{translate('kg.exchange.title')}</h2>
          </div>
          <button className="kg-icon-button" type="button" onClick={onClose}>
            <AppIcon name="x" />
          </button>
        </header>

        <div className="kg-exchange-tabs">
          <button className={draft.mode === 'export' ? 'active' : ''} type="button" onClick={() => onChange({ mode: 'export' })}>
            {translate('kg.exchange.tab.export')}
          </button>
          <button className={draft.mode === 'import' ? 'active' : ''} type="button" onClick={() => onChange({ mode: 'import' })}>
            {translate('kg.exchange.tab.import')}
          </button>
        </div>

        {draft.mode === 'export' ? (
          <div className="kg-exchange-body">
            <label className="kg-field">
              <span>{translate('kg.exchange.field.format')}</span>
              <select value={draft.format} onChange={(event) => onChange({ format: event.target.value as KnowledgeGraphExchangeDraft['format'] })}>
                <option value="json">{translate('kg.exchange.format.json')}</option>
                <option value="mermaid">{translate('kg.exchange.format.mermaid')}</option>
              </select>
            </label>
            <PermissionButton className="primary-button" code="ai:knowledge:graph:export" disabled={exporting} fallback="disable" type="button" onClick={() => void handleExport()}>
              {exporting ? translate('kg.exchange.exporting') : translate('kg.exchange.exportCurrent')}
            </PermissionButton>
            <textarea readOnly className="kg-exchange-textarea" value={exportContent} />
          </div>
        ) : (
          <div className="kg-exchange-body">
            <label className="kg-field">
              <span>{translate('kg.exchange.field.fileName')}</span>
              <input value={draft.fileName} onChange={(event) => onChange({ fileName: event.target.value })} />
            </label>
            <textarea
              className="kg-exchange-textarea"
              placeholder={translate('kg.exchange.importPlaceholder')}
              value={draft.importContent}
              onChange={(event) => onChange({ importContent: event.target.value })}
            />
            {importSummary ? <p className="kg-import-summary">{importSummary}</p> : null}
            <PermissionButton className="primary-button" code="ai:knowledge:graph:import" disabled={importing || !draft.importContent.trim()} fallback="disable" type="button" onClick={() => void handleImport()}>
              {importing ? translate('kg.exchange.importing') : translate('kg.exchange.import')}
            </PermissionButton>
          </div>
        )}
      </section>
    </div>
  );
}

function getResultPart(record: Record<string, unknown>, key: string, label: string): string {
  const value = record[key];
  if (typeof value === 'number') {
    return `${label} ${value}`;
  }
  return '';
}
