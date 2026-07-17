# Phase 1 command history boundary

## Root cause

The latest command path previously captured the complete `DesignerDocument` inside inverse commands. The same behavior existed for merged edits and `DesignerDocumentStore.executeTransaction`. This made Undo/Redo memory proportional to the entire page, and an inverse could replace unrelated changes instead of reporting a conflict.

## Current contract

`DesignerDocumentPatch` contains only changed element records and changed content fields. `revision` and `documentHash` are deliberately excluded because the store recalculates them when committing. Each patch stores both the expected value and the replacement value. Applying an inverse first verifies every expected node and field; any mismatch returns diagnostics without publishing a candidate document.

Single commands, merged continuous edits, explicit transactions, and redo are all represented in history by a forward patch and an inverse patch. A command-provided `DesignerPatchCommand` inverse is preserved so stricter expected-value guards are not discarded; commands without one receive a canonical inverse generated from before/after. Redo applies the stored forward patch and never re-executes the original command. The document store validates the complete latest document before accepting its initial state and before committing a transaction.

## Acceptance cases

- A node edit stores one node change and no whole-document field.
- Metadata or binding changes store only the changed content field.
- Delete/insert/duplicate operations retain only affected nodes and parent records.
- Undo and redo restore canonical content and recalculate revision/hash.
- A conflicting inverse fails atomically and leaves the current document unchanged.
- The patch history source contains no complete-document restore command.

The remaining clipboard command source outside Worker A's allowed directories still declares a patch inverse. The bus preserves that explicit inverse while normalizing the forward history entry to a patch, so clipboard undo/redo follows the same conflict-diagnosable path as responsive, metadata, and ordinary document edits.
