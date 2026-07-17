import { access, readFile, readdir } from 'node:fs/promises';
import { dirname, join, normalize, relative, resolve } from 'node:path';

const repositoryRoot = resolve(process.cwd(), '..', '..');
const publishRoot = resolve(repositoryRoot, 'backend', 'AsterERP.Api', 'wwwroot');
const manifestPath = join(publishRoot, '.astererp-frontend-manifest.json');

const manifest = JSON.parse(await readFile(manifestPath, 'utf8'));
if (!Array.isArray(manifest.files) || !manifest.files.includes('index.html')) {
  throw new Error('Shared frontend manifest is missing index.html.');
}

for (const file of manifest.files) {
  await access(join(publishRoot, file));
}

const missingReferences = [];
const referencedFiles = new Set();
const manifestFiles = new Set(manifest.files);

function isSkippableReference(reference) {
  return !reference || reference.startsWith('#') || reference.startsWith('data:') || reference.startsWith('blob:') || /^[a-z][a-z\d+.-]*:/i.test(reference);
}

function resolvePublishedReference(owner, reference, { allowBareAsset = false } = {}) {
  const cleanReference = reference.split(/[?#]/, 1)[0];
  if (isSkippableReference(cleanReference)) return null;
  const isRootPublishedAsset = cleanReference.startsWith('/assets/') || cleanReference.startsWith('/vendor/') || cleanReference.startsWith('/wasm/');
  const isRelativeReference = cleanReference.startsWith('./') || cleanReference.startsWith('../');
  const isBareAsset = allowBareAsset && /\.(?:css|gif|ico|jpe?g|js|png|svg|ttf|wasm|webp|woff2?)$/i.test(cleanReference);
  if (!isRootPublishedAsset && !isRelativeReference && !isBareAsset) return null;
  const ownerPath = join(publishRoot, owner);
  const candidate = cleanReference.startsWith('/')
    ? cleanReference.slice(1)
    : normalize(join(dirname(ownerPath), cleanReference));
  const normalized = normalize(candidate);
  const relativePath = relative(publishRoot, normalized).replaceAll('\\', '/');
  if (!relativePath || relativePath.startsWith('../') || relativePath === '..') return null;
  return relativePath;
}

async function collectReferences(owner, source) {
  const referencesWithMode = [];
  for (const match of source.matchAll(/(?:src|href)=["']([^"']+)["']/gi)) referencesWithMode.push({ value: match[1], allowBareAsset: false });
  for (const match of source.matchAll(/\bimport\s*(?:\(\s*)?["']([^"']+)["']/g)) referencesWithMode.push({ value: match[1], allowBareAsset: false });
  for (const match of source.matchAll(/\bfrom\s*["']([^"']+)["']/g)) referencesWithMode.push({ value: match[1], allowBareAsset: false });
  if (owner.endsWith('.css')) {
    for (const match of source.matchAll(/url\(\s*["']?([^"')]+)["']?\s*\)/gi)) referencesWithMode.push({ value: match[1], allowBareAsset: true });
  }

  for (const { value: reference, allowBareAsset } of referencesWithMode) {
    const publishedPath = resolvePublishedReference(owner, reference, { allowBareAsset });
    if (!publishedPath) continue;
    referencedFiles.add(publishedPath);
    if (!manifestFiles.has(publishedPath)) {
      missingReferences.push(`${owner} -> ${reference} (expected ${publishedPath})`);
      continue;
    }
    try {
      await access(join(publishRoot, publishedPath));
    } catch {
      missingReferences.push(`${owner} -> ${reference} (missing ${publishedPath})`);
    }
  }
}

for (const file of manifest.files) {
  if (!/\.(?:html|js|css)$/i.test(file)) continue;
  await collectReferences(file, await readFile(join(publishRoot, file), 'utf8'));
}

if (missingReferences.length > 0) {
  throw new Error(`Published frontend has missing referenced files:\n${missingReferences.join('\n')}`);
}

const legacyBundlePaths = [
  'assets/elementRegistry-rD7WR26B.js',
  'assets/DesignerRoutePage-B3INfkXw.js',
  'assets/RuntimePage-BFddAhfr.js'
];
const legacyBundlesPresent = [];
for (const legacyPath of legacyBundlePaths) {
  try {
    await access(join(publishRoot, legacyPath));
    legacyBundlesPresent.push(legacyPath);
  } catch {
    // A missing legacy path is the expected 404-safe state.
  }
}
if (legacyBundlesPresent.length > 0) {
  throw new Error(`Legacy frontend bundles must be absent (404-safe): ${legacyBundlesPresent.join(', ')}`);
}

const entries = await readdir(publishRoot, { withFileTypes: true });
const frontendEntries = entries.filter(entry => entry.name === 'index.html' || entry.name === 'assets');
const allowedEntries = new Set([
  'index.html',
  'assets',
  'vendor',
  'wasm',
  'flyfish-viewer-assets.json',
  'uploads',
  '.astererp-frontend-manifest.json'
]);
if (frontendEntries.length !== 2 || entries.some(entry => !allowedEntries.has(entry.name))) {
  throw new Error('wwwroot does not contain exactly one shared frontend entry (index.html + assets/).');
}

const indexHtml = await readFile(join(publishRoot, 'index.html'), 'utf8');
if (!indexHtml.includes('/assets/')) {
  throw new Error('Published index.html must reference shared root-relative assets.');
}

console.log(`Shared frontend publish check passed: ${manifest.files.length} files, ${referencedFiles.size} referenced files, legacy bundle paths absent in ${publishRoot}`);
