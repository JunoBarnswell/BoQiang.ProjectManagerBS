# Connection wizard status

Saved data-source diagnosis is implemented at `POST /api/application-data-center/data-sources/{id}/diagnose` with configuration, network, TLS, authentication, database, permission, and capability stages.

The unsaved provider draft flow is also implemented at `POST /api/application-data-center/data-sources/draft/diagnose`. It accepts provider-specific configuration and secret input without a persistence identity, protects the temporary secret in memory, executes the same staged diagnostic pipeline, writes only a redacted audit record, and returns stage status without echoing the secret. The page calls this endpoint before Save and keeps the persisted `dataSourceId` flow for re-diagnosis.

Remaining release evidence is external: authorized provider credentials/containers and production secret-rotation/audit traces are required before HAO-70/71 can be closed.
