import { createElement, useMemo, useState } from 'react';

import { PrintLaunchDialog } from '../components/PrintLaunchDialog';
import type { PrintLaunchRequest } from '../types';

export function usePrintLauncher() {
  const [request, setRequest] = useState<PrintLaunchRequest | null>(null);

  const dialog = useMemo(
    () => createElement(PrintLaunchDialog, { open: Boolean(request), request, onClose: () => setRequest(null) }),
    [request]
  );

  return {
    close: () => setRequest(null),
    dialog,
    open: (nextRequest: PrintLaunchRequest) => setRequest(nextRequest)
  };
}
