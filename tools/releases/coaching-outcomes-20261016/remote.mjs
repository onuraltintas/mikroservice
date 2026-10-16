import { readFileSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
const file = process.argv[2];
if (!file?.endsWith('.sh')) throw new Error('An explicit reviewed shell file is required.');
const args = process.argv.slice(3); if (args.some(x => !/^[a-z-]+$/.test(x))) throw new Error('Invalid operation');
const syntax = args.length === 1 && args[0] === 'syntax';
const script = '{\n' + readFileSync(file, 'utf8').replaceAll('\r\n', '\n') + '\n} </dev/null\n';
const result = spawnSync('ssh', ['-F', 'NUL', '-i', 'C:/Users/onura/.ssh/eduivme_vps_deploy', '-o', 'BatchMode=yes',
  '-o', 'StrictHostKeyChecking=yes', 'root@185.170.198.83', 'bash', ...(syntax ? ['-n', '-s'] : ['-s', '--', ...args])], { input: script, encoding: 'utf8', maxBuffer: 1024 * 1024 });
if (result.stdout) process.stdout.write(result.stdout);
if (result.stderr) process.stderr.write(result.stderr);
process.exit(result.status ?? 1);
