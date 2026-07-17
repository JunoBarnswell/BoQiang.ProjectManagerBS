# Frontend Worker Blocker

## Backend static frontend runtime root is duplicated

- `backend/AsterERP.Api/Program.cs` resolves static files from several roots, including source `backend/AsterERP.Api/wwwroot` and process-relative publish `wwwroot` directories.
- The workspace currently contains distinct `index.html` files in `backend/AsterERP.Api/wwwroot`, `bin/Release/net10.0/publish/wwwroot`, and `bin/Release/net10.0/win-x64/publish/wwwroot`; their SHA-256 hashes differ.
- This is outside the assigned frontend-only slice. Frontend mitigation rejects `VITE_APP_OUT_DIR` outside `frontend/AsterERP.Web` and keeps `publish:frontend` staging inside the frontend project before synchronizing the canonical source `wwwroot`.
- Backend owner decision required: select one runtime static root and one deployment artifact owner, then rerun the shared frontend publish check.
