#!/usr/bin/env node
import { mkdir, readdir, rm, stat, writeFile } from 'node:fs/promises';
import { existsSync, statSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

import * as esbuild from 'esbuild';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const projectRoot = path.resolve(__dirname, '..');
const args = parseArgs(process.argv.slice(2));
const target = (args.target || process.env.VITE_APP_TARGET_APP_CODE || '').trim().toUpperCase();
const metafilePath = path.resolve(
  projectRoot,
  args.metafile || process.env.PUBLISH_REACHABILITY_METAFILE || `../../artifacts/frontend-${target || 'FULL'}-metafile.json`
);
const reportPath = path.resolve(
  projectRoot,
  args.report || process.env.PUBLISH_REACHABILITY_REPORT || `../../artifacts/frontend-${target || 'FULL'}-purity-report.json`
);
const isTarget = target && target !== 'SYSTEM';
const shouldPrune = isTarget && isEnabled(args.prune || process.env.PUBLISH_REACHABILITY_PRUNE);

const alias = buildAlias(target);
const result = await esbuild.build({
  absWorkingDir: projectRoot,
  bundle: true,
  define: {
    'import.meta.env.MODE': JSON.stringify('production'),
    'import.meta.env.VITE_APP_TARGET_APP_CODE': JSON.stringify(target),
    'import.meta.env.VITE_APP_API_BASE_URL': JSON.stringify('/api'),
    'import.meta.env.VITE_APP_BASE_PATH': JSON.stringify(target ? `/${target}` : '/')
  },
  entryPoints: ['src/main.tsx'],
  external: ['buffer', 'events', 'fs', 'fs/promises', 'node:*', 'path', 'stream', 'util', 'zlib'],
  format: 'esm',
  loader: {
    '.css': 'empty',
    '.gif': 'file',
    '.jpg': 'file',
    '.jpeg': 'file',
    '.png': 'file',
    '.svg': 'file',
    '.ttf': 'file',
    '.webp': 'file',
    '.woff': 'file',
    '.woff2': 'file'
  },
  metafile: true,
  outfile: 'out.js',
  plugins: [aliasPlugin(alias)],
  sourcemap: false,
  treeShaking: true,
  write: false
});

const inputs = Object.keys(result.metafile.inputs)
  .map((input) => input.replaceAll('\\', '/'))
  .sort((a, b) => a.localeCompare(b));
const forbidden = isTarget
  ? inputs.filter((input) =>
      input.includes('/src/pages/system/') ||
      input.includes('/src/pages/platform/') ||
      input.includes('/src/pages/modules/') ||
      input.includes('/src/pages/engine/') ||
      input.endsWith('/src/app/router/workspaceRoutes.full.tsx') ||
      input.endsWith('/src/app/navigation/routes.ts') ||
      input.endsWith('/src/core/i18n/messages.ts') ||
      input.endsWith('/src/apps/runtimeRegistry.full.ts') ||
      (target === 'WMS' && input.includes('/src/apps/mes/')) ||
      (target === 'MES' && input.includes('/src/apps/wms/'))
    )
  : [];

await mkdir(path.dirname(metafilePath), { recursive: true });
await mkdir(path.dirname(reportPath), { recursive: true });
await writeFile(metafilePath, JSON.stringify(result.metafile, null, 2));

if (forbidden.length > 0) {
  await writeReport({ pruned: [] });
  console.error(`Forbidden frontend inputs detected for ${target}:`);
  for (const item of forbidden) {
    console.error(`- ${item}`);
  }
  process.exit(1);
}

const pruned = shouldPrune ? await pruneUnreachableSourceFiles(inputs) : [];
await writeReport({ pruned });

function parseArgs(values) {
  const parsed = {};
  for (let index = 0; index < values.length; index += 1) {
    const value = values[index];
    if (!value.startsWith('--')) {
      if (!parsed.target) {
        parsed.target = value;
      }
      continue;
    }

    const key = value.slice(2);
    parsed[key] = values[index + 1];
    index += 1;
  }

  return parsed;
}

async function writeReport({ pruned }) {
  await writeFile(
    reportPath,
    JSON.stringify(
      {
        target: target || 'FULL',
        inputCount: inputs.length,
        forbiddenCount: forbidden.length,
        forbidden,
        pruneEnabled: shouldPrune,
        prunedCount: pruned.length,
        pruned,
        inputs
      },
      null,
      2
    )
  );
}

async function pruneUnreachableSourceFiles(reachableInputs) {
  const srcRoot = path.resolve(projectRoot, 'src');
  const reachable = new Set(
    reachableInputs
      .map((input) => normalizePath(path.isAbsolute(input) ? input : path.resolve(projectRoot, input)))
      .filter((input) => input.startsWith(`${normalizePath(srcRoot)}/`))
  );
  const allSourceFiles = await listFiles(srcRoot);
  const pruned = [];

  for (const file of allSourceFiles) {
    const normalizedFile = normalizePath(file);
    const relativeFile = normalizePath(path.relative(projectRoot, file));
    if (relativeFile === 'src/vite-env.d.ts' || reachable.has(normalizedFile)) {
      continue;
    }

    await rm(file, { force: true });
    pruned.push(relativeFile);
  }

  await removeEmptyDirectories(srcRoot);
  return pruned.sort((a, b) => a.localeCompare(b));
}

async function listFiles(directory) {
  const entries = await readdir(directory, { withFileTypes: true });
  const files = [];
  for (const entry of entries) {
    const entryPath = path.join(directory, entry.name);
    if (entry.isDirectory()) {
      files.push(...(await listFiles(entryPath)));
      continue;
    }

    if (entry.isFile()) {
      files.push(entryPath);
    }
  }

  return files;
}

async function removeEmptyDirectories(directory) {
  const entries = await readdir(directory, { withFileTypes: true });
  for (const entry of entries) {
    if (entry.isDirectory()) {
      await removeEmptyDirectories(path.join(directory, entry.name));
    }
  }

  if (normalizePath(directory) === normalizePath(path.resolve(projectRoot, 'src'))) {
    return;
  }

  const remaining = await readdir(directory);
  if (remaining.length === 0 && (await stat(directory)).isDirectory()) {
    await rm(directory, { recursive: true, force: true });
  }
}

function normalizePath(value) {
  return value.replaceAll('\\', '/');
}

function isEnabled(value) {
  return value === true || value === '1' || value === 'true' || value === 'yes';
}

function buildAlias(targetAppCode) {
  const normalizedTarget = targetAppCode.toUpperCase();
  const isTargetBuild = normalizedTarget && normalizedTarget !== 'SYSTEM';
  return {
    '@/app/router/workspaceRoutes': path.resolve(
      projectRoot,
      isTargetBuild ? 'src/app/router/workspaceRoutes.target.tsx' : 'src/app/router/workspaceRoutes.full.tsx'
    ),
    '@/app/navigation/routes': path.resolve(
      projectRoot,
      isTargetBuild ? 'src/app/navigation/routes.target.ts' : 'src/app/navigation/routes.ts'
    ),
    '@/core/i18n/messages': path.resolve(
      projectRoot,
      isTargetBuild ? 'src/core/i18n/messages.target.ts' : 'src/core/i18n/messages.ts'
    ),
    '@/pages/dashboard/DashboardPage': path.resolve(
      projectRoot,
      isTargetBuild ? 'src/pages/dashboard/DashboardPage.target.tsx' : 'src/pages/dashboard/DashboardPage.tsx'
    ),
    '@/apps/runtimeRegistry': path.resolve(projectRoot, resolveRuntimeRegistryModule(normalizedTarget)),
    '@': path.resolve(projectRoot, 'src')
  };
}

function resolveRuntimeRegistryModule(targetAppCode) {
  if (!targetAppCode || targetAppCode === 'SYSTEM') {
    return 'src/apps/runtimeRegistry.full.ts';
  }

  if (targetAppCode === 'WMS') {
    return 'src/apps/runtimeRegistry.wms.ts';
  }

  if (targetAppCode === 'MES') {
    return 'src/apps/runtimeRegistry.mes.ts';
  }

  return 'src/apps/runtimeRegistry.empty.ts';
}

function aliasPlugin(aliasMap) {
  const sortedAliases = Object.entries(aliasMap).sort((a, b) => b[0].length - a[0].length);
  return {
    name: 'astererp-alias',
    setup(build) {
      build.onResolve({ filter: /\?worker$/ }, (args) => {
        const withoutQuery = args.path.slice(0, -'?worker'.length);
        const workerPath = withoutQuery.endsWith('.js') ? withoutQuery : `${withoutQuery}.js`;
        if (workerPath.startsWith('monaco-editor/')) {
          return { path: resolveAliasPath(path.resolve(projectRoot, 'node_modules', workerPath)) };
        }

        if (args.resolveDir) {
          return { path: resolveAliasPath(path.resolve(args.resolveDir, workerPath)) };
        }

        return null;
      });

      build.onResolve({ filter: /^monaco-editor\/esm\// }, (args) => {
        const monacoPath = args.path.endsWith('.js') ? args.path : `${args.path}.js`;
        return { path: resolveAliasPath(path.resolve(projectRoot, 'node_modules', monacoPath)) };
      });

      build.onResolve({ filter: /.*/ }, (args) => {
        for (const [find, replacement] of sortedAliases) {
          if (args.path === find) {
            return { path: resolveAliasPath(replacement) };
          }

          if (args.path.startsWith(`${find}/`)) {
            return { path: resolveAliasPath(path.join(replacement, args.path.slice(find.length + 1))) };
          }
        }

        return null;
      });
    }
  };
}

function resolveAliasPath(candidate) {
  for (const resolved of [
    `${candidate}.ts`,
    `${candidate}.tsx`,
    `${candidate}.js`,
    `${candidate}.jsx`,
    `${candidate}.json`,
    path.join(candidate, 'index.ts'),
    path.join(candidate, 'index.tsx'),
    path.join(candidate, 'index.js'),
    path.join(candidate, 'index.jsx'),
    candidate
  ]) {
    if (existsSync(resolved) && statSync(resolved).isFile()) {
      return resolved;
    }
  }

  return candidate;
}
