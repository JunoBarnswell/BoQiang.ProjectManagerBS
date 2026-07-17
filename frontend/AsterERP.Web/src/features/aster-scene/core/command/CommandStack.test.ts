import { describe, expect, it } from 'vitest';

import { CommandStack, createTransaction, type AsterSceneCommand } from './CommandStack';

function add(delta: number): AsterSceneCommand<number> {
  return {
    id: `add-${delta}`,
    label: `Add ${delta}`,
    redo: (state) => state + delta,
    timestamp: 1,
    undo: (state) => state - delta
  };
}

describe('CommandStack', () => {
  it('executes transactions and restores them with undo and redo', () => {
    const stack = new CommandStack<number>();
    const transaction = createTransaction('Add sequence', [add(2), add(3)]);

    const executed = stack.execute(1, transaction);
    const undone = stack.undo(executed);
    const redone = stack.redo(undone);

    expect(executed).toBe(6);
    expect(undone).toBe(1);
    expect(redone).toBe(6);
    expect(stack.canUndo()).toBe(true);
    expect(stack.canRedo()).toBe(false);
  });

  it('clears redo history after a new transaction', () => {
    const stack = new CommandStack<number>();
    const first = createTransaction('First', [add(1)]);
    const second = createTransaction('Second', [add(4)]);

    const executed = stack.execute(0, first);
    const undone = stack.undo(executed);
    const next = stack.execute(undone, second);

    expect(next).toBe(4);
    expect(stack.canRedo()).toBe(false);
  });

  it('bounds undo history by max depth', () => {
    const stack = new CommandStack<number>(1);

    const first = stack.execute(0, createTransaction('First', [add(1)]));
    const second = stack.execute(first, createTransaction('Second', [add(2)]));
    const undone = stack.undo(second);

    expect(second).toBe(3);
    expect(undone).toBe(1);
    expect(stack.canUndo()).toBe(false);
  });
});
