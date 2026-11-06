import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

// Paket B (05.11.2026): the 2026 LGS and MEB-AGS score model packs are reproducible from their sources (derive.py --check)
// and keep their documented shape.
//   node --test tests/E2E/coaching-score-models-2026-all-exams.contract.test.mjs
const pack = folder => fileURLToPath(new URL(`../../content-packs/coaching-score-models/${folder}/`, import.meta.url));
const run = (folder, ...args) => spawnSync('python3', ['-I', `${pack(folder)}derive.py`, ...args], { encoding: 'utf8' });

for (const [folder, file] of [['2026-lgs', 'puan-modelleri-2026-lgs.csv'], ['2026-meb-ags', 'puan-modelleri-2026-meb-ags.csv']]) {
  test(`${folder}: the committed file equals derive.py output`, () => {
    const result = run(folder, '--check');
    assert.equal(result.status, 0, result.stderr || result.stdout);
    assert.match(result.stdout, /is up to date/);
  });

  test(`${folder}: --help exits 0 and unknown arguments fail without a traceback`, () => {
    assert.equal(run(folder, '--help').status, 0);
    const bad = run(folder, '--chek');
    assert.notEqual(bad.status, 0);
    assert.match(bad.stderr, /Usage: python3 -I derive\.py/);
    assert.doesNotMatch(bad.stderr, /Traceback/);
  });

  test(`${folder}: no rank rows, a range for every score type`, () => {
    const rows = readFileSync(`${pack(folder)}${file}`, 'utf8').split('\n').filter(x => x && !x.startsWith('#')).slice(1).map(x => x.split(';'));
    assert.equal(rows.filter(x => x[0] === 'siralama').length, 0);
    const types = new Set(rows.map(x => x[1]));
    for (const type of types) assert.equal(rows.filter(x => x[1] === type && x[0] === 'belirsizlik').length, 1, type);
    assert.equal(types.size, folder === '2026-lgs' ? 1 : 19);
  });
}
