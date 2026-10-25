import { readdirSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import { spawnSync } from 'node:child_process';

const root = fileURLToPath(new URL('../', import.meta.url));
const files = [];

function collect(directory) {
  for (const entry of readdirSync(directory, { withFileTypes: true })) {
    const file = path.join(directory, entry.name);
    if (entry.isDirectory() && !['bin', 'obj', 'node_modules'].includes(entry.name)) collect(file);
    else if (entry.isFile() && entry.name.endsWith('.test.mjs')) files.push(file);
  }
}

collect(path.join(root, 'clients/speed-reading/template-contracts'));
collect(path.join(root, 'services/speed-reading-service'));
files.sort();
if (files.length === 0) throw new Error('No Speed Reading contract tests found.');

console.log(`Running ${files.length} Speed Reading contract files.`);
const result = spawnSync(process.execPath, ['--test', ...files], { cwd: root, stdio: 'inherit' });
if (result.error) throw result.error;
process.exit(result.status ?? 1);
