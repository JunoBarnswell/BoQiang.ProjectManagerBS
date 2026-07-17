import {
  ArrowDown,
  ArrowUp,
  Eye,
  EyeOff,
  GripVertical,
  PanelRightClose,
  Plus,
  RotateCcw,
  Search,
  Trash2,
  X
} from 'lucide-react';
import { type ReactNode, useMemo, useState } from 'react';

import { formatMessage } from '../../../core/i18n/formatMessage';
import { useI18n } from '../../../core/i18n/I18nProvider';
import { useDragReorder } from '../hooks/useDragReorder';
import type { DataTableColumnSetting, DataTableSide, DataTableSortDirection, DataTableSortRule } from '../tableTypes';

export interface ColumnSettingsPanelColumn {
  canFreeze: boolean;
  canHide: boolean;
  fixed?: DataTableSide;
  isVisible: boolean;
  key: string;
  setting?: DataTableColumnSetting;
  sortable: boolean;
  sortField: string;
  title: ReactNode;
  titleText: string;
  width?: string;
  widthPx: number;
}

interface ColumnSettingsPanelProps {
  actions?: ReactNode;
  columns: ColumnSettingsPanelColumn[];
  onAddSortRule: (field: string) => void;
  onClearSortRules: () => void;
  onClose: () => void;
  onColumnChange: (key: string, patch: Partial<DataTableColumnSetting>) => void;
  onColumnReorder: (sourceKey: string, targetKey: string) => void;
  onMoveSortRule: (index: number, offset: -1 | 1) => void;
  onRemoveSortRule: (index: number) => void;
  onReset: () => void;
  onShowVerticalLinesChange: (enabled: boolean) => void;
  onSortRuleDirectionChange: (index: number, direction: DataTableSortDirection) => void;
  onSortRuleFieldChange: (index: number, field: string) => void;
  showVerticalLines: boolean;
  sortRules: DataTableSortRule[];
}

interface SettingSwitchProps {
  checked: boolean;
  disabled?: boolean;
  label: string;
  onChange: (checked: boolean) => void;
}

function SettingSwitch({ checked, disabled = false, label, onChange }: SettingSwitchProps) {
  return (
    <label className={['data-table-setting-toggle', disabled ? 'data-table-setting-toggle--disabled' : ''].filter(Boolean).join(' ')}>
      <input checked={checked} disabled={disabled} role="switch" type="checkbox" onChange={(event) => onChange(event.target.checked)} />
      <span aria-hidden="true" className="data-table-setting-toggle__track" />
      <span>{label}</span>
    </label>
  );
}

function splitFields(value: string): string[] | null {
  const fields = value
    .split(',')
    .map((item) => item.trim())
    .filter(Boolean);
  return fields.length > 0 ? fields : null;
}

function joinFields(fields?: string[] | null): string {
  return fields?.join(', ') ?? '';
}

function emptyToUndefined(value: string): string | undefined {
  const next = value.trim();
  return next.length > 0 ? next : undefined;
}

function emptyToNull(value: string): string | null {
  const next = value.trim();
  return next.length > 0 ? next : null;
}

function getMerge(setting?: DataTableColumnSetting) {
  return setting?.merge ?? {
    direction: 'vertical',
    enabled: false,
    fields: [],
    strategy: 'same-value'
  };
}

function getValueSource(setting?: DataTableColumnSetting) {
  return setting?.valueSource ?? {
    field: null,
    fields: null,
    path: null,
    template: null,
    type: 'field'
  };
}

