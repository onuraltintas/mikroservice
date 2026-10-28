import { test } from 'node:test';
import assert from 'node:assert/strict';
import { execFile } from 'node:child_process';
import { chmod, mkdir, mkdtemp, readdir, readFile, rm, writeFile } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import { join } from 'node:path';
import { tmpdir } from 'node:os';
import { fileURLToPath } from 'node:url';
import { promisify } from 'node:util';

// Roadmap P1: nightly PostgreSQL backups on the server with an encrypted off-site copy;
// kept 7 daily, 4 weekly (Sunday) and 6 monthly (first backup of the month).
const run = promisify(execFile);
const root = fileURLToPath(new URL('../../../', import.meta.url));
const script = join(root, 'infrastructure/backup/eduivme-db-backup.sh');

async function sandbox() {
  const dir = await mkdtemp(join(tmpdir(), 'eduivme-backup-'));
  const bin = join(dir, 'bin'); await mkdir(bin);
  const log = join(dir, 'calls.txt');
  const mock = async (name, body) => { await writeFile(join(bin, name), `#!/usr/bin/env bash\nset -euo pipefail\nprintf '%s %s\\n' "${name}" "$*" >> "${log}"\n${body}\n`); await chmod(join(bin, name), 0o755); };
  await mock('docker', `case "$*" in
  *"printenv POSTGRES_USER"*) echo eduplatform ;;
  *"datname FROM pg_database"*) printf 'identity_db\\ncoaching_db\\nnotification_db\\n' ;;
  *pg_dumpall*) echo "-- roles" ;;
  *"pg_dump"*) [[ "\${MOCK_FAIL_DB:-}" != "" && "$*" == *"\${MOCK_FAIL_DB:-none}"* ]] && exit 7; echo "PGDMP fake dump for $*" ;;
  *"pg_restore --list"*) cat >/dev/null; echo "; Archive created" ;;
  *) echo "unexpected docker $*" >&2; exit 2 ;;
esac`);
  await mock('age', `out=""; while [[ $# -gt 0 ]]; do case "$1" in -o) out="$2"; shift 2 ;; -r) shift 2 ;; *) in="$1"; shift ;; esac; done; { echo age-encrypted; cat "$in"; } > "$out"`);
  await mock('rclone', `exit 0`);
  return { dir, bin, log, backups: join(dir, 'backups') };
}

async function backup(box, date, extra = {}) {
  return run('bash', [script], { env: { PATH: `${box.bin}:/usr/bin:/bin`, BACKUP_DIR: box.backups, BACKUP_DATE: date, POSTGRES_CONTAINER: 'postgres', ...extra } });
}

test('dumps every database and the roles, checks each dump and writes checksums', async () => {
  const box = await sandbox();
  try {
    await backup(box, '2026-11-04');
    const day = join(box.backups, 'daily', '2026-11-04');
    const files = (await readdir(day)).sort();
    assert.deepEqual(files, ['SHA256SUMS', 'coaching_db.dump', 'globals.sql', 'identity_db.dump', 'notification_db.dump']);
    const sums = await readFile(join(day, 'SHA256SUMS'), 'utf8');
    assert.match(sums, /[0-9a-f]{64}\s+coaching_db\.dump/);
    const calls = await readFile(box.log, 'utf8');
    assert.equal((calls.match(/pg_restore --list/g) ?? []).length, 3, 'each dump is read back');
    assert.doesNotMatch(calls, /rclone/, 'no off-site copy without a configured remote');
  } finally { await rm(box.dir, { recursive: true, force: true }); }
});

test('keeps 7 daily, 4 weekly (Sunday) and 6 monthly backups', async () => {
  const box = await sandbox();
  try {
    const start = new Date(Date.UTC(2026, 0, 1));
    for (let day = 0; day < 240; day++) {
      const date = new Date(start.getTime() + day * 86400000).toISOString().slice(0, 10);
      await backup(box, date);
    }
    const list = async kind => (await readdir(join(box.backups, kind))).sort();
    assert.deepEqual(await list('daily'), ['2026-08-22', '2026-08-23', '2026-08-24', '2026-08-25', '2026-08-26', '2026-08-27', '2026-08-28']);
    assert.deepEqual(await list('weekly'), ['2026-08-02', '2026-08-09', '2026-08-16', '2026-08-23']);
    assert.deepEqual(await list('monthly'), ['2026-03-01', '2026-04-01', '2026-05-01', '2026-06-01', '2026-07-01', '2026-08-01']);
  } finally { await rm(box.dir, { recursive: true, force: true }); }
});

test('encrypts the off-site copy for the configured recipient and uploads only encrypted files', async () => {
  const box = await sandbox();
  try {
    await backup(box, '2026-11-01', { BACKUP_RCLONE_REMOTE: 'offsite:eduivme-backups', BACKUP_AGE_RECIPIENT: 'age1examplerecipientkey' });
    const calls = await readFile(box.log, 'utf8');
    assert.match(calls, /age -r age1examplerecipientkey/);
    assert.match(calls, /rclone copy .*offsite:eduivme-backups\/2026-11-01/);
    const staged = join(box.backups, 'offsite', '2026-11-01');
    assert.ok(!existsSync(staged), 'encrypted staging copy is removed after upload');
    assert.doesNotMatch(calls, /rclone copy [^\n]*daily/, 'plain dumps are never uploaded');
    await backup(box, '2026-11-02', { BACKUP_RCLONE_REMOTE: 'offsite:eduivme-backups', BACKUP_AGE_RECIPIENT: 'age1examplerecipientkey', BACKUP_REMOTE_PREFIX: 'postgres' });
    assert.match(await readFile(box.log, 'utf8'), /rclone copy .*offsite:eduivme-backups\/postgres\/2026-11-02/, 'each container has its own folder');
  } finally { await rm(box.dir, { recursive: true, force: true }); }
});

test('refuses an off-site remote without an encryption recipient and fails loudly when a dump fails', async () => {
  const box = await sandbox();
  try {
    await assert.rejects(backup(box, '2026-11-02', { BACKUP_RCLONE_REMOTE: 'offsite:x' }), /BACKUP_AGE_RECIPIENT/);
    await assert.rejects(backup(box, '2026-11-03', { MOCK_FAIL_DB: 'coaching_db' }));
    assert.ok(!existsSync(join(box.backups, 'daily', '2026-11-03')), 'a failed night leaves no half backup');
    assert.match(await readFile(join(box.backups, 'last-status'), 'utf8'), /^failed 2026-11-03/);
  } finally { await rm(box.dir, { recursive: true, force: true }); }
});
