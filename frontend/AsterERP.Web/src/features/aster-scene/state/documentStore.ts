import { create } from 'zustand';

import { CommandStack, createTransaction, type AsterSceneCommand } from '../core/command/CommandStack';
import { normalizeSceneDocument } from '../core/scene-document/documentKernel';
import type { AsterSceneProject, SceneDocument } from '../model/types';

interface DocumentState {
  commandStack: CommandStack<SceneDocument>;
  dirty: boolean;
  document: SceneDocument | null;
  documentHash: string;
  project: AsterSceneProject | null;
  revision: number;
  applyCommand: (command: AsterSceneCommand<SceneDocument>) => void;
  markSaved: (revision: number, documentHash: string) => void;
  redo: () => void;
  reset: (payload: { document: SceneDocument; documentHash: string; project: AsterSceneProject; revision: number }) => void;
  undo: () => void;
}

export const useAsterSceneDocumentStore = create<DocumentState>((set, get) => ({
  commandStack: new CommandStack<SceneDocument>(),
  dirty: false,
  document: null,
  documentHash: '',
  project: null,
  revision: 0,
  applyCommand: (command) => {
    const current = get().document;
    if (!current) {
      return;
    }

    const transaction = createTransaction(command.label, [command]);
    const next = normalizeSceneDocument(get().commandStack.execute(current, transaction));
    set({ dirty: true, document: next });
  },
  markSaved: (revision, documentHash) => set({ dirty: false, documentHash, revision }),
  redo: () => {
    const current = get().document;
    if (!current) {
      return;
    }

    set({ dirty: true, document: get().commandStack.redo(current) });
  },
  reset: (payload) => {
    const stack = new CommandStack<SceneDocument>();
    set({
      commandStack: stack,
      dirty: false,
      document: normalizeSceneDocument(payload.document),
      documentHash: payload.documentHash,
      project: payload.project,
      revision: payload.revision
    });
  },
  undo: () => {
    const current = get().document;
    if (!current) {
      return;
    }

    set({ dirty: true, document: get().commandStack.undo(current) });
  }
}));