export function ColumnSettingsPanel({
  actions,
  columns,
  onAddSortRule,
  onClearSortRules,
  onClose,
  onColumnChange,
  onColumnReorder,
  onMoveSortRule,
  onRemoveSortRule,
  onReset,
  onShowVerticalLinesChange,
  onSortRuleDirectionChange,
  onSortRuleFieldChange,
  showVerticalLines,
  sortRules
}: ColumnSettingsPanelProps) {
  const { translate } = useI18n();
  const [keyword, setKeyword] = useState('');
  const [selectedKey, setSelectedKey] = useState(() => columns[0]?.key ?? '');
  const normalizedKeyword = keyword.trim().toLowerCase();
  const columnDrag = useDragReorder({
    enabled: columns.length > 1,
    onDrop: onColumnReorder
  });

  const filteredColumns = useMemo(
    () =>
      columns.filter((column) =>
        normalizedKeyword.length === 0 ||
        column.key.toLowerCase().includes(normalizedKeyword) ||
        column.titleText.toLowerCase().includes(normalizedKeyword) ||
        (column.setting?.binding ?? '').toLowerCase().includes(normalizedKeyword)
      ),
    [columns, normalizedKeyword]
  );

  const selectedColumn = columns.find((column) => column.key === selectedKey) ?? filteredColumns[0] ?? columns[0];
  const selectedSetting = selectedColumn?.setting;
  const merge = getMerge(selectedSetting);
  const valueSource = getValueSource(selectedSetting);
  const sortFieldSet = new Set(sortRules.map((rule) => rule.field));
  const sortableColumns = columns.filter((column) => column.sortable);
  const availableSortField = sortableColumns.find((column) => !sortFieldSet.has(column.sortField))?.sortField ?? '';

  const updateMerge = (patch: Partial<NonNullable<DataTableColumnSetting['merge']>>) => {
    if (!selectedColumn) {
      return;
    }

    onColumnChange(selectedColumn.key, {
      merge: {
        ...merge,
        ...patch
      }
    });
  };

  const updateValueSource = (patch: Partial<NonNullable<DataTableColumnSetting['valueSource']>>) => {
    if (!selectedColumn) {
      return;
    }

    onColumnChange(selectedColumn.key, {
      valueSource: {
        ...valueSource,
        ...patch
      }
    });
  };

  const updateSelectedColumn = (patch: Partial<DataTableColumnSetting>) => {
    if (!selectedColumn) {
      return;
    }

    onColumnChange(selectedColumn.key, patch);
  };

  return (
    <div className="data-table-settings-overlay">
      <aside aria-label={translate('table.columnSettings')} className="data-table-settings-panel">
        <header className="data-table-settings-panel__header">
          <div>
            <strong>{translate('table.columnSettings')}</strong>
            <span>{formatMessage(translate('table.totalColumns'), { count: columns.length })}</span>
          </div>
          <button aria-label={translate('table.closeColumnSettings')} className="data-table-icon-button" type="button" onClick={onClose}>
            <PanelRightClose size={16} />
          </button>
        </header>

        <div className="data-table-settings-panel__actions">
          {actions}
          <button className="data-table-text-button" type="button" onClick={onReset}>
            <RotateCcw size={14} />
            {translate('table.restoreDefault')}
          </button>
        </div>

        <div className="data-table-settings-panel__body">
          <section className="data-table-settings-section">
            <SettingSwitch checked={showVerticalLines} label={translate('table.showVerticalLines')} onChange={onShowVerticalLinesChange} />
          </section>

          <section className="data-table-settings-section data-table-settings-section--split">
            <div className="data-table-settings-search">
              <Search size={14} />
              <input
                placeholder={translate('table.searchColumns')}
                type="search"
                value={keyword}
                onChange={(event) => setKeyword(event.target.value)}
              />
              {keyword ? (
                <button aria-label={translate('table.clearSearch')} type="button" onClick={() => setKeyword('')}>
                  <X size={14} />
                </button>
              ) : null}
            </div>

            <div className="data-table-column-list">
              {filteredColumns.map((column) => {
                const isSelected = selectedColumn?.key === column.key;
                const isDragOver = columnDrag.dragOverKey === column.key;
                return (
                  <button
                    className={[
                      'data-table-column-list__item',
                      isSelected ? 'data-table-column-list__item--active' : '',
                      isDragOver ? 'data-table-column-list__item--over' : ''
                    ].filter(Boolean).join(' ')}
                    key={column.key}
                    type="button"
                    {...columnDrag.getDropTargetProps(column.key)}
                    onClick={() => setSelectedKey(column.key)}
                  >
                    <span
                      aria-label={translate('table.dragSort')}
                      className="data-table-column-list__drag"
                      {...columnDrag.getDragSourceProps(column.key)}
                    >
                      <GripVertical size={14} />
                    </span>
                    {column.isVisible ? <Eye size={14} /> : <EyeOff size={14} />}
                    <span className="data-table-column-list__title">{column.titleText || column.key}</span>
                    <span className="data-table-column-list__meta">{column.setting?.binding ?? column.key}</span>
                  </button>
                );
              })}
            </div>
          </section>

          {selectedColumn ? (
            <section className="data-table-settings-section">
              <div className="data-table-settings-section__title">
                <span>{selectedColumn.titleText || selectedColumn.key}</span>
                <code>{selectedColumn.key}</code>
              </div>

              <SettingSwitch
                checked={selectedColumn.isVisible}
                disabled={!selectedColumn.canHide}
                label={translate('table.show')}
                onChange={(checked) => updateSelectedColumn({ isVisible: checked })}
              />

              <div className="data-table-settings-grid">
                <label className="data-table-field">
                  <span>{translate('table.title')}</span>
                  <input
                    value={selectedSetting?.title ?? selectedColumn.titleText}
                    onChange={(event) => updateSelectedColumn({ title: event.target.value })}
                  />
                </label>

                <label className="data-table-field">
                  <span>{translate('table.bindingField')}</span>
                  <input
                    value={selectedSetting?.binding ?? selectedColumn.key}
                    onChange={(event) => updateSelectedColumn({ binding: emptyToUndefined(event.target.value) })}
                  />
                </label>

                <label className="data-table-field">
                  <span>{translate('table.width')}</span>
                  <input
                    value={selectedSetting?.width ?? selectedColumn.width ?? `${Math.round(selectedColumn.widthPx)}px`}
                    onChange={(event) => updateSelectedColumn({ width: emptyToUndefined(event.target.value) })}
                  />
                </label>

                <label className="data-table-field">
                  <span>{translate('table.fixed')}</span>
                  <select
                    disabled={!selectedColumn.canFreeze}
                    value={selectedColumn.fixed ?? ''}
                    onChange={(event) => updateSelectedColumn({ fixed: (event.target.value as DataTableSide) || undefined })}
                  >
                    <option value="">{translate('table.notFixed')}</option>
                    <option value="left">{translate('table.fixedLeft')}</option>
                    <option value="right">{translate('table.fixedRight')}</option>
                  </select>
                </label>

                <label className="data-table-field">
                  <span>{translate('table.queryField')}</span>
                  <input
                    value={selectedSetting?.queryField ?? ''}
                    onChange={(event) => updateSelectedColumn({ queryField: emptyToUndefined(event.target.value) })}
                  />
                </label>

                <label className="data-table-field">
                  <span>{translate('table.sortField')}</span>
                  <input
                    value={selectedSetting?.sortField ?? ''}
                    onChange={(event) => updateSelectedColumn({ sortField: emptyToUndefined(event.target.value) })}
                  />
                </label>

                <label className="data-table-field">
                  <span>{translate('table.renderer')}</span>
                  <input
                    value={selectedSetting?.renderer ?? ''}
                    onChange={(event) => updateSelectedColumn({ renderer: emptyToUndefined(event.target.value) })}
                  />
                </label>
              </div>
            </section>
          ) : null}

          {selectedColumn ? (
            <section className="data-table-settings-section">
              <div className="data-table-settings-section__title">
                <span>{translate('table.mergeConfig')}</span>
              </div>
              <SettingSwitch checked={merge.enabled} label={translate('table.enabled')} onChange={(checked) => updateMerge({ enabled: checked })} />
              <div className="data-table-settings-grid">
                <label className="data-table-field">
                  <span>{translate('table.direction')}</span>
                  <input value={merge.direction} onChange={(event) => updateMerge({ direction: event.target.value })} />
                </label>
                <label className="data-table-field">
                  <span>{translate('table.strategy')}</span>
                  <input value={merge.strategy} onChange={(event) => updateMerge({ strategy: event.target.value })} />
                </label>
                <label className="data-table-field data-table-field--wide">
                  <span>{translate('table.fields')}</span>
                  <input value={joinFields(merge.fields)} onChange={(event) => updateMerge({ fields: splitFields(event.target.value) ?? [] })} />
                </label>
              </div>
            </section>
          ) : null}

          {selectedColumn ? (
            <section className="data-table-settings-section">
              <div className="data-table-settings-section__title">
                <span>{translate('table.valueSource')}</span>
              </div>
              <div className="data-table-settings-grid">
                <label className="data-table-field">
                  <span>{translate('table.type')}</span>
                  <input value={valueSource.type} onChange={(event) => updateValueSource({ type: event.target.value })} />
                </label>
                <label className="data-table-field">
                  <span>{translate('table.fields')}</span>
                  <input value={valueSource.field ?? ''} onChange={(event) => updateValueSource({ field: emptyToNull(event.target.value) })} />
                </label>
                <label className="data-table-field">
                  <span>{translate('table.path')}</span>
                  <input value={valueSource.path ?? ''} onChange={(event) => updateValueSource({ path: emptyToNull(event.target.value) })} />
                </label>
                <label className="data-table-field data-table-field--wide">
                  <span>{translate('table.fieldCollection')}</span>
                  <input value={joinFields(valueSource.fields)} onChange={(event) => updateValueSource({ fields: splitFields(event.target.value) })} />
                </label>
                <label className="data-table-field data-table-field--wide">
                  <span>{translate('table.template')}</span>
                  <textarea value={valueSource.template ?? ''} onChange={(event) => updateValueSource({ template: emptyToNull(event.target.value) })} />
                </label>
              </div>
            </section>
          ) : null}

          <section className="data-table-settings-section">
            <div className="data-table-settings-section__title">
              <span>{translate('table.dataSort')}</span>
              <div className="data-table-settings-section__tools">
                <button className="data-table-icon-button" disabled={!availableSortField} type="button" onClick={() => onAddSortRule(availableSortField)}>
                  <Plus size={14} />
                </button>
                <button className="data-table-icon-button" disabled={sortRules.length === 0} type="button" onClick={onClearSortRules}>
                  <X size={14} />
                </button>
              </div>
            </div>

            <div className="data-table-sort-list">
              {sortRules.map((rule, index) => (
                <div className="data-table-sort-list__item" key={`${rule.field}-${index}`}>
                  <span className="data-table-sort-list__priority">{index + 1}</span>
                  <select value={rule.field} onChange={(event) => onSortRuleFieldChange(index, event.target.value)}>
                    {sortableColumns.map((column) => (
                      <option key={column.key} value={column.sortField}>
                        {column.titleText || column.key}
                      </option>
                    ))}
                  </select>
                  <select value={rule.direction} onChange={(event) => onSortRuleDirectionChange(index, event.target.value as DataTableSortDirection)}>
                    <option value="asc">{translate('table.ascending')}</option>
                    <option value="desc">{translate('table.descending')}</option>
                  </select>
                  <button className="data-table-icon-button" disabled={index === 0} type="button" onClick={() => onMoveSortRule(index, -1)}>
                    <ArrowUp size={14} />
                  </button>
                  <button className="data-table-icon-button" disabled={index === sortRules.length - 1} type="button" onClick={() => onMoveSortRule(index, 1)}>
                    <ArrowDown size={14} />
                  </button>
                  <button className="data-table-icon-button" type="button" onClick={() => onRemoveSortRule(index)}>
                    <Trash2 size={14} />
                  </button>
                </div>
              ))}
              {sortRules.length === 0 ? <p className="data-table-settings-empty">{translate('table.noSortConfigured')}</p> : null}
            </div>
          </section>
        </div>
      </aside>
    </div>
  );
}
