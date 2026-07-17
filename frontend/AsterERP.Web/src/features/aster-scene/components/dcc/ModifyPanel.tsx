import { ChevronsUpDown, Layers3, PenLine } from 'lucide-react';

import { createModifier, getModifierDefinition, modifierCatalog, readModifiers, type ModifierParameterDefinition } from '../../core/dcc/sceneDocumentDcc';
import type { SceneActor, SceneComponent, SceneDocument, SceneModifier, SceneSubObjectMode, SceneVector3 } from '../../model/types';
import { useAsterSceneEditorStore } from '../../state/editorStore';

interface ModifyPanelProps {
  document: SceneDocument;
  onMeshOperation: (operation: string) => void;
  onRenameActor: (name: string) => void;
  onTransformChange: (patch: { position?: SceneVector3; rotation?: SceneVector3; scale?: SceneVector3 }) => void;
  onUpsertModifier: (modifier: SceneModifier) => void;
  selectedActor: SceneActor | null;
  selectedTransform: SceneComponent | null;
  t: (key: string) => string;
}

const subObjectModes: SceneSubObjectMode[] = ['object', 'vertex', 'edge', 'border', 'polygon', 'element'];
const meshOperations = ['extrude', 'bevel', 'inset', 'bridge', 'connect', 'weld', 'detach', 'collapse', 'flipNormal', 'subdivide'];
const modifierTypes = modifierCatalog.map((item) => item.type);

export function ModifyPanel({ document, onMeshOperation, onRenameActor, onTransformChange, onUpsertModifier, selectedActor, selectedTransform, t }: ModifyPanelProps) {
  const { setSubObjectMode, subObjectMode } = useAsterSceneEditorStore();
  const stack = selectedActor ? readActorModifierStack(document, selectedActor) : [];
  const position = readVector(selectedTransform?.position, { x: 0, y: 0, z: 0 });
  const rotation = readVector(selectedTransform?.rotation, { x: 0, y: 0, z: 0 });
  const scale = readVector(selectedTransform?.scale, { x: 1, y: 1, z: 1 });

  return (
    <section className="as-dcc-command-panel">
      <header>
        <h2>{t('asterscene.dcc.modifyPanel')}</h2>
      </header>
      {!selectedActor ? <p className="as-dcc-muted">{t('asterscene.dcc.noSelection')}</p> : null}
      {selectedActor ? (
        <>
          <label className="as-dcc-field">
            <span>{t('asterscene.common.name')}</span>
            <input onChange={(event) => onRenameActor(event.target.value)} value={selectedActor.name} />
          </label>
          <TransformEditor label={t('asterscene.dcc.position')} onChange={(value) => onTransformChange({ position: value })} value={position} />
          <TransformEditor label={t('asterscene.dcc.rotation')} onChange={(value) => onTransformChange({ rotation: value })} step={0.05} value={rotation} />
          <TransformEditor label={t('asterscene.dcc.scale')} onChange={(value) => onTransformChange({ scale: value })} step={0.05} value={scale} />
          <div className="as-dcc-subobject">
            {subObjectModes.map((mode) => (
              <button className={subObjectMode === mode ? 'is-active' : ''} key={mode} onClick={() => setSubObjectMode(mode)} type="button">
                {t(`asterscene.dcc.subobject.${mode}`)}
              </button>
            ))}
          </div>
          <div className="as-dcc-section-title">
            <PenLine size={15} /> {t('asterscene.dcc.meshOps')}
          </div>
          <div className="as-dcc-op-grid">
            {meshOperations.map((operation) => (
              <button key={operation} onClick={() => onMeshOperation(operation)} type="button">
                {t(`asterscene.dcc.mesh.${operation}`)}
              </button>
            ))}
          </div>
          <div className="as-dcc-section-title">
            <Layers3 size={15} /> {t('asterscene.dcc.modifierStack')}
          </div>
          <div className="as-dcc-modifier-add">
            {modifierTypes.map((type) => (
              <button
                key={type}
                onClick={() => onUpsertModifier(createModifier(type, t(`asterscene.dcc.modifier.${type}`)))}
                type="button"
              >
                {t(`asterscene.dcc.modifier.${type}`)}
              </button>
            ))}
          </div>
          <div className="as-dcc-stack">
            {stack.map((modifier) => (
              <ModifierStackItem key={modifier.id} modifier={modifier} onUpsertModifier={onUpsertModifier} t={t} />
            ))}
          </div>
        </>
      ) : null}
    </section>
  );
}

