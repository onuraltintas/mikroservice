import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const connection = 'Host=127.0.0.1;Port=55441;Database=coaching_planning_e2e;Username=planning_test;Password=disposable-planning-only';
const assembly = fileURLToPath(new URL('../../../services/coaching-service/Coaching.API/bin/Debug/net10.0/Coaching.API.dll', import.meta.url));
export function validatePlanningProcess(commandLine) {
  const args = (commandLine.match(/"[^"]*"|\S+/g) ?? []).map(x => x.replace(/^"|"$/g, ''));
  assert.match(args[0] ?? '', /(?:^|[\\/])dotnet(?:\.exe)?$/i, 'Expected the dotnet API host.');
  assert.equal(resolve(args[1] ?? '').toLowerCase(), resolve(assembly).toLowerCase(), 'Start the exact workspace API using its absolute assembly path.');
  // Reject duplicate keys in ANY command-line spelling before accepting our strict spaced form.
  assert.equal((commandLine.match(/ConnectionStrings:DefaultConnection/gi) ?? []).length, 1,
    'Exactly one database override is allowed.');
  const index = args.findIndex(x => /^--ConnectionStrings:DefaultConnection$/i.test(x));
  assert.ok(index >= 2, 'Start the API with an explicit spaced database override.');
  assert.equal(args[index + 1], connection, 'API database must be the dedicated loopback test database.');
  assert.equal((commandLine.match(/Services:IdentityService/gi) ?? []).length, 1,
    'Exactly one explicit Identity fixture override is required.');
  const identityIndex = args.findIndex(x => /^--Services:IdentityService$/i.test(x));
  assert.ok(identityIndex >= 2, 'Use the explicit loopback Identity fixture.');
  assert.equal(args[identityIndex + 1], 'http://127.0.0.1:4600', 'Identity calls must stay in the local test fixture.');
}

export default function preflight() {
  assert.equal(process.env.E2E_DISPOSABLE_ENV, 'true');
  assert.equal(process.platform, 'win32', 'This local fixture verifies the Windows API process.');
  const commandLine = execFileSync('powershell.exe', ['-NoProfile', '-Command',
    '$listeners=@(Get-NetTCPConnection -State Listen -LocalPort 5006); if(!$listeners.Count -or @($listeners | Where-Object {$_.LocalAddress -notin @("127.0.0.1","::1")}).Count){throw "Expected loopback-only API listeners"}; $pids=@($listeners | Select-Object -ExpandProperty OwningProcess -Unique); if($pids.Count -ne 1){throw "Expected one local API process"}; (Get-CimInstance Win32_Process -Filter ("ProcessId="+$pids[0])).CommandLine'],
  { encoding: 'utf8', timeout: 15_000 });
  validatePlanningProcess(commandLine.trim());
}
