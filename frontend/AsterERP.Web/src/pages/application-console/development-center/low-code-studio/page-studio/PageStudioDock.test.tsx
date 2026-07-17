import { describe, expect, it } from 'vitest';

import { normalizeStudioDockState, toStudioDockPreference } from './PageStudioDock';

describe('Page Studio dock state', () => {
  it('falls back to a collapsed dock when persisted state is invalid', () => {
    expect(normalizeStudioDockState({ activeTool: 'unknown', mode: 'pinned', width: 336 })).toEqual({ activeTool: 'components', mode: 'collapsed', width: 272 });
  });

  it('preserves only supported tools and clamps persisted width', () => {
    expect(normalizeStudioDockState({ activeTool: 'resources', mode: 'pinned', width: 999 })).toEqual({ activeTool: 'resources', mode: 'pinned', width: 288 });
  });

  it('never restores a transient overlay and persists only the dock preference', () => {
    expect(normalizeStudioDockState({ activeTool: 'layers', mode: 'overlay', width: 260 })).toEqual({ activeTool: 'layers', mode: 'collapsed', width: 260 });
    expect(toStudioDockPreference({ activeTool: 'page', mode: 'overlay', width: 260 })).toEqual({ activeTool: 'page', pinned: false, width: 260 });
  });
});