function TransformEditor({
  label,
  onChange,
  step = 0.1,
  value
}: {
  label: string;
  onChange: (value: SceneVector3) => void;
  step?: number;
  value: SceneVector3;
}) {
  return (
    <label className="as-dcc-field">
      <span>{label}</span>
      <div className="as-vector-inputs">
        {(['x', 'y', 'z'] as const).map((axis) => (
          <input
            key={axis}
            onChange={(event) =>
              onChange({
                ...value,
                [axis]: Number(event.target.value)
              })
            }
            step={step}
            type="number"
            value={Number(value[axis].toFixed(3))}
          />
        ))}
      </div>
    </label>
  );
}

function ModifierStackItem({
  modifier,
  onUpsertModifier,
  t
}: {
  modifier: SceneModifier;
  onUpsertModifier: (modifier: SceneModifier) => void;
  t: (key: string) => string;
}) {
  const definition = getModifierDefinition(modifier.type);
  return (
    <div>
      <ChevronsUpDown size={14} />
      <span>{modifier.name}</span>
      <label>
        <input
          checked={modifier.enabled}
          disabled={!definition.previewSupported}
          onChange={(event) => onUpsertModifier({ ...modifier, enabled: event.target.checked })}
          type="checkbox"
        />
        {definition.previewSupported ? t('asterscene.common.ready') : t('asterscene.dcc.disabled')}
      </label>
      {definition.parameters.map((parameter) => (
        <ModifierParameterEditor
          key={parameter.key}
          onChange={(value) =>
            onUpsertModifier({
              ...modifier,
              parameters: {
                ...modifier.parameters,
                [parameter.key]: value
              }
            })
          }
          parameter={parameter}
          value={modifier.parameters[parameter.key]}
        />
      ))}
    </div>
  );
}

function ModifierParameterEditor({
  onChange,
  parameter,
  value
}: {
  onChange: (value: boolean | number | string) => void;
  parameter: ModifierParameterDefinition;
  value: unknown;
}) {
  const label = splitCamelCase(parameter.key);
  if (parameter.type === 'boolean') {
    return (
      <label className="as-dcc-field">
        <span>{label}</span>
        <input checked={typeof value === 'boolean' ? value : Boolean(parameter.defaultValue)} onChange={(event) => onChange(event.target.checked)} type="checkbox" />
      </label>
    );
  }

  if (parameter.type === 'select' && parameter.values?.length) {
    const currentValue = typeof value === 'string' ? value : String(parameter.defaultValue);
    return (
      <label className="as-dcc-field">
        <span>{label}</span>
        <select onChange={(event) => onChange(event.target.value)} value={currentValue}>
          {parameter.values.map((item) => (
            <option key={item} value={item}>
              {item}
            </option>
          ))}
        </select>
      </label>
    );
  }

  if (parameter.type === 'select') {
    return (
      <label className="as-dcc-field">
        <span>{label}</span>
        <input onChange={(event) => onChange(event.target.value)} value={typeof value === 'string' ? value : String(parameter.defaultValue)} />
      </label>
    );
  }

  return (
    <label className="as-dcc-field">
      <span>{label}</span>
      <input
        max={parameter.max}
        min={parameter.min}
        onChange={(event) => onChange(Number(event.target.value))}
        step={parameter.step ?? 0.1}
        type="number"
        value={typeof value === 'number' && Number.isFinite(value) ? Number(value.toFixed(3)) : Number(parameter.defaultValue)}
      />
    </label>
  );
}

function readActorModifierStack(document: SceneDocument, actor: SceneActor): SceneModifier[] {
  const stack = document.components.find((component) => actor.components.includes(component.id) && component.type === 'modifierStack');
  return stack ? readModifiers(stack) : [];
}

function readVector(value: unknown, fallback: SceneVector3): SceneVector3 {
  if (!value || typeof value !== 'object') {
    return fallback;
  }

  const record = value as Record<string, unknown>;
  return {
    x: typeof record.x === 'number' ? record.x : fallback.x,
    y: typeof record.y === 'number' ? record.y : fallback.y,
    z: typeof record.z === 'number' ? record.z : fallback.z
  };
}

function splitCamelCase(value: string): string {
  return value.replace(/([a-z])([A-Z])/g, '$1 $2');
}
