import { Boxes, FileCog, Layers3, PanelLeftClose, PanelLeftOpen, Pin, TableProperties } from 'lucide-react';
import { useEffect, useId, useState, type ReactNode } from 'react';

import { useI18n } from '../../../../../core/i18n/I18nProvider';

export type StudioToolId = 'components' | 'layers' | 'resources' | 'page';
export type StudioDockMode = 'collapsed' | 'overlay' | 'pinned';

export interface StudioDockState {
  activeTool: StudioToolId;
  mode: StudioDockMode;
  width: number;
}

export interface StudioDockPreference {
  activeTool: StudioToolId;
  pinned: boolean;
  width: number;
}

const DEFAULT_DOCK_STATE: StudioDockState = { activeTool: 'components', mode: 'collapsed', width: 272 };
const MIN_DOCK_WIDTH = 240;
const MAX_DOCK_WIDTH = 288;

export function readStudioDockState(storageKey: string): StudioDockState {
  if (typeof window === 'undefined') return DEFAULT_DOCK_STATE;
  try {
    return normalizeStudioDockState(JSON.parse(window.localStorage.getItem(storageKey) ?? ''));
  } catch {
    return DEFAULT_DOCK_STATE;
  }
}

export function normalizeStudioDockState(value: unknown): StudioDockState {
  if (!value || typeof value !== 'object') return DEFAULT_DOCK_STATE;
  const state = value as Partial<StudioDockState & StudioDockPreference>;
  if (!isTool(state.activeTool)) return DEFAULT_DOCK_STATE;
  if (typeof state.pinned === 'boolean') return { activeTool: state.activeTool, mode: state.pinned ? 'pinned' : 'collapsed', width: clampWidth(state.width) };
  if (!isMode(state.mode)) return DEFAULT_DOCK_STATE;
  return { activeTool: state.activeTool, mode: state.mode === 'pinned' ? 'pinned' : 'collapsed', width: clampWidth(state.width) };
}

export function toStudioDockPreference(state: StudioDockState): StudioDockPreference {
  return { activeTool: state.activeTool, pinned: state.mode === 'pinned', width: clampWidth(state.width) };
}

export function PageStudioDock({ children, onChange, state }: { children: ReactNode; onChange: (state: StudioDockState) => void; state: StudioDockState }) {
  const { translate } = useI18n();
  const [desktop, setDesktop] = useState(() => typeof window === 'undefined' || window.matchMedia('(min-width: 1280px)').matches);
  const panelId = useId();
  const effectiveMode: StudioDockMode = state.mode === 'pinned' && !desktop ? 'overlay' : state.mode;

  useEffect(() => {
    const query = window.matchMedia('(min-width: 1280px)');
    const sync = () => {
      setDesktop(query.matches);
    };
    sync();
    query.addEventListener('change', sync);
    return () => query.removeEventListener('change', sync);
  }, []);

  useEffect(() => {
    if (state.mode !== 'overlay') return undefined;
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key !== 'Escape' || event.defaultPrevented) return;
      event.preventDefault();
      onChange({ ...state, mode: 'collapsed' });
      window.requestAnimationFrame(() => document.querySelector<HTMLButtonElement>(`[data-studio-tool="${state.activeTool}"]`)?.focus());
    };
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [onChange, state]);

  const tools: ReadonlyArray<{ id: StudioToolId; icon: typeof Boxes; label: string }> = [
    { id: 'components', icon: Boxes, label: translate('lowCode.pageStudio.components') },
    { id: 'layers', icon: Layers3, label: translate('lowCode.pageStudio.layers') },
    { id: 'resources', icon: TableProperties, label: translate('lowCode.pageStudio.resources') },
    { id: 'page', icon: FileCog, label: translate('lowCode.pageStudio.pageTools') }
  ];
  const expanded = effectiveMode !== 'collapsed';
  const panelClass = effectiveMode === 'pinned' ? 'page-studio__dock-panel page-studio__dock-panel--pinned' : 'page-studio__dock-panel page-studio__dock-panel--overlay';

  return <div className="page-studio__dock" data-page-studio-dock="true">
    <nav aria-label={translate('lowCode.pageStudio.dockTools')} className="page-studio__dock-rail">
      {tools.map(({ id, icon: Icon, label }) => {
        const active = expanded && state.activeTool === id;
        return <button aria-controls={panelId} aria-expanded={active} aria-label={label} className={`page-studio__dock-tool ${active ? 'is-active' : ''}`} data-page-studio-dock-tool="true" data-studio-tool={id} key={id} title={label} type="button" onClick={() => onChange({ ...state, activeTool: id, mode: active ? 'collapsed' : state.mode === 'pinned' && desktop ? 'pinned' : 'overlay' })}><Icon aria-hidden="true" className="h-4 w-4" /></button>;
      })}
    </nav>
    {expanded ? <aside aria-label={tools.find((tool) => tool.id === state.activeTool)?.label} className={panelClass} id={panelId} style={{ width: `${state.width}px` }}>
      <header className="page-studio__panel-header">
        <h2 className="truncate text-sm font-semibold">{tools.find((tool) => tool.id === state.activeTool)?.label}</h2>
        <div className="flex items-center gap-1">
          {desktop ? <button aria-label={state.mode === 'pinned' ? translate('lowCode.pageStudio.unpinDock') : translate('lowCode.pageStudio.pinDock')} className="icon-button h-7 w-7" title={state.mode === 'pinned' ? translate('lowCode.pageStudio.unpinDock') : translate('lowCode.pageStudio.pinDock')} type="button" onClick={() => onChange({ ...state, mode: state.mode === 'pinned' ? 'overlay' : 'pinned' })}><Pin aria-hidden="true" className={`h-3.5 w-3.5 ${state.mode === 'pinned' ? 'fill-current' : ''}`} /></button> : null}
          <button aria-label={translate('lowCode.pageStudio.closeDock')} className="icon-button h-7 w-7" title={translate('lowCode.pageStudio.closeDock')} type="button" onClick={() => onChange({ ...state, mode: 'collapsed' })}>{effectiveMode === 'pinned' ? <PanelLeftClose aria-hidden="true" className="h-3.5 w-3.5" /> : <PanelLeftOpen aria-hidden="true" className="h-3.5 w-3.5" />}</button>
        </div>
      </header>
      {effectiveMode === 'pinned' ? <label className="page-studio__dock-width"><span>{translate('lowCode.pageStudio.dockWidth')}</span><input aria-label={translate('lowCode.pageStudio.dockWidth')} max={MAX_DOCK_WIDTH} min={MIN_DOCK_WIDTH} step={4} type="range" value={state.width} onChange={(event) => onChange({ ...state, width: clampWidth(Number(event.target.value)) })} /></label> : null}
      <div className="min-h-0 flex-1 overflow-y-auto">{children}</div>
    </aside> : null}
  </div>;
}

function clampWidth(value: unknown): number {
  const width = typeof value === 'number' && Number.isFinite(value) ? value : DEFAULT_DOCK_STATE.width;
  return Math.min(MAX_DOCK_WIDTH, Math.max(MIN_DOCK_WIDTH, width));
}

function isMode(value: unknown): value is StudioDockMode {
  return value === 'collapsed' || value === 'overlay' || value === 'pinned';
}

function isTool(value: unknown): value is StudioToolId {
  return value === 'components' || value === 'layers' || value === 'resources' || value === 'page';
}
