import type { ReactNode } from 'react';

import type { RuntimeComponentRenderContext } from './RuntimeComponentTypes';
import { applyRuntimeNodePresentation } from './RuntimeNodePresentation';

export function hasButtonRuntimeRenderer(type: string): boolean {
  return type === 'action.button' || type === 'action.submitButton' || type === 'action.resetButton' || type === 'action.imageButton';
}

export function renderButtonRuntime(context: RuntimeComponentRenderContext): ReactNode {
  const type = context.componentType === 'action.submitButton' ? 'submit' : context.componentType === 'action.resetButton' ? 'reset' : context.props.buttonType === 'submit' || context.props.buttonType === 'reset' ? context.props.buttonType : 'button';
  const title = context.value === null || context.value === undefined || context.value === ''
    ? String(context.props.text ?? context.props.label ?? context.title)
    : String(context.value);
  return applyRuntimeNodePresentation(context, <button className={context.props.variant === 'primary' ? 'primary-button h-9' : 'secondary-button h-9'} disabled={context.disabled || context.readOnly} type={type} onClick={(event) => { if (type === 'button') event.preventDefault(); if (context.action) void context.executeAction(context.action, context.runtime); }}>{title}</button>);
}
