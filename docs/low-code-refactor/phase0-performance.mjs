import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import { resolve } from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const root = resolve(fileURLToPath(new URL('../..', import.meta.url)));
const frontend = resolve(root, 'frontend/AsterERP.Web');
const testFile = 'src/pages/application-console/development-center/low-code-studio/testing/performance/lowCodePerformanceBenchmark.test.ts';
const outputDirectory = resolve(root, 'artifacts/phase0/performance');
const aggregateOutputPath = resolve(outputDirectory, 'designer-performance.json');
const expectedNodeCounts = [100, 500, 1000, 2000];

mkdirSync(outputDirectory, { recursive: true });

const command = process.platform === 'win32' ? 'npm.cmd' : 'npm';
const result = spawnSync(command, ['run', 'test', '--', '--run', testFile], {
  cwd: frontend,
  env: { ...process.env, LOW_CODE_PERFORMANCE_OUTPUT: aggregateOutputPath },
  shell: true,
  encoding: 'utf8',
  stdio: 'pipe'
});

process.stdout.write(result.stdout ?? '');
process.stderr.write(result.stderr ?? '');
if (result.status !== 0) {
  if (result.error) throw result.error;
  throw new Error(`Performance test failed with exit code ${result.status}.`);
}

const aggregate = JSON.parse(readFileSync(aggregateOutputPath, 'utf8'));
aggregate.commit = readGitValue(root, ['rev-parse', 'HEAD']);
aggregate.workingTreeStatus = readGitValue(root, ['status', '--porcelain']);
aggregate.toolchain = {
  node: process.version,
  platform: process.platform,
  arch: process.arch,
  cpu: os.cpus()[0]?.model ?? 'unknown',
  cpuCount: os.cpus().length
};
if (aggregate.status !== 'Measured' || aggregate.sampleCount !== 5 || aggregate.warmupCount !== 1) {
  throw new Error('Performance runner received incomplete aggregate evidence.');
}

const scenarios = aggregate.scenarios ?? [];
if (scenarios.length !== expectedNodeCounts.length ||
    scenarios.some((scenario) => !expectedNodeCounts.includes(scenario.nodeCount) ||
      scenario.saveMs?.length !== 5 || scenario.undoMs?.length !== 5 || scenario.runtimeFirstScreenMs?.length !== 5)) {
  throw new Error('Performance runner received incomplete node-count evidence.');
}

for (const scenario of scenarios) {
  const outputPath = resolve(outputDirectory, `designer-operations-${scenario.nodeCount}.json`);
  writeFileSync(outputPath, `${JSON.stringify({
    format: 'astererp.low-code.performance-evidence.v1',
    scenario: `designer-operations-${scenario.nodeCount}`,
    ...scenario,
    runs: scenario.saveMs.length,
    status: 'Measured',
    evidenceSource: aggregateOutputPath,
    capturedAt: aggregate.capturedAt,
    commit: aggregate.commit,
    workingTreeStatus: aggregate.workingTreeStatus,
    toolchain: aggregate.toolchain
  }, null, 2)}\n`, 'utf8');
  console.log(`PASS designer-operations-${scenario.nodeCount}: saveP95=${scenario.saveP95Ms}ms undoP95=${scenario.undoP95Ms}ms runtimeP95=${scenario.runtimeFirstScreenP95Ms}ms heap=${scenario.peakWorkingSetBytes} bytes`);
}

function readGitValue(cwd, args) {
  const git = spawnSync('git', args, { cwd, encoding: 'utf8', stdio: 'pipe' });
  if (git.status !== 0) return 'unavailable';
  return (git.stdout ?? '').trim();
}
