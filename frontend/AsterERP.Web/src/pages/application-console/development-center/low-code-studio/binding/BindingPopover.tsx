import { Link2, Unlink2 } from 'lucide-react';
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';

import { useI18n } from '../../../../../core/i18n/I18nProvider';
import type { ResourceRef } from '../document/ResourceRef';
import { resourceOptionToExpression } from '../expression/expressionCatalog';
import { normalizeValueType } from '../expression/expressionModel';
import type { BindingDocument, DesignerValueType, DesignerVariableExpression } from '../expression/expressionTypes';

import { resourceReferenceFor, type StableResourceReference } from './bindingTypes';
import { createConversionPipeline } from './conversionPipeline';
import { ResourceExplorer } from './ResourceExplorer';
import { listStableResources, type StableResourceUsage } from './resourceExplorerStore';
import { RESOURCE_DROP_EVENT } from './resourcePointerDrag';
import { scoreTypeCompatibility } from './typeCompatibility';

export interface BindingPopoverProps {
  document?: BindingDocument | null;
  expectedType: DesignerValueType;
  expression: DesignerVariableExpression | null;
  onBindValue?: (resource: ResourceRef) => void;
  onChange: (expression: DesignerVariableExpression | null) => void;
}

export function BindingPopover({ document, expectedType, expression, onBindValue, onChange }: BindingPopoverProps) {
  const { translate } = useI18n();
  const [open, setOpen] = useState(false);
  const [repairingUsage, setRepairingUsage] = useState<StableResourceUsage | null>(null);
  const selected = expression ? normalizeValueType(expression.expectedType) : null;
  const compatibility = useMemo(() => selected ? scoreTypeCompatibility(selected, expectedType) : null, [expectedType, selected]);
  const pipeline = useMemo(() => selected ? createConversionPipeline(selected, expectedType) : null, [expectedType, selected]);
  const dropTargetRef = useRef<HTMLDivElement>(null);

  const choose = useCallback((resource: StableResourceReference) => {
    const resourceCompatibility = scoreTypeCompatibility(resource.valueType, expectedType);
    const resourcePipeline = createConversionPipeline(resource.valueType, expectedType);
    if (resourceCompatibility.compatibility === 'incompatible' || !resourcePipeline.valid) {
      return;
    }

    const next = resourceOptionToExpression({
      ...resource,
      description: resource.path,
      groupId: resource.source,
      groupName: resource.source,
      id: resource.id,
      sourceName: resource.source
    });
    const graph = resourcePipeline.steps.length === 0
      ? { root: { kind: 'resourceRef' as const, resourceId: resource.id, valueType: resource.valueType } }
      : { root: { kind: 'conversion' as const, pipeline: resourcePipeline.steps, input: { kind: 'resourceRef' as const, resourceId: resource.id, valueType: resource.valueType }, valueType: expectedType } };
    const resourceRef: ResourceRef = resourceReferenceFor(resource, expectedType);
    if (onBindValue) onBindValue(resourceRef);
    else onChange({
      ...next,
      fallback: expression?.fallback ?? next.fallback,
      graph,
      conversionPipeline: resourcePipeline.steps,
      resourceId: resource.id
    } as DesignerVariableExpression);
    setOpen(false);
  }, [expectedType, expression, onBindValue, onChange]);

  useEffect(() => {
    const target = dropTargetRef.current;
    if (!target) return undefined;
    const handleResourceDrop = (event: Event) => {
      const resourceId = (event as CustomEvent<{ resourceId?: string }>).detail?.resourceId;
      if (!resourceId || !document) return;
      const resource = listStableResources(document).find((item) => item.id === resourceId);
      if (resource) {
        event.stopPropagation();
        choose(resource);
      }
    };
    target.addEventListener(RESOURCE_DROP_EVENT, handleResourceDrop);
    return () => target.removeEventListener(RESOURCE_DROP_EVENT, handleResourceDrop);
  }, [choose, document]);

  const blocked = Boolean(compatibility && (compatibility.compatibility === 'incompatible' || !pipeline?.valid));

  return (
    <div ref={dropTargetRef} data-resource-drop-target="true" className="page-studio__binding-control">
      <button type="button" aria-expanded={open} aria-label={translate(`lowCode.pageStudio.${expression ? 'bindingReplace' : 'bindingAdd'}`)} title={translate(`lowCode.pageStudio.${expression ? 'bindingReplace' : 'bindingAdd'}`)} onClick={() => setOpen((value) => !value)} className={`page-studio__binding-trigger ${expression ? 'is-bound' : ''}`}>
        <Link2 aria-hidden="true" className="h-3.5 w-3.5" />
      </button>
      {expression ? <button aria-label={translate('lowCode.pageStudio.bindingRemove')} className="page-studio__binding-unlink" title={translate('lowCode.pageStudio.bindingRemove')} type="button" onClick={() => { setOpen(false); onChange(null); }}><Unlink2 aria-hidden="true" className="h-3.5 w-3.5" /></button> : null}
      {expression && blocked ? <div role="alert" className="page-studio__binding-error">{translate('lowCode.pageStudio.bindingTypeIncompatible')}</div> : null}
      {open ? <div className="page-studio__binding-popover">{repairingUsage ? <p className="page-studio__binding-repair">{translate('lowCode.pageStudio.bindingRepairing')} {repairingUsage.path}，{translate('lowCode.pageStudio.bindingChooseReplacement')}</p> : null}<ResourceExplorer document={document} expectedType={expectedType} onRepairUsage={setRepairingUsage} onSelect={(resource) => { choose(resource); setRepairingUsage(null); }} /></div> : null}
    </div>
  );
}
