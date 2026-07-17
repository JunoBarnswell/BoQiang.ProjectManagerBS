# Page Studio coordinator update

The first HAO-111 acceptance run exposed a production defect: deleting a
selected node removed only the root and left descendants in the document.
`createDeleteNodesCommand` was corrected to collect the complete subtree,
remove every descendant, clean external child references, and preserve the
full subtree through the inverse command. The acceptance fixture was aligned
with the typed `ResourceRef` contract (`displayName`, `valueType`, and typed
fallback).

Current evidence:

- `pageStudioHao111Acceptance.test.ts`: 5/5 passed.
- Complete low-code-studio selection: 16 files / 46 tests passed.
- `npm run typecheck`: passed.
- Authenticated browser UI/E2E, real drag/resize screenshots, permission
  denial, and save API reload remain Blocked because the authorized Page
  Studio URL and login session are unavailable.
