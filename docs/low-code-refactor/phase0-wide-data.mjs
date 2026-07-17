import { mkdir, writeFile } from 'node:fs/promises';
import { spawn } from 'node:child_process';
import path from 'node:path';
import process from 'node:process';

const root = path.resolve(import.meta.dirname, '../..');
const frontend = path.join(root, 'frontend', 'AsterERP.Web');
const outputDirectory = path.join(root, 'artifacts', 'phase0', 'wide-data');
const columns = [20, 200, 1000];
const rows = [10000, 1000000];

await mkdir(outputDirectory, { recursive: true });
const evidence = [];
let failed = false;

for (const columnCount of columns) {
  for (const rowCount of rows) {
    const outputPath = path.join(outputDirectory, `wide-${columnCount}cols-${rowCount}rows.json`);
    const result = await runScenario(columnCount, rowCount, outputPath);
    evidence.push(result);
    if (result.status !== 'Measured') failed = true;
  }
}

await writeFile(
  path.join(outputDirectory, 'summary.json'),
  JSON.stringify({
    format: 'astererp.low-code.wide-data-summary.v1',
    scenarios: evidence,
    status: failed ? 'Fail' : 'Measured',
    externalDatabaseStatus: 'Blocked',
    capturedAt: new Date().toISOString()
  }, null, 2) + '\n',
  'utf8'
);

if (failed) process.exitCode = 1;

function runScenario(columnCount, rowCount, outputPath) {
  return new Promise((resolve) => {
    const child = spawn(
      process.platform === 'win32' ? 'npm.cmd' : 'npm',
      ['run', 'test', '--', '--run', 'src/shared/table/wideDataVolumeBaseline.test.ts', '--reporter=verbose'],
      {
        cwd: frontend,
        env: {
          ...process.env,
          ASTERERP_WIDE_BASELINE_COLUMNS: String(columnCount),
          ASTERERP_WIDE_BASELINE_ROWS: String(rowCount),
          ASTERERP_WIDE_BASELINE_OUTPUT: outputPath
        },
        shell: process.platform === 'win32',
        stdio: ['ignore', 'pipe', 'pipe']
      }
    );
    let stdout = '';
    let stderr = '';
    child.stdout.on('data', (chunk) => { stdout += chunk; });
    child.stderr.on('data', (chunk) => { stderr += chunk; });
    child.on('close', async (code) => {
      const marker = stdout.split(/\r?\n/).find((line) => line.startsWith('WIDE_BASELINE_JSON '));
      if (marker) {
        const parsed = JSON.parse(marker.slice('WIDE_BASELINE_JSON '.length));
        await writeFile(outputPath, JSON.stringify(parsed, null, 2) + '\n', 'utf8');
        resolve(parsed);
        return;
      }

      resolve({
        scenario: `wide-${columnCount}cols-${rowCount}rows`,
        status: 'Fail',
        exitCode: code,
        error: (stderr || stdout).slice(-4000)
      });
    });
  });
}
