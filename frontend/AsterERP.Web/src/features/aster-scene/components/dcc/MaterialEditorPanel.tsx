import { Cable, PaintBucket } from 'lucide-react';

import type { SceneDocument, SceneMaterial, SceneMaterialPbrPatch, ScenePbrTextureSlot, SceneTextureBinding, SceneUvTransform } from '../../model/types';

interface MaterialEditorPanelProps {
  document: SceneDocument;
  onAssignMaterial: (materialId: string) => void;
  onCreateMaterial: () => void;
  onUpdateMaterialColor: (materialId: string, value: string | SceneMaterialPbrPatch) => void;
  selectedMaterialId: string | null;
  t: (key: string) => string;
}

export function MaterialEditorPanel({ document, onAssignMaterial, onCreateMaterial, onUpdateMaterialColor, selectedMaterialId, t }: MaterialEditorPanelProps) {
  const textureAssets = document.assets.filter((asset) => ['hdri', 'image', 'texture'].includes(asset.kind.toLowerCase()));
  return (
    <section className="as-dcc-material-panel">
      <header>
        <h2>{t('asterscene.dcc.materialEditor')}</h2>
        <button className="as-dcc-small-button" onClick={onCreateMaterial} type="button">
          <PaintBucket size={14} /> {t('asterscene.dcc.material.new')}
        </button>
      </header>
      <div className="as-dcc-material-slots">
        {document.materials.map((material) => (
          <MaterialSlot
            key={material.id}
            material={material}
            onAssignMaterial={onAssignMaterial}
            onUpdateMaterialColor={onUpdateMaterialColor}
            selected={material.id === selectedMaterialId}
            t={t}
            textureAssets={textureAssets}
          />
        ))}
      </div>
      <div className="as-dcc-node-graph">
        <div>
          <Cable size={16} />
          <span>{t('asterscene.dcc.material.slate')}</span>
        </div>
        <div className="as-dcc-node">PBR</div>
        <div className="as-dcc-node">Bitmap</div>
        <div className="as-dcc-node">Output</div>
      </div>
    </section>
  );
}

function MaterialSlot({
  material,
  onAssignMaterial,
  onUpdateMaterialColor,
  selected,
  t,
  textureAssets
}: {
  material: SceneMaterial;
  onAssignMaterial: (materialId: string) => void;
  onUpdateMaterialColor: (materialId: string, value: string | SceneMaterialPbrPatch) => void;
  selected: boolean;
  t: (key: string) => string;
  textureAssets: SceneDocument['assets'];
}) {
  const color = readColor(material.pbr?.baseColor ?? material.baseColor);
  const emissive = readColor(material.pbr?.emissive ?? material.emissive, '#000000');
  const uvTransform = readUvTransform(material.pbr?.uvTransform);
  return (
    <div className={selected ? 'as-dcc-material-slot is-active' : 'as-dcc-material-slot'}>
      <button onClick={() => onAssignMaterial(material.id)} type="button">
        <span style={{ background: color }} />
        <strong>{material.name}</strong>
      </button>
      <label>
        {t('asterscene.dcc.material.baseColor')}
        <input onChange={(event) => onUpdateMaterialColor(material.id, event.target.value)} type="color" value={color} />
      </label>
      <label>
        Emissive
        <input onChange={(event) => onUpdateMaterialColor(material.id, { emissive: event.target.value })} type="color" value={emissive} />
      </label>
      <NumberField
        label="Metallic"
        max={1}
        min={0}
        onChange={(value) => onUpdateMaterialColor(material.id, { metallic: value })}
        step={0.01}
        value={readNumber(material.pbr?.metallic ?? material.metallic, 0)}
      />
      <NumberField
        label="Roughness"
        max={1}
        min={0}
        onChange={(value) => onUpdateMaterialColor(material.id, { roughness: value })}
        step={0.01}
        value={readNumber(material.pbr?.roughness ?? material.roughness, 0.68)}
      />
      <NumberField
        label="Opacity"
        max={1}
        min={0}
        onChange={(value) => onUpdateMaterialColor(material.id, { opacity: value, alphaMode: value < 1 ? 'blend' : 'opaque' })}
        step={0.01}
        value={readNumber(material.pbr?.opacity ?? material.opacity, 1)}
      />
      <label>
        Double sided
        <input
          checked={material.pbr?.doubleSided === true}
          onChange={(event) => onUpdateMaterialColor(material.id, { doubleSided: event.target.checked })}
          type="checkbox"
        />
      </label>
      <div className="as-dcc-section-title">Texture slots</div>
      {textureSlotKeys.map((slot) => (
        <TextureSlotEditor
          binding={material.pbr?.textureSlots?.[slot]}
          key={slot}
          label={slotLabels[slot]}
          onChange={(binding) => onUpdateMaterialColor(material.id, { textureSlots: { [slot]: binding } })}
          textureAssets={textureAssets}
        />
      ))}
      <div className="as-dcc-section-title">UV transform</div>
      <UvTransformEditor
        onChange={(nextTransform) => onUpdateMaterialColor(material.id, { uvTransform: nextTransform })}
        value={uvTransform}
      />
    </div>
  );
}

