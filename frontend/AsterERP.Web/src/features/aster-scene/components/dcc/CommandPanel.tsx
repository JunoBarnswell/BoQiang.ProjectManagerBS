import {
  Boxes,
  Camera,
  Circle,
  DoorOpen,
  Grid3X3,
  HelpingHand,
  Lamp,
  Layers3,
  PanelTop,
  RectangleHorizontal,
  Sparkles,
  Square,
  Target,
  Triangle,
  Video,
  Workflow
} from 'lucide-react';
import type { ComponentType } from 'react';

import { primitiveCatalog, type PrimitiveDefinition } from '../../core/dcc/sceneDocumentDcc';

interface CommandPanelProps {
  activePrimitiveCode: PrimitiveDefinition['code'] | null;
  onCreatePrimitive: (definition: PrimitiveDefinition) => void;
  t: (key: string) => string;
}

const iconMap: Record<string, ComponentType<{ size?: number }>> = {
  camera: Camera,
  ceiling: PanelTop,
  circle: Circle,
  collider: Grid3X3,
  cone: Triangle,
  cube: Square,
  cylinder: Boxes,
  door: DoorOpen,
  helper: HelpingHand,
  hotspot: Target,
  light: Lamp,
  panel: Video,
  plinth: RectangleHorizontal,
  portal: Workflow,
  showcase: Sparkles,
  torus: Circle,
  tube: Boxes,
  wall: Layers3,
  window: PanelTop
};

export function CommandPanel({ activePrimitiveCode, onCreatePrimitive, t }: CommandPanelProps) {
  return (
    <section className="as-dcc-command-panel">
      <header>
        <h2>{t('asterscene.dcc.createPanel')}</h2>
        {activePrimitiveCode ? <span>{t('asterscene.dcc.create.activeTool')}</span> : null}
      </header>
      <div className="as-dcc-primitive-grid">
        {primitiveCatalog.map((definition) => {
          const Icon = iconMap[definition.icon] ?? Square;
          return (
            <button className={activePrimitiveCode === definition.code ? 'is-active' : undefined} key={definition.code} onClick={() => onCreatePrimitive(definition)} type="button">
              <Icon size={18} />
              <span>{t(definition.labelKey)}</span>
            </button>
          );
        })}
      </div>
    </section>
  );
}
