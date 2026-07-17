import type { ReactNode } from 'react';
import { useMemo } from 'react';
import { useSearchParams } from 'react-router-dom';

import './styles/ai-center.css';

export interface AiCenterTabItem {
  key: string;
  label: string;
  description?: string;
  permissionCode?: string;
  content: ReactNode;
}

export function AiCenterTabs({ defaultTab, description, eyebrow, items, title }: { defaultTab: string; description?: string; eyebrow?: string; items: AiCenterTabItem[]; title?: string }) {
  const [searchParams, setSearchParams] = useSearchParams();
  const activeKey = searchParams.get('tab') ?? defaultTab;
  const activeItem = useMemo(() => items.find((item) => item.key === activeKey) ?? items[0], [activeKey, items]);

  return (
    <div className="ai-center-tabs">
      {title ? (
        <header className="ai-center-tabs-header">
          <div>
            {eyebrow ? <span className="ai-center-tabs-eyebrow">{eyebrow}</span> : null}
            <h1>{title}</h1>
            {description ? <p>{description}</p> : null}
          </div>
          {activeItem?.description ? (
            <div className="ai-center-tabs-context">
              <span>{activeItem.label}</span>
              <strong>{activeItem.description}</strong>
            </div>
          ) : null}
        </header>
      ) : null}
      <div className="ai-center-tabbar" role="tablist">
        {items.map((item) => (
          <button
            key={item.key}
            aria-selected={item.key === activeItem.key}
            className={item.key === activeItem.key ? 'active' : ''}
            role="tab"
            type="button"
            onClick={() => {
              const next = new URLSearchParams(searchParams);
              next.set('tab', item.key);
              setSearchParams(next, { replace: true });
            }}
          >
            {item.label}
          </button>
        ))}
      </div>
      <div className="ai-center-tab-panel" role="tabpanel">
        {activeItem.content}
      </div>
    </div>
  );
}
