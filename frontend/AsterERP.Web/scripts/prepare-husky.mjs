#!/usr/bin/env node
import { existsSync } from 'node:fs';
import { dirname, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const projectRoot = resolve(scriptDirectory, '..');
const repositoryRoot = findRepositoryRoot(projectRoot);

if (!repositoryRoot) {
  process.exit(0);
}

const huskyBin = resolve(projectRoot, 'node_modules/.bin/husky.cmd');
const fallbackHuskyBin = resolve(projectRoot, 'node_modules/.bin/husky');
const executable = existsSync(huskyBin) ? huskyBin : fallbackHuskyBin;
const huskyDirectory = relative(repositoryRoot, resolve(projectRoot, '.husky')).replaceAll('\\', '/');
const result = spawnSync(executable, [huskyDirectory], {
  cwd: repositoryRoot,
  stdio: 'inherit',
  shell: process.platform === 'win32'
});

process.exit(result.status ?? 1);

function findRepositoryRoot(startDirectory) {
  let current = startDirectory;

  while (true) {
    if (existsSync(resolve(current, '.git'))) {
      return current;
    }

    const parent = dirname(current);
    if (parent === current) {
      return null;
    }

    current = parent;
  }
}
