import { describe, expect, it } from 'vitest';

import { formatWorkbenchSql } from './WorkbenchSqlEditor';

describe('formatWorkbenchSql', () => {
  it('formats clauses and boolean predicates without changing SQL tokens', () => {
    expect(formatWorkbenchSql('SELECT id,name FROM users WHERE active = 1 AND name = \'Ada\' ORDER BY id')).toBe(
      "SELECT id, name\nFROM users\nWHERE active = 1\n  AND name = 'Ada'\nORDER BY id"
    );
  });

  it('normalizes whitespace and keeps an empty script empty', () => {
    expect(formatWorkbenchSql('  SELECT   *   FROM   users  ')).toBe('SELECT *\nFROM users');
    expect(formatWorkbenchSql('   ')).toBe('');
  });
});