const textureSlotKeys: ScenePbrTextureSlot[] = ['baseColor', 'normal', 'metallicRoughness', 'ao', 'emissive', 'opacity'];
const slotLabels: Record<ScenePbrTextureSlot, string> = {
  ao: 'AO',
  baseColor: 'Base color',
  emissive: 'Emissive',
  metallicRoughness: 'Metallic/Roughness',
  normal: 'Normal',
  opacity: 'Opacity'
};

function TextureSlotEditor({
  binding,
  label,
  onChange,
  textureAssets
}: {
  binding?: SceneTextureBinding;
  label: string;
  onChange: (binding: SceneTextureBinding | null) => void;
  textureAssets: SceneDocument['assets'];
}) {
  return (
    <div className="as-dcc-field">
      <span>{label}</span>
      <select
        onChange={(event) => {
          const asset = textureAssets.find((item) => item.id === event.target.value);
          onChange(asset ? { assetId: asset.id, url: asset.url } : readBindingUrl(binding) ? { url: readBindingUrl(binding) } : null);
        }}
        value={binding?.assetId ?? ''}
      >
        <option value="">None</option>
        {textureAssets.map((asset) => (
          <option key={asset.id} value={asset.id}>
            {asset.id}
          </option>
        ))}
      </select>
      <input
        onChange={(event) => {
          const url = event.target.value.trim();
          onChange(url || binding?.assetId ? { assetId: binding?.assetId, url: url || undefined } : null);
        }}
        placeholder="/uploads/texture.png"
        value={readBindingUrl(binding)}
      />
    </div>
  );
}

function UvTransformEditor({ onChange, value }: { onChange: (value: SceneUvTransform) => void; value: Required<SceneUvTransform> }) {
  return (
    <>
      <Vector2Field
        label="Offset"
        onChange={(offset) => onChange({ ...value, offset })}
        step={0.01}
        value={value.offset}
      />
      <Vector2Field
        label="Repeat"
        min={0.01}
        onChange={(repeat) => onChange({ ...value, repeat })}
        step={0.01}
        value={value.repeat}
      />
      <NumberField
        label="Rotation"
        onChange={(rotation) => onChange({ ...value, rotation })}
        step={1}
        value={value.rotation}
      />
    </>
  );
}

function Vector2Field({
  label,
  min,
  onChange,
  step,
  value
}: {
  label: string;
  min?: number;
  onChange: (value: [number, number]) => void;
  step: number;
  value: [number, number];
}) {
  return (
    <label>
      {label}
      <div className="as-vector-inputs">
        {[0, 1].map((index) => (
          <input
            key={index}
            min={min}
            onChange={(event) => {
              const next = [...value] as [number, number];
              next[index] = Number(event.target.value);
              onChange(next);
            }}
            step={step}
            type="number"
            value={Number(value[index].toFixed(3))}
          />
        ))}
      </div>
    </label>
  );
}

function NumberField({
  label,
  max,
  min,
  onChange,
  step,
  value
}: {
  label: string;
  max?: number;
  min?: number;
  onChange: (value: number) => void;
  step: number;
  value: number;
}) {
  return (
    <label>
      {label}
      <input
        max={max}
        min={min}
        onChange={(event) => onChange(Number(event.target.value))}
        step={step}
        type="number"
        value={Number(value.toFixed(3))}
      />
    </label>
  );
}

function readColor(value: unknown, fallback = '#8a9099'): string {
  return typeof value === 'string' && /^#[0-9a-f]{6}$/i.test(value) ? value : fallback;
}

function readNumber(value: unknown, fallback: number): number {
  return typeof value === 'number' && Number.isFinite(value) ? value : fallback;
}

function readBindingUrl(binding?: SceneTextureBinding): string {
  return typeof binding?.url === 'string' ? binding.url : '';
}

function readUvTransform(value: SceneUvTransform | undefined): Required<SceneUvTransform> {
  return {
    offset: Array.isArray(value?.offset) ? value.offset : [0, 0],
    repeat: Array.isArray(value?.repeat) ? value.repeat : [1, 1],
    rotation: readNumber(value?.rotation, 0)
  };
}
