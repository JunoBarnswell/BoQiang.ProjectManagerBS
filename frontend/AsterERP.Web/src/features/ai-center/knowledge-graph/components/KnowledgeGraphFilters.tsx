import { useI18n } from '../../../../core/i18n/I18nProvider';
import { AppIcon } from '../../../../shared/icons/AppIcon';
import type { KnowledgeGraphFilterState, KnowledgeGraphOption } from '../types';

interface KnowledgeGraphFiltersProps {
  filters: KnowledgeGraphFilterState;
  nodeTypeOptions: KnowledgeGraphOption[];
  relationTypeOptions: KnowledgeGraphOption[];
  sourceOptions: KnowledgeGraphOption[];
  statusOptions: KnowledgeGraphOption[];
  onChange: (patch: Partial<KnowledgeGraphFilterState>) => void;
  onReset: () => void;
}

export function KnowledgeGraphFilters({
  filters,
  nodeTypeOptions,
  onChange,
  onReset,
  relationTypeOptions,
  sourceOptions,
  statusOptions
}: KnowledgeGraphFiltersProps) {
  const { translate } = useI18n();

  return (
    <section className="kg-filters" aria-label={translate('kg.filters.ariaLabel')}>
      <label className="kg-field kg-field--wide">
        <span>{translate('kg.filters.keyword')}</span>
        <div className="kg-input-with-icon">
          <AppIcon name="search" />
          <input
            placeholder={translate('kg.filters.keywordPlaceholder')}
            value={filters.keyword}
            onChange={(event) => onChange({ keyword: event.target.value })}
          />
        </div>
      </label>

      <label className="kg-field">
        <span>{translate('kg.filters.nodeType')}</span>
        <select value={filters.nodeType} onChange={(event) => onChange({ nodeType: event.target.value })}>
          <option value="">{translate('ai.search.statusAll')}</option>
          {nodeTypeOptions.map((option) => (
            <option key={option.value} value={option.value}>{option.label}</option>
          ))}
        </select>
      </label>

      <label className="kg-field">
        <span>{translate('kg.filters.relationType')}</span>
        <select value={filters.relationType} onChange={(event) => onChange({ relationType: event.target.value })}>
          <option value="">{translate('ai.search.statusAll')}</option>
          {relationTypeOptions.map((option) => (
            <option key={option.value} value={option.value}>{option.label}</option>
          ))}
        </select>
      </label>

      <label className="kg-field">
        <span>{translate('kg.filters.source')}</span>
        <select value={filters.sourceId} onChange={(event) => onChange({ sourceId: event.target.value })}>
          <option value="">{translate('ai.search.statusAll')}</option>
          {sourceOptions.map((option) => (
            <option key={option.value} value={option.value}>{option.label}</option>
          ))}
        </select>
      </label>

      <label className="kg-field">
        <span>{translate('kg.filters.status')}</span>
        <select value={filters.status} onChange={(event) => onChange({ status: event.target.value })}>
          <option value="">{translate('ai.search.statusAll')}</option>
          {statusOptions.map((option) => (
            <option key={option.value} value={option.value}>{option.label}</option>
          ))}
        </select>
      </label>

      <label className="kg-field">
        <span>{translate('kg.filters.direction')}</span>
        <select value={filters.direction} onChange={(event) => onChange({ direction: event.target.value as KnowledgeGraphFilterState['direction'] })}>
          <option value="both">{translate('kg.filters.directionBoth')}</option>
          <option value="outgoing">{translate('kg.filters.directionOutgoing')}</option>
          <option value="incoming">{translate('kg.filters.directionIncoming')}</option>
        </select>
      </label>

      <label className="kg-field">
        <span>{translate('kg.filters.maxDepth')}</span>
        <input
          min={1}
          max={8}
          type="number"
          value={filters.maxDepth}
          onChange={(event) => onChange({ maxDepth: Number(event.target.value) })}
        />
      </label>

      <label className="kg-field">
        <span>{translate('kg.filters.maxNodes')}</span>
        <input
          min={50}
          max={1000}
          step={50}
          type="number"
          value={filters.maxNodes}
          onChange={(event) => onChange({ maxNodes: Number(event.target.value) })}
        />
      </label>

      <label className="kg-check">
        <input
          checked={filters.includeOrphans}
          type="checkbox"
          onChange={(event) => onChange({ includeOrphans: event.target.checked })}
        />
        <span>{translate('kg.filters.includeOrphans')}</span>
      </label>

      <label className="kg-check">
        <input
          checked={filters.includeInactive}
          type="checkbox"
          onChange={(event) => onChange({ includeInactive: event.target.checked })}
        />
        <span>{translate('kg.filters.includeInactive')}</span>
      </label>

      <button className="ghost-button" type="button" onClick={onReset}>
        <AppIcon name="refresh" />
        {translate('common.reset')}
      </button>
    </section>
  );
}
