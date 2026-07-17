import type { DesignerGuide } from '../document/DesignerEditorSession';

interface CanvasGuideEditorProps {
  guides: readonly DesignerGuide[];
  onChange: (guides: readonly DesignerGuide[]) => void;
  text: (key: string) => string;
}

export function CanvasGuideEditor({ guides, onChange, text }: CanvasGuideEditorProps) {
  const addGuide = (axis: DesignerGuide['axis']) => onChange([...guides, { axis, id: `${axis}-${Date.now()}`, position: 160 }]);
  const updateGuide = (id: string, position: number) => onChange(guides.map((guide) => guide.id === id ? { ...guide, position: Math.max(0, position) } : guide));
  const removeGuide = (id: string) => onChange(guides.filter((guide) => guide.id !== id));
  return <section aria-label={text('guides')} className="page-studio__guide-editor"><div className="page-studio__guide-header"><h3>{text('guides')}</h3><div className="page-studio__guide-actions"><button aria-label={text('addVerticalGuide')} className="page-studio__guide-button" type="button" onClick={() => addGuide('x')}>+ V</button><button aria-label={text('addHorizontalGuide')} className="page-studio__guide-button" type="button" onClick={() => addGuide('y')}>+ H</button></div></div>{guides.length === 0 ? <p className="page-studio__guide-empty">{text('noGuides')}</p> : guides.map((guide) => <div className="page-studio__guide-row" key={guide.id}><span className="page-studio__guide-axis">{guide.axis === 'x' ? 'V' : 'H'}</span><input aria-label={`${text('guidePosition')} ${guide.id}`} className="form-input h-7 min-w-0 flex-1" min={0} type="number" value={Math.round(guide.position)} onChange={(event) => updateGuide(guide.id, Number(event.target.value))} /><button aria-label={`${text('deleteGuide')} ${guide.id}`} className="page-studio__guide-delete" type="button" onClick={() => removeGuide(guide.id)}>×</button></div>)}</section>;
}
