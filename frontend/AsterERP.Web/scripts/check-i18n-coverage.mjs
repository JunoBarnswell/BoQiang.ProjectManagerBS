import fs from 'node:fs';
import path from 'node:path';

const root = process.cwd();
const projectManagementWorkbenchOnly = process.argv.includes('--project-management-workbench');
const scanRoots = projectManagementWorkbenchOnly ? [
  'src/features/project-management/ui',
  'src/features/project-management/collaboration',
  'src/features/project-management/notifications'
] : [
  'src/pages',
  'src/features/flowise-studio',
  'src/features/ai-center',
  'src/features/print-center',
  'src/shared',
  'src/app/layout',
  'src/app/navigation'
];

const allowFileSnippets = [
  'node_modules',
  'Flowise',
  'AsterERP',
  'AsterScene',
  'OpenAI',
  'SHA256',
  'TraceId',
  'JSON',
  'http://',
  'https://',
  'application/',
  'text/',
  'image/',
  'audio/',
  'video/'
];

const suspiciousPatterns = [
  /title="[^"{][^"]*[\u4e00-\u9fff][^"]*"/g,
  />[^<>{]*[\u4e00-\u9fff][^<>{]*</g,
  /placeholder="[^"{][^"]*[\u4e00-\u9fff][^"]*"/g,
  /label:\s*'[^'{]*[\u4e00-\u9fff][^']*'/g,
  /label:\s*"[^"{]*[\u4e00-\u9fff][^"]*"/g
];

function walk(dir, output = []) {
  if (!fs.existsSync(dir)) {
    return output;
  }

  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const fullPath = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      walk(fullPath, output);
      continue;
    }

    if (/\.(tsx?|jsx?)$/.test(entry.name) && !/\.test\./.test(entry.name)) {
      output.push(fullPath);
    }
  }

  return output;
}

function isAllowed(match) {
  return allowFileSnippets.some((item) => match.includes(item));
}

const findings = [];

for (const relativeRoot of scanRoots) {
  const files = walk(path.join(root, relativeRoot));
  for (const file of files) {
    const content = fs.readFileSync(file, 'utf8');
    for (const pattern of suspiciousPatterns) {
      for (const match of content.matchAll(pattern)) {
        const value = match[0];
        if (isAllowed(value)) {
          continue;
        }

        const line = content.slice(0, match.index).split(/\r?\n/).length;
        findings.push(`${path.relative(root, file)}:${line}: ${value.trim()}`);
      }
    }
  }
}

if (findings.length > 0) {
  console.error('i18n coverage audit found suspicious hardcoded UI strings:');
  for (const finding of findings.slice(0, 200)) {
    console.error(`- ${finding}`);
  }
  if (findings.length > 200) {
    console.error(`... ${findings.length - 200} more`);
  }
  process.exit(1);
}

console.log(projectManagementWorkbenchOnly ? 'project-management workbench i18n audit passed' : 'i18n coverage audit passed');
