# Retired data-source provider migration

`REST`, `MinIO`, `S3`, `OSS`, `Kafka`, and `RabbitMQ` are not supported Data Center providers. They have no creation template, form schema, connection test, preview, catalog, workbench, query-model, mapping-cache, integration, or runtime execution path.

Use `GET /api/application-data-center/data-sources/migration-required` with the data-source view permission to produce a read-only inventory. The endpoint marks each matching historical configuration as `MigrationRequired` and returns its identifier, retired provider, and diagnostic; it never rewrites credentials or silently converts the source to another provider.

Resolve every entry by exporting the required metadata, creating a supported database, ApplicationDatabase, Excel, or CSV data source, repointing dependent artifacts, and then deleting the retired source. Any operational endpoint invoked directly on a retired source fails closed and preserves the migration diagnostic.
