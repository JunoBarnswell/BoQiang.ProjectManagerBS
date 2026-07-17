# Designer Fixture Baseline

The checked-in MES fixture set was generated from the required debug database:

`D:\Code\AsterERP\backend\AsterERP.Api\data\application-databases\tenant-a\MES\mes11.db`

It contains every non-deleted `app_dev_pages` record in the `tenant-a / MES` workspace for which the current source reader can resolve the latest non-deleted Designer Document and latest published Runtime Artifact. Document structure, component types, bindings, actions, permissions, and responsive fields are retained. Sensitive properties such as tokens, passwords, secrets, connection strings, cookies, and authorization values are replaced with deterministic fixture-local tokens.

Regenerate the baseline with:

```powershell
dotnet run --project tools/AsterERP.DesignerFixtureGenerator/AsterERP.DesignerFixtureGenerator.csproj -- `
  --database 'D:\Code\AsterERP\backend\AsterERP.Api\data\application-databases\tenant-a\MES\mes11.db' `
  --tenant tenant-a `
  --app MES `
  --output 'docs/low-code-refactor/fixtures/tenant-a/MES'
```

`manifest.json` is ordered by `PageCode` and records the anonymized document and artifact hashes. The generator is deterministic for identical source input and fixture identity.
