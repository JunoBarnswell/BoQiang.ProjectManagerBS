import { Button, TextField, ToggleButton, ToggleButtonGroup } from '@mui/material';
import { useMemo, useState } from 'react';

import { useI18n } from '../../../core/i18n/I18nProvider';
import { AppIcon } from '../../../shared/icons/AppIcon';
import { flowiseI18nKeys } from '../i18n/flowiseI18nKeys';
import type { FlowiseNodeCatalogItemDto } from '../types/node.types';

interface FlowiseNodePaletteProps {
  catalog: FlowiseNodeCatalogItemDto[];
  loading?: boolean;
  onAddNode: (item: FlowiseNodeCatalogItemDto) => void;
  onAddStickyNote: () => void;
}

export function FlowiseNodePalette({ catalog, loading, onAddNode, onAddStickyNote }: FlowiseNodePaletteProps) {
  const { translate } = useI18n();
  const [activeCategory, setActiveCategory] = useState<string>('__all__');
  const [expandedCategories, setExpandedCategories] = useState<Record<string, boolean>>({});
  const [keyword, setKeyword] = useState('');
  const categories = useMemo(() => Array.from(new Set(catalog.map((item) => item.category).filter(Boolean))).sort(), [catalog]);
  const groups = useMemo(() => {
    const normalizedKeyword = keyword.trim().toLowerCase();
    const filtered = catalog
      .filter((item) => activeCategory === '__all__' || item.category === activeCategory)
      .map((item) => ({ item, score: scoreNode(item, normalizedKeyword) }))
      .filter(({ score }) => !normalizedKeyword || score > 0)
      .sort((left, right) => right.score - left.score)
      .map(({ item }) => item);
    return filtered.reduce<Record<string, FlowiseNodeCatalogItemDto[]>>((acc, item) => {
      const category = item.category || translate(flowiseI18nKeys.canvas.allNodes);
      acc[category] = [...(acc[category] ?? []), item];
      return acc;
    }, {});
  }, [activeCategory, catalog, keyword, translate]);

  const hasNodes = Object.values(groups).some((items) => items.length > 0);
  const isExpanded = (category: string) => expandedCategories[category] ?? true;

  return (
    <aside className="flowise-node-palette">
      <div className="flowise-panel-title">
        <AppIcon name="squares-four" />
        {translate(flowiseI18nKeys.canvas.addNodes)}
      </div>
      <div className="flowise-palette-search">
        <TextField
          fullWidth
          placeholder={translate(flowiseI18nKeys.canvas.paletteSearch)}
          size="small"
          value={keyword}
          slotProps={{
            input: {
              startAdornment: <AppIcon className="flowise-palette-search__icon" name="search" />
            }
          }}
          onChange={(event) => setKeyword(event.target.value)}
        />
      </div>
      <ToggleButtonGroup
        exclusive
        className="flowise-palette-tabs"
        size="small"
        value={activeCategory}
        onChange={(_event, value: string | null) => {
          if (value) {
            setActiveCategory(value);
          }
        }}
      >
        <ToggleButton value="__all__">
          {translate(flowiseI18nKeys.canvas.allNodes)}
        </ToggleButton>
        {categories.map((category) => (
          <ToggleButton key={category} value={category}>
            {category}
          </ToggleButton>
        ))}
      </ToggleButtonGroup>
      <Button className="flowise-palette-sticky" fullWidth startIcon={<AppIcon name="file-text" />} variant="outlined" onClick={onAddStickyNote}>
        {translate(flowiseI18nKeys.canvas.stickyNote)}
      </Button>
      <div className="flowise-node-catalog">
        {loading ? <p>{translate(flowiseI18nKeys.common.loading)}</p> : null}
        {!loading && !hasNodes ? <p>{translate(flowiseI18nKeys.messages.noNodes)}</p> : null}
        {Object.entries(groups).map(([category, items]) => (
          <section key={category}>
            <Button
              className="flowise-node-catalog__category"
              title={translate(isExpanded(category) ? flowiseI18nKeys.canvas.collapseCategory : flowiseI18nKeys.canvas.expandCategory)}
              variant="text"
              onClick={() => setExpandedCategories((current) => ({ ...current, [category]: !isExpanded(category) }))}
            >
              <AppIcon name={isExpanded(category) ? 'chevron-down' : 'chevron-right'} />
              <h4>{category}</h4>
              <em>{items.length}</em>
            </Button>
            {isExpanded(category)
              ? items.map((item) => (
                  <Button
                    draggable
                    className="flowise-node-catalog__item"
                    key={item.nodeType}
                    variant="outlined"
                    onClick={() => onAddNode(item)}
                    onDragStart={(event) => {
                      event.dataTransfer.setData('application/reactflow', JSON.stringify(item));
                      event.dataTransfer.setData('application/x-flowise-node', JSON.stringify(item));
                      event.dataTransfer.effectAllowed = 'move';
                    }}
                  >
                    <span className="flowise-node-catalog__item-icon">
                      <AppIcon name={item.icon ?? 'module'} />
                    </span>
                    <strong>{item.displayName}</strong>
                    <small>{item.description}</small>
                  </Button>
                ))
              : null}
          </section>
        ))}
      </div>
    </aside>
  );
}

function scoreNode(item: FlowiseNodeCatalogItemDto, keyword: string): number {
  if (!keyword) {
    return 1;
  }

  return Math.max(
    fuzzyScore(keyword, item.displayName),
    fuzzyScore(keyword, item.nodeType),
    fuzzyScore(keyword, item.category),
    fuzzyScore(keyword, item.description) * 0.5
  );
}

function fuzzyScore(searchTerm: string, text?: string | null): number {
  const search = searchTerm.trim().toLowerCase();
  const target = (text ?? '').toLowerCase();
  if (!search || !target) {
    return 0;
  }

  const exactIndex = target.indexOf(search);
  if (exactIndex >= 0) {
    return 1000 - exactIndex * 2 - Math.max(0, target.length - search.length);
  }

  let score = 0;
  let searchIndex = 0;
  let lastMatchIndex = -1;
  for (let index = 0; index < target.length && searchIndex < search.length; index += 1) {
    if (target[index] !== search[searchIndex]) {
      continue;
    }

    score += 10;
    if (lastMatchIndex === index - 1) {
      score += 8;
    }

    if (index === 0 || [' ', '-', '_'].includes(target[index - 1])) {
      score += 12;
    }

    lastMatchIndex = index;
    searchIndex += 1;
  }

  return searchIndex === search.length ? score : 0;
}
