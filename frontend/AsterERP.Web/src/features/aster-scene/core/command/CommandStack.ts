export interface AsterSceneCommand<TState> {
  id: string;
  label: string;
  payload?: unknown;
  redo: (state: TState) => TState;
  timestamp: number;
  undo: (state: TState) => TState;
}

export interface AsterSceneTransaction<TState> {
  commands: AsterSceneCommand<TState>[];
  id: string;
  label: string;
  timestamp: number;
}

export class CommandStack<TState> {
  private readonly redoStack: AsterSceneTransaction<TState>[] = [];
  private readonly undoStack: AsterSceneTransaction<TState>[] = [];

  public constructor(private readonly maxDepth = 200) {}

  public canRedo(): boolean {
    return this.redoStack.length > 0;
  }

  public canUndo(): boolean {
    return this.undoStack.length > 0;
  }

  public clear(): void {
    this.redoStack.length = 0;
    this.undoStack.length = 0;
  }

  public execute(state: TState, transaction: AsterSceneTransaction<TState>): TState {
    const next = transaction.commands.reduce((current, command) => command.redo(current), state);
    this.undoStack.push(transaction);
    if (this.undoStack.length > this.maxDepth) {
      this.undoStack.shift();
    }

    this.redoStack.length = 0;
    return next;
  }

  public redo(state: TState): TState {
    const transaction = this.redoStack.pop();
    if (!transaction) {
      return state;
    }

    const next = transaction.commands.reduce((current, command) => command.redo(current), state);
    this.undoStack.push(transaction);
    return next;
  }

  public undo(state: TState): TState {
    const transaction = this.undoStack.pop();
    if (!transaction) {
      return state;
    }

    const next = [...transaction.commands].reverse().reduce((current, command) => command.undo(current), state);
    this.redoStack.push(transaction);
    return next;
  }

  public snapshot(): { redo: AsterSceneTransaction<TState>[]; undo: AsterSceneTransaction<TState>[] } {
    return {
      redo: [...this.redoStack],
      undo: [...this.undoStack]
    };
  }
}

export function createTransaction<TState>(
  label: string,
  commands: AsterSceneCommand<TState>[]
): AsterSceneTransaction<TState> {
  return {
    commands,
    id: `txn_${crypto.randomUUID().replaceAll('-', '')}`,
    label,
    timestamp: Date.now()
  };
}
