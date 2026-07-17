import type { DesignerDocument } from '../document/DesignerDocument';

import { createDesignerClipboardPayload } from './designerClipboardPayload';
export type { DesignerClipboardPayload } from './designerClipboardPayload';

export function createDesignerClipboard(document: DesignerDocument, nodeIds: readonly string[]) {
  return createDesignerClipboardPayload(document, nodeIds);
}
