import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';

// Paket C #5 (05.11.2026): the shared KVKK aydınlatma metni names the data the coaching student's first-login wizard collects,
// why and where each is used. It is published with the same mechanism as docs/legal/publish-test-legal-20261001.sql: the old
// version is kept as a revision and the page version goes up by one. Only the text changes: no consent or erasure mechanism.
const read = path => readFile(new URL(`../../${path}`, import.meta.url), 'utf8');
const sql = await read('docs/legal/publish-kvkk-coaching-wizard-20261106.sql');
const content = slug => {
  const match = sql.match(new RegExp(`\\('${slug}','[^']*',\\$legal\\$([\\s\\S]*?)\\$legal\\$\\)`));
  assert.ok(match, `${slug} text missing`);
  return match[1];
};

test('the new text covers every item the wizard collects, with its purpose', () => {
  const text = content('kvkk');
  assert.match(text, /^6 Kasım 2026 — /);
  for (const item of ['doğum tarihi', 'şehir ve ilçe', 'sınıf', 'mezun', 'diploma notu', 'hazırlandığı sınav', 'hedef okul', 'bölüm', 'hedef puan',
    'davet', 'veli bağlantı kodu', 'bildirim', 'e-posta'])
    assert.ok(text.toLocaleLowerCase('tr').includes(item), `missing: ${item}`);
  for (const purpose of ['amacıyla', 'puan tahmini', 'çalışma planı', 'hedef kartı'])
    assert.ok(text.includes(purpose), `missing purpose: ${purpose}`);
  // The rest of the published 1 Ekim text stays (controller, legal bases, transfers, retention, rights).
  const previous = content('kvkk-previous');
  for (const paragraph of previous.split('\n\n').slice(1)) assert.ok(text.includes(paragraph), `dropped: ${paragraph.slice(0, 60)}…`);
});

test('it is published like the earlier legal texts: a revision of the old version and version + 1', () => {
  assert.match(sql, /INSERT INTO identity\."PlatformLegalPageRevisions"/);
  assert.match(sql, /"Version"=identity\."PlatformLegalPages"\."Version"\+1/);
  assert.match(sql, /WHERE p\."Slug"='kvkk'/);
  // It refuses to overwrite a text edited on the server since 1 Ekim.
  assert.match(sql, /RAISE EXCEPTION 'kvkk text differs/);
  assert.match(sql, /^BEGIN;/m); assert.match(sql, /^COMMIT;/m);
});

test('only the text changes: no consent, acceptance or erasure statement', () => {
  for (const forbidden of [/RegistrationLegalConsent/i, /Acceptances/i, /Erasure/i, /DELETE FROM/i, /"users"\s+SET/i])
    assert.doesNotMatch(sql, forbidden);
  assert.doesNotMatch(sql, /\('(privacy|coaching-terms|speed-reading-terms|cookies)'/);
});
