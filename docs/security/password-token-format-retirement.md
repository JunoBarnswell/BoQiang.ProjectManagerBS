# HAO-153 password and token format retirement contract

## Passwords

- The only current login format is `PBKDF2$v1$HMACSHA256$<iterations>$<salt>$<hash>`.
- `PasswordHashService` never compares an input password to a stored plaintext
  value and never attempts format guessing for malformed PBKDF2 values.
- Legacy PBKDF2 without a version prefix is accepted only when the configured
  `Security:PasswordHash:LegacyAcceptanceUntilUtc` is present and in the
  future. A successful legacy verification must immediately write `PBKDF2$v1`.
- SHA-256, suspected plaintext, expired legacy PBKDF2, empty values, and
  malformed PBKDF2 values require an explicit password reset. This fails
  closed: the stored value is never compared or guessed. Password reset state is stored
  as `PasswordResetRequired` and the non-secret format label is stored in
  `PasswordFormatVersion`.
- `PasswordFormatInventoryService` reports only format counts, scope, UTC
  timestamp, and a report hash. It never returns or logs password values.

### Initial administrator recovery

- `POST /api/auth/initial-admin-password-recovery` is an anonymous,
  rate-limited, deployment-only recovery route. It can reset only an enabled
  platform administrator whose `PasswordResetRequired` flag is set.
- Deployments enable it by injecting
  `Security__InitialAdminPasswordRecovery__Code`; the value must be managed as
  a secret and must not be committed to `appsettings*.json`. When it is absent,
  the route fails closed.
- The route uses fixed-time recovery-code comparison, records only the account,
  result, and trace context, revokes existing sessions, and never logs or
  returns the recovery code, password, or password hash.

## Tokens

- Browser token storage schema version is `2`.
- The old `astererp.access-token` value can be read only once while
  `VITE_APP_TOKEN_LEGACY_MIGRATION_UNTIL_UTC` is configured and unexpired.
  After the window the key name is detected without reading its value, counted
  as blocked, and removed during the one-time schema transition.
- Platform/application slots remain separate. Logout removes both versioned
  slots, the active slot marker, and any old key while retaining non-secret
  migration counters (`legacyReads`, `migrated`, `blockedAfterWindow`).

## Evidence status

The HAO-153 worker has not received an approved production read-only snapshot,
an auditable format inventory, or a legally approved legacy-password cutoff.
Therefore this repository only claims implementation readiness. It does not
claim production legacy counts are zero or that a production retirement window
has completed.
