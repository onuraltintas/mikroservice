import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { overstatedClaims } from './speed-reading-claims.mjs';

// Decision 30.10.2026: the student app does not promise unproven effects (eye muscles, a wider visual field, a larger
// brain capacity). Exercises are described by what they practise.
const root = new URL('../../../clients/speed-reading/src/', import.meta.url).pathname;
const files = dir => fs.readdirSync(dir, { withFileTypes: true }).flatMap(entry => {
  const full = path.join(dir, entry.name);
  if (entry.isDirectory()) return files(full);
  return /\.(ts|html)$/.test(entry.name) && !entry.name.endsWith('.spec.ts') ? [full] : [];
});

test('the student app has no overstated speed-reading claims', () => {
  const found = files(root).flatMap(file => overstatedClaims(fs.readFileSync(file, 'utf8'))
    .map(claim => `${path.relative(root, file)}: ${claim}`));
  assert.deepEqual(found, []);
});

test('the claim check recognises the phrases it guards against', () => {
  assert.ok(overstatedClaims('Göz kaslarını geliştirmek için düzenli egzersiz yapın.').length > 0);
  assert.ok(overstatedClaims('Periferik görüş alanını genişleterek daha fazla kelime görebilirsiniz.').length > 0);
  assert.ok(overstatedClaims('Periferik görme alanınızı test edeceğiz').length > 0);
  assert.ok(overstatedClaims('Beynin algılama kapasitesini artırarak geliştirilir.').length > 0);
  assert.ok(overstatedClaims('Görüş Alanı Genişletme').length > 0);
  assert.deepEqual(overstatedClaims('Kelimeleri anlamlı öbekler hâlinde okuma.'), []);
});
