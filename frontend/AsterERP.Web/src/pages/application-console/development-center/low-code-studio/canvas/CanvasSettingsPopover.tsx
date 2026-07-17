import { Settings2, X } from 'lucide-react';
import { useEffect, useRef, useState } from 'react';

import { useI18n } from '../../../../../core/i18n/I18nProvider';
import type { DesignerEditorSession } from '../document/DesignerEditorSession';
import { createResponsivePreviewViewport, DEFAULT_DEVICE_PROFILES, DEFAULT_RESPONSIVE_BREAKPOINTS, toDesignerDeviceSession } from '../responsive/deviceProfiles';
import type { DesignerEditorSessionStore } from '../session/DesignerEditorSessionStore';

import { CanvasGuideEditor } from './CanvasGuideEditor';

interface CanvasSettingsPopoverProps {
  session: DesignerEditorSession;
  sessionStore: DesignerEditorSessionStore;
}

export function CanvasSettingsPopover({ session, sessionStore }: CanvasSettingsPopoverProps) {
  const { translate } = useI18n();
  const [open, setOpen] = useState(false);
  const rootRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);
  useEffect(() => {
    if (!open) return undefined;
    const closeFromPointer = (event: PointerEvent) => {
      if (rootRef.current?.contains(event.target as Node)) return;
      setOpen(false);
    };
    const closeFromKeyboard = (event: KeyboardEvent) => {
      if (event.key !== 'Escape') return;
      event.preventDefault();
      event.stopPropagation();
      setOpen(false);
      window.requestAnimationFrame(() => triggerRef.current?.focus());
    };
    window.addEventListener('pointerdown', closeFromPointer, true);
    window.addEventListener('keydown', closeFromKeyboard, true);
    return () => {
      window.removeEventListener('pointerdown', closeFromPointer, true);
      window.removeEventListener('keydown', closeFromKeyboard, true);
    };
  }, [open]);
  const patchCanvas = (patch: Partial<DesignerEditorSession['canvas']>) => sessionStore.patch({ canvas: patch });
  const selectDevice = (profileId: string) => {
    if (profileId === 'custom') {
      patchCanvas({ device: { browserBar: { bottom: 0, top: 0 }, breakpointId: 'mobile', height: 844, id: 'custom', orientation: 'portrait', pixelRatio: 1, safeArea: { bottom: 0, left: 0, right: 0, top: 0 }, width: 390 } });
      sessionStore.patch({ viewport: { width: 390, height: 844 } });
      return;
    }
    const profile = DEFAULT_DEVICE_PROFILES.find((item) => item.id === profileId);
    if (!profile) return patchCanvas({ device: null });
    const viewport = createResponsivePreviewViewport(profile, DEFAULT_RESPONSIVE_BREAKPOINTS);
    patchCanvas({ device: toDesignerDeviceSession(profile, viewport.breakpoint.id) });
    sessionStore.patch({ viewport: { width: profile.width, height: profile.height } });
  };
  const updateDevice = (patch: Partial<NonNullable<DesignerEditorSession['canvas']['device']>>) => {
    if (!session.canvas.device) return;
    const current = session.canvas.device;
    const next = { ...current, ...patch, safeArea: patch.orientation && patch.orientation !== current.orientation ? rotateSafeArea(current.safeArea) : patch.safeArea ?? current.safeArea };
    const breakpointId = patch.width || patch.orientation ? resolveBreakpointId(next) : next.breakpointId;
    patchCanvas({ device: { ...next, breakpointId } });
    sessionStore.patch({ viewport: { width: next.width, height: next.height } });
  };
  return <div ref={rootRef} className="relative" data-canvas-settings="true" data-canvas-interaction-control="true">
    <button ref={triggerRef} aria-expanded={open} aria-haspopup="dialog" aria-label={translate('lowCode.pageStudio.canvasSettings')} className="secondary-button h-8" type="button" onClick={() => setOpen((current) => !current)}><Settings2 aria-hidden="true" className="h-4 w-4" />{translate('lowCode.pageStudio.view')}</button>
    {open ? <div aria-label={translate('lowCode.pageStudio.canvasSettings')} className="page-studio__popover space-y-3" role="dialog">
      <div className="flex items-center justify-between"><h2 className="font-semibold text-[color:var(--app-text)]">{translate('lowCode.pageStudio.canvasSettings')}</h2><button aria-label={translate('lowCode.pageStudio.closeCanvasSettings')} className="icon-button h-7 w-7" type="button" onClick={() => { setOpen(false); window.requestAnimationFrame(() => triggerRef.current?.focus()); }}><X aria-hidden="true" className="h-3.5 w-3.5" /></button></div>
      <label className="flex items-center justify-between gap-2"><span>{translate('lowCode.pageStudio.device')}</span><select aria-label={translate('lowCode.pageStudio.deviceProfile')} className="form-input h-8 flex-1" value={session.canvas.device?.id ?? ''} onChange={(event) => selectDevice(event.target.value)}><option value="">{translate('lowCode.pageStudio.editorCanvas')}</option>{DEFAULT_DEVICE_PROFILES.map((profile) => <option key={profile.id} value={profile.id}>{profile.name}</option>)}<option value="custom">{translate('lowCode.pageStudio.custom')}</option></select></label>
      {session.canvas.device ? <div className="grid grid-cols-2 gap-2"><label>{translate('lowCode.pageStudio.width')}<input aria-label={translate('lowCode.pageStudio.deviceWidth')} className="form-input h-7 w-full" min={1} type="number" value={session.canvas.device.width} onChange={(event) => updateDevice({ width: Number(event.target.value), id: 'custom' })} /></label><label>{translate('lowCode.pageStudio.height')}<input aria-label={translate('lowCode.pageStudio.deviceHeight')} className="form-input h-7 w-full" min={1} type="number" value={session.canvas.device.height} onChange={(event) => updateDevice({ height: Number(event.target.value), id: 'custom' })} /></label><button aria-label={translate('lowCode.pageStudio.toggleOrientation')} className="secondary-button col-span-2 h-7" type="button" onClick={() => updateDevice({ orientation: session.canvas.device?.orientation === 'portrait' ? 'landscape' : 'portrait', width: session.canvas.device?.height, height: session.canvas.device?.width })}>{translate(`lowCode.pageStudio.${session.canvas.device.orientation}`)}</button></div> : null}
      <div className="grid grid-cols-2 gap-2"><label className="flex items-center gap-2"><input aria-label={translate('lowCode.pageStudio.showRulers')} checked={session.canvas.rulersVisible} type="checkbox" onChange={(event) => patchCanvas({ rulersVisible: event.target.checked })} />{translate('lowCode.pageStudio.rulers')}</label><label className="flex items-center gap-2"><input aria-label={translate('lowCode.pageStudio.showGrid')} checked={session.canvas.gridVisible} type="checkbox" onChange={(event) => patchCanvas({ gridVisible: event.target.checked })} />{translate('lowCode.pageStudio.grid')}</label><label className="flex items-center gap-2"><input aria-label={translate('lowCode.pageStudio.showMinimap')} checked={session.canvas.minimapVisible} type="checkbox" onChange={(event) => patchCanvas({ minimapVisible: event.target.checked })} />{translate('lowCode.pageStudio.minimap')}</label><label className="flex items-center gap-2"><input aria-label={translate('lowCode.pageStudio.handTool')} checked={session.canvas.tool === 'hand'} type="checkbox" onChange={(event) => patchCanvas({ tool: event.target.checked ? 'hand' : 'select' })} />{translate('lowCode.pageStudio.handTool')} (H)</label><label>{translate('lowCode.pageStudio.gridSize')}<select aria-label={translate('lowCode.pageStudio.gridSize')} className="form-input h-7 w-full" value={session.canvas.gridSize} onChange={(event) => patchCanvas({ gridSize: Number(event.target.value) })}><option value="4">4</option><option value="8">8</option><option value="16">16</option><option value="24">24</option><option value="32">32</option></select></label><label>{translate('lowCode.pageStudio.snapThreshold')}<input aria-label={translate('lowCode.pageStudio.snapThreshold')} className="form-input h-7 w-full" max={64} min={0} type="number" value={session.canvas.snapThreshold} onChange={(event) => patchCanvas({ snapThreshold: Number(event.target.value) })} /></label></div>
      <CanvasGuideEditor guides={session.canvas.guides} text={(key) => translate(`lowCode.pageStudio.${key}`)} onChange={(guides) => patchCanvas({ guides })} />
    </div> : null}
  </div>;
}

function resolveBreakpointId(device: { height: number; orientation: 'portrait' | 'landscape'; width: number }): string | null {
  try { return createResponsivePreviewViewport({ id: 'custom', name: 'Custom', width: device.width, height: device.height, pixelRatio: 1, orientation: device.orientation, safeArea: { bottom: 0, left: 0, right: 0, top: 0 } }, DEFAULT_RESPONSIVE_BREAKPOINTS).breakpoint.id; } catch { return null; }
}

function rotateSafeArea(safeArea: NonNullable<DesignerEditorSession['canvas']['device']>['safeArea']) { return { bottom: safeArea.left, left: safeArea.top, right: safeArea.bottom, top: safeArea.right }; }
