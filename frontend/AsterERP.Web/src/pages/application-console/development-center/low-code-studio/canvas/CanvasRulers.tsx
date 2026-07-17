import type { DesignerEditorSession } from '../document/DesignerEditorSession';

import { createDeviceCanvasFrame, documentToScreen } from './coordinateSystem';

interface CanvasRulersProps {
  session: DesignerEditorSession;
  stageSize: { height: number; width: number };
  text: (key: string) => string;
}

export function CanvasRulers({ session, stageSize, text }: CanvasRulersProps) {
  if (!session.canvas.rulersVisible) return null;
  const frame = createDeviceCanvasFrame({ height: session.viewport.height || stageSize.height, width: session.viewport.width || stageSize.width }, session.canvas.device);
  const viewport = { pan: session.viewport.pan ?? { x: 0, y: 0 }, zoom: session.viewport.zoom };
  const tick = Math.max(8, session.canvas.gridSize * 4);
  const horizontal = Array.from({ length: Math.ceil(stageSize.width / tick) + 1 }, (_, index) => index * tick);
  const vertical = Array.from({ length: Math.ceil(stageSize.height / tick) + 1 }, (_, index) => index * tick);
  return <div aria-label={text('canvasRulers')} className="page-studio__rulers" role="group">
    <div aria-label={text('horizontalRuler')} className="page-studio__ruler page-studio__ruler--horizontal">{horizontal.map((position) => <span className="page-studio__ruler-tick page-studio__ruler-tick--horizontal" key={`x-${position}`} style={{ left: documentToScreen({ x: position, y: 0 }, viewport, frame).x }}>{position}</span>)}</div>
    <div aria-label={text('verticalRuler')} className="page-studio__ruler page-studio__ruler--vertical">{vertical.map((position) => <span className="page-studio__ruler-tick page-studio__ruler-tick--vertical" key={`y-${position}`} style={{ top: documentToScreen({ x: 0, y: position }, viewport, frame).y }}>{position}</span>)}</div>
    <div className="page-studio__ruler-corner" />
  </div>;
}
