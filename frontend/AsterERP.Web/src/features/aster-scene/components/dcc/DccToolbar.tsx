import { Copy, Eye, EyeOff, Grid2X2, History, Magnet, MousePointer2, Move3D, Play, Redo2, Rotate3D, Save, Scale3D, Trash2, Undo2 } from 'lucide-react';

import { PermissionButton } from '@/shared/auth/PermissionButton';

import { asterScenePermissions } from '../../model/permissions';
import type { SceneTransformMode } from '../../model/types';
import { useAsterSceneEditorStore } from '../../state/editorStore';

interface DccToolbarProps {
  canSave: boolean;
  canPublish: boolean;
  hasSelection: boolean;
  onDelete: () => void;
  onDuplicate: () => void;
  onPublish: () => void;
  onOpenVersions: () => void;
  onSave: () => void;
  onToggleHidden: () => void;
  onUndo: () => void;
  onRedo: () => void;
  publishing: boolean;
  saving: boolean;
  t: (key: string) => string;
}

export function DccToolbar({
  canPublish,
  canSave,
  hasSelection,
  onDelete,
  onDuplicate,
  onPublish,
  onOpenVersions,
  onRedo,
  onSave,
  onToggleHidden,
  onUndo,
  publishing,
  saving,
  t
}: DccToolbarProps) {
  const { setSnapEnabled, setTransformMode, setTransformSpace, setViewportLayout, snapEnabled, transformMode, transformSpace, viewportLayout } = useAsterSceneEditorStore();

  return (
    <header className="as-dcc-toolbar">
      <div className="as-dcc-toolbar__menu">
        <button type="button">{t('asterscene.dcc.menu.file')}</button>
        <button type="button">{t('asterscene.dcc.menu.edit')}</button>
        <button type="button">{t('asterscene.dcc.menu.create')}</button>
        <button type="button">{t('asterscene.dcc.menu.modify')}</button>
        <button type="button">{t('asterscene.dcc.menu.animation')}</button>
        <button type="button">{t('asterscene.dcc.menu.render')}</button>
      </div>
      <div className="as-dcc-toolbar__row">
        <button className="as-icon-button" onClick={onUndo} title={t('asterscene.studio.undo')} type="button">
          <Undo2 size={16} />
        </button>
        <button className="as-icon-button" onClick={onRedo} title={t('asterscene.studio.redo')} type="button">
          <Redo2 size={16} />
        </button>
        <ToolButton active={transformMode === 'translate'} label={t('asterscene.dcc.tool.selectMove')} mode="translate" setTransformMode={setTransformMode} />
        <ToolButton active={transformMode === 'rotate'} label={t('asterscene.dcc.tool.rotate')} mode="rotate" setTransformMode={setTransformMode} />
        <ToolButton active={transformMode === 'scale'} label={t('asterscene.dcc.tool.scale')} mode="scale" setTransformMode={setTransformMode} />
        <button className={transformSpace === 'local' ? 'as-dcc-tool is-active' : 'as-dcc-tool'} onClick={() => setTransformSpace(transformSpace === 'local' ? 'world' : 'local')} type="button">
          <MousePointer2 size={16} /> {transformSpace === 'local' ? t('asterscene.dcc.space.local') : t('asterscene.dcc.space.world')}
        </button>
        <button className={snapEnabled ? 'as-dcc-tool is-active' : 'as-dcc-tool'} onClick={() => setSnapEnabled(!snapEnabled)} type="button">
          <Magnet size={16} /> {t('asterscene.dcc.tool.snap')}
        </button>
        <button className={viewportLayout === 'quad' ? 'as-dcc-tool is-active' : 'as-dcc-tool'} onClick={() => setViewportLayout(viewportLayout === 'quad' ? 'single' : 'quad')} type="button">
          <Grid2X2 size={16} /> {t('asterscene.dcc.tool.viewport')}
        </button>
        <button className="as-dcc-tool" disabled={!hasSelection} onClick={onDuplicate} type="button">
          <Copy size={16} /> {t('asterscene.dcc.tool.duplicate')}
        </button>
        <button className="as-dcc-tool" disabled={!hasSelection} onClick={onToggleHidden} type="button">
          {hasSelection ? <EyeOff size={16} /> : <Eye size={16} />} {t('asterscene.dcc.tool.hide')}
        </button>
        <button className="as-dcc-tool" disabled={!hasSelection} onClick={onDelete} type="button">
          <Trash2 size={16} /> {t('asterscene.dcc.tool.delete')}
        </button>
        <PermissionButton
          className="as-button"
          code={asterScenePermissions.documentSave}
          disabled={!canSave || saving}
          iconStart={false}
          onClick={onSave}
          type="button"
        >
          <Save size={16} /> {t('common.save')}
        </PermissionButton>
        <PermissionButton
          className="as-button as-button--primary"
          code={asterScenePermissions.publishExecute}
          disabled={!canPublish || publishing}
          iconStart={false}
          onClick={onPublish}
          type="button"
        >
          <Play size={16} /> {t('asterscene.studio.publish')}
        </PermissionButton>
        <PermissionButton
          className="as-button"
          code={asterScenePermissions.publishView}
          iconStart={false}
          onClick={onOpenVersions}
          type="button"
        >
          <History size={16} /> {t('asterscene.studio.versions')}
        </PermissionButton>
      </div>
    </header>
  );
}

function ToolButton({
  active,
  label,
  mode,
  setTransformMode
}: {
  active: boolean;
  label: string;
  mode: SceneTransformMode;
  setTransformMode: (mode: SceneTransformMode) => void;
}) {
  const Icon = mode === 'translate' ? Move3D : mode === 'rotate' ? Rotate3D : Scale3D;
  return (
    <button className={active ? 'as-dcc-tool is-active' : 'as-dcc-tool'} onClick={() => setTransformMode(mode)} type="button">
      <Icon size={16} /> {label}
    </button>
  );
}
