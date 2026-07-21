import { cp, mkdir, mkdtemp, readFile, readdir, rm, writeFile } from 'node:fs/promises';
import { spawn } from 'node:child_process';
import { dirname, isAbsolute, join, relative, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

const frontendRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const repositoryRoot = resolve(frontendRoot, '..', '..');
const publishRoot = resolve(repositoryRoot, 'backend', 'AsterERP.Api', 'wwwroot');
const stagingRoot = await mkdtemp(join(frontendRoot, '.shared-build-'));
const manifestPath = join(publishRoot, '.astererp-frontend-manifest.json');

try {
  await runBuild(stagingRoot);
  await validateBuildOutput(stagingRoot);
  await syncPublishedFiles(stagingRoot);
  console.log(`Published one shared frontend build to ${publishRoot}`);
} finally {
  await rm(stagingRoot, { recursive: true, force: true });
}

function runBuild(outDir) {
  const viteCli = join(frontendRoot, 'node_modules', 'vite', 'bin', 'vite.js');
  return new Promise((resolveProcess, rejectProcess) => {
    const child = spawn(process.execPath, [viteCli, 'build', '--outDir', outDir], {
      cwd: frontendRoot,
      env: {
        ...process.env,
        VITE_APP_OUT_DIR: outDir,
        VITE_APP_BASE_PATH: '/',
        VITE_APP_API_BASE_URL: '/api',
        VITE_APP_TARGET_APP_CODE: ''
      },
      stdio: 'inherit'
    });

    child.once('error', rejectProcess);
    child.once('exit', (code, signal) => {
      if (code === 0) {
        resolveProcess();
        return;
      }

      rejectProcess(new Error(`Shared frontend build failed (code=${code ?? 'unknown'}, signal=${signal ?? 'none'})`));
    });
  });
}

async function validateBuildOutput(outDir) {
  const entries = await readdir(outDir, { withFileTypes: true });
  const names = entries.map(entry => entry.name);
  if (!names.includes('index.html') || !names.includes('assets')) {
    throw new Error('Shared frontend build must contain index.html and assets/.');
  }

  const allowedEntries = new Set(['index.html', 'assets', 'vendor', 'wasm', 'flyfish-viewer-assets.json']);
  const forbiddenEntries = names.filter(name => !allowedEntries.has(name));
  if (forbiddenEntries.length > 0) {
    throw new Error(`Shared frontend build contains unexpected entries: ${forbiddenEntries.join(', ')}`);
  }

  const indexHtml = await readFile(join(outDir, 'index.html'), 'utf8');
  if (!indexHtml.includes('/assets/')) {
    throw new Error('Shared frontend index.html does not reference root-relative assets.');
  }
}

async function syncPublishedFiles(outDir) {
  await mkdir(publishRoot, { recursive: true });
  await removeLegacyAppDirectories();
  const previousFiles = await readManifest();
  for (const file of previousFiles) {
    const target = resolve(publishRoot, file);
    assertInsidePublishRoot(target);
    await rm(target, { recursive: false, force: true });
  }

  const files = await collectFiles(outDir);
  for (const file of files) {
    const source = join(outDir, file);
    const target = resolve(publishRoot, file);
    assertInsidePublishRoot(target);
    await mkdir(dirname(target), { recursive: true });
    await cp(source, target);
  }

  await writeFile(manifestPath, `${JSON.stringify({ files }, null, 2)}\n`, 'utf8');
}

async function readManifest() {
  try {
    const parsed = JSON.parse(await readFile(manifestPath, 'utf8'));
    return Array.isArray(parsed.files) ? parsed.files : [];
  } catch (error) {
    if (error.code === 'ENOENT') {
      return [];
    }

    throw new Error(`Cannot read shared frontend manifest: ${error.message}`);
  }
}

async function collectFiles(root, current = '') {
  const directory = join(root, current);
  const entries = await readdir(directory, { withFileTypes: true });
  const files = [];
  for (const entry of entries) {
    const relativePath = join(current, entry.name).split(sep).join('/');
    if (entry.isDirectory()) {
      files.push(...(await collectFiles(root, relativePath)));
    } else if (entry.isFile()) {
      files.push(relativePath);
    }
  }

  return files;
}

function assertInsidePublishRoot(target) {
  const pathRelativeToRoot = relative(publishRoot, target);
  if (isAbsolute(pathRelativeToRoot) || pathRelativeToRoot.startsWith(`..${sep}`) || pathRelativeToRoot === '..') {
    throw new Error(`Refusing to write outside frontend publish root: ${target}`);
  }
}

async function removeLegacyAppDirectories() {
  const allowedRootEntries = new Set([
    'index.html',
    'assets',
    'vendor',
    'wasm',
    'flyfish-viewer-assets.json',
    'uploads',
    '.astererp-frontend-manifest.json'
  ]);
  const entries = await readdir(publishRoot, { withFileTypes: true });
  for (const entry of entries) {
    if (!entry.isDirectory() || allowedRootEntries.has(entry.name)) {
      continue;
    }

    const legacyPath = join(publishRoot, entry.name);
    assertInsidePublishRoot(legacyPath);
    await rm(legacyPath, { recursive: true, force: true });
    console.log(`Removed legacy per-app frontend directory: ${entry.name}`);
  }
}
