import { describe, expect, it } from 'vitest';

import { defaultContainerLayout, defaultPlacement, normalizeLayoutProtocol, type LayoutProtocol } from './LayoutProtocol';
import { layoutProtocolDiagnosticCodes } from './LayoutProtocolDiagnostics';
import { validateLayoutProtocol } from './LayoutProtocolValidator';

describe('LayoutProtocol', () => {
  it('creates a mode-specific container and placement without mixing responsibilities', () => {
    expect(defaultContainerLayout('grid')).toMatchObject({ mode: 'grid', grid: { columns: ['1fr'], rows: ['auto'] } });
    expect(defaultPlacement('flex')).toEqual({ kind: 'flex-item', flexItem: { order: 0, grow: 0, shrink: 1, basis: 'auto' } });
    expect(defaultPlacement('free', 12, 24)).toEqual({ kind: 'absolute', absolute: { x: 12, y: 24 } });
  });

  it('normalizes a protocol to one payload matching the container mode', () => {
    const protocol: LayoutProtocol = {
      container: { mode: 'flex', flex: { direction: 'column', wrap: 'wrap', gap: 8, alignItems: 'center', justifyContent: 'end' } },
      placement: { kind: 'absolute', absolute: { x: 10, y: 20 }, flexItem: { order: 2, grow: 1, shrink: 0, basis: 120 } },
      size: { width: 120, height: 40 }
    };
    expect(normalizeLayoutProtocol(protocol)).toMatchObject({ container: { mode: 'flex' }, placement: { kind: 'flex-item' }, size: protocol.size });
    expect(normalizeLayoutProtocol(protocol).placement.absolute).toBeUndefined();
  });

  it('reports invalid mixed placement, invalid size and invalid grid span with stable codes and paths', () => {
    const diagnostics = validateLayoutProtocol({
      container: { mode: 'grid', grid: { columns: ['1fr'], rows: ['auto'], columnGap: 0, rowGap: 0, autoFlow: 'row' } },
      placement: { kind: 'grid-item', absolute: { x: 0, y: 0 }, gridItem: { rowStart: 'auto', rowSpan: 0, columnStart: 'auto', columnSpan: 1 } },
      size: { width: -1, height: 20, aspectRatio: 0 }
    });
    expect(diagnostics).toEqual(expect.arrayContaining([
      expect.objectContaining({ code: layoutProtocolDiagnosticCodes.multiplePlacementPayloads, path: 'placement' }),
      expect.objectContaining({ code: layoutProtocolDiagnosticCodes.invalidDimension, path: 'size.width' }),
      expect.objectContaining({ code: layoutProtocolDiagnosticCodes.invalidAspectRatio, path: 'size.aspectRatio' }),
      expect.objectContaining({ code: layoutProtocolDiagnosticCodes.invalidGridSpan, path: 'placement.gridItem' })
    ]));
  });

  it('rejects fractional and non-finite grid spans', () => {
    const diagnostics = validateLayoutProtocol({
      container: defaultContainerLayout('grid'),
      placement: { kind: 'grid-item', gridItem: { rowStart: 1, rowSpan: 1.5, columnStart: 1, columnSpan: Number.NaN } },
      size: { width: 100, height: 40 }
    });
    expect(diagnostics).toEqual(expect.arrayContaining([expect.objectContaining({ code: layoutProtocolDiagnosticCodes.invalidGridSpan })]));
  });

  it('rejects malformed persisted migration state', () => {
    const diagnostics = validateLayoutProtocol({
      container: defaultContainerLayout('free'),
      placement: defaultPlacement('free'),
      size: { width: 100, height: 40 },
      migration: {
        previousContainers: { flex: { mode: 'grid' } },
        previousPlacements: { flex: { kind: 'absolute', absolute: { x: 0, y: 0 } } }
      }
    } as unknown as LayoutProtocol);
    expect(diagnostics).toEqual(expect.arrayContaining([
      expect.objectContaining({ code: layoutProtocolDiagnosticCodes.invalidMigrationState, path: 'migration.previousContainers.flex' }),
      expect.objectContaining({ code: layoutProtocolDiagnosticCodes.invalidMigrationState, path: 'migration.previousPlacements.flex' })
    ]));
  });
});
