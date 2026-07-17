export type ShortcutAction = 'copy' | 'paste' | 'duplicate' | 'select-all' | 'delete' | 'escape' | 'undo' | 'redo' | 'nudge-left' | 'nudge-right' | 'nudge-up' | 'nudge-down' | 'zoom-in' | 'zoom-out' | 'fit-page' | 'fit-selection';
export interface ShortcutInput { key: string; ctrlKey?: boolean; metaKey?: boolean; shiftKey?: boolean; altKey?: boolean }
export interface ShortcutBinding { action: ShortcutAction; key: string; ctrlOrMeta?: boolean; shift?: boolean; alt?: boolean }

export const DEFAULT_SHORTCUT_BINDINGS: readonly ShortcutBinding[] = [
  { action: 'copy', key: 'c', ctrlOrMeta: true }, { action: 'paste', key: 'v', ctrlOrMeta: true }, { action: 'duplicate', key: 'd', ctrlOrMeta: true },
  { action: 'select-all', key: 'a', ctrlOrMeta: true }, { action: 'undo', key: 'z', ctrlOrMeta: true }, { action: 'redo', key: 'z', ctrlOrMeta: true, shift: true }, { action: 'redo', key: 'y', ctrlOrMeta: true },
  { action: 'escape', key: 'Escape' }, { action: 'delete', key: 'Delete' }, { action: 'delete', key: 'Backspace' },
  { action: 'zoom-in', key: '=' }, { action: 'zoom-in', key: '+' }, { action: 'zoom-out', key: '-' }, { action: 'fit-page', key: '0' }, { action: 'fit-selection', key: '0', shift: true },
  { action: 'nudge-left', key: 'ArrowLeft' }, { action: 'nudge-right', key: 'ArrowRight' }, { action: 'nudge-up', key: 'ArrowUp' }, { action: 'nudge-down', key: 'ArrowDown' }
];

function normalizeKey(key: string): string { return key.length === 1 ? key.toLowerCase() : key.toLowerCase(); }

function bindingMatches(input: ShortcutInput, binding: ShortcutBinding): boolean {
  const commandPressed = Boolean(input.ctrlKey || input.metaKey);
  return normalizeKey(input.key) === normalizeKey(binding.key)
    && commandPressed === Boolean(binding.ctrlOrMeta)
    && Boolean(input.shiftKey) === Boolean(binding.shift)
    && Boolean(input.altKey) === Boolean(binding.alt);
}

export function resolveShortcut(input: ShortcutInput, bindings: readonly ShortcutBinding[] = DEFAULT_SHORTCUT_BINDINGS): ShortcutAction | null {
  return bindings.find((binding) => bindingMatches(input, binding))?.action ?? null;
}

export function findShortcutConflicts(bindings: readonly ShortcutBinding[]): readonly (readonly [string, readonly ShortcutAction[]])[] {
  const actionsBySignature = new Map<string, ShortcutAction[]>();
  for (const binding of bindings) {
    const signature = `${normalizeKey(binding.key)}|${Boolean(binding.ctrlOrMeta)}|${Boolean(binding.shift)}|${Boolean(binding.alt)}`;
    const actions = actionsBySignature.get(signature) ?? [];
    if (!actions.includes(binding.action)) actions.push(binding.action);
    actionsBySignature.set(signature, actions);
  }
  return [...actionsBySignature.entries()].filter(([, actions]) => actions.length > 1);
}

export function nudgeDistance(input: ShortcutInput): number { return input.shiftKey ? 10 : 1; }
