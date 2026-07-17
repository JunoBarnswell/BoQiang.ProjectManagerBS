# Runtime Artifact rollback closed loop

Rollback is authorized by `app:development-center:designer:publish` at `POST /api/application-development-center/pages/{pageId}/rollback`.

The application workspace resolver supplies the tenant and app boundary. The page resolves the DesignerDocument, and the requested artifact must match tenant, app, document, published status, artifact id, and artifact hash. The service revalidates canonical document hash, signature, manifest declarations, manifest hash, and persisted manifest JSON.

Within one application-database transaction the service updates the active DesignerDocument pointer, the page's published schema pointer, the `system_page_schemas` runtime payload, and application runtime menu schema pointers. It inserts a `RollbackSucceeded` publish record in that transaction. Failed validation or mutation inserts an immutable `RollbackFailed` record in a separate transaction. `OperationId` is unique per tenant/app and replay returns the original successful audit record.

The runtime read chain is `Permission -> Controller -> ApplicationDesignerArtifactRollbackService -> workspace database -> DesignerDocument/RuntimeArtifact -> SystemPageSchema -> RuntimePageSchemaService`.
