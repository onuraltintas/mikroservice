import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';

// E3 #14 (06.11.2026): the wizard asks "Şu an durumun ne?" (ortaokul/lise öğrencisi, lise mezunu, üniversite öğrencisi ya da mezunu)
// instead of the grade alone. The KVKK text published on 06.11 as version 2 (publish-kvkk-coaching-wizard-20261106.sql, never
// edited again) says "Sınıf ya da mezun olma durumu"; a new publish script changes only those sentences, with the same mechanism:
// it refuses unless the live text is the version 2 text, keeps that as a revision and raises the version by one.
// E3 #34 (no time zone choice) changes nothing in the text: it never mentioned a time zone. Not run: needs the user's go-ahead.
const read = path => readFile(new URL(`../../${path}`, import.meta.url), 'utf8');
const published = await read('docs/legal/publish-kvkk-coaching-wizard-20261106.sql');
const sql = await read('docs/legal/publish-kvkk-coaching-education-20261107.sql');
const content = (source, slug) => {
  const match = source.match(new RegExp(`\\('${slug}','[^']*',\\$legal\\$([\\s\\S]*?)\\$legal\\$\\)`));
  assert.ok(match, `${slug} text missing`);
  return match[1];
};
const v2 = content(published, 'kvkk');
const v3 = content(sql, 'kvkk');

test('the refusal compares with the published version 2 text, word for word', () => {
  assert.equal(content(sql, 'kvkk-v2'), v2);
  assert.match(sql, /RAISE EXCEPTION 'kvkk text differs from the 6 Kasım 2026 version/);
  // Idempotent: a rerun after publishing finds the new text and changes nothing.
  assert.match(sql, /p\."Content" IN \(SELECT content FROM kvkk_texts\)/);
});

test('the education sentences name the new statuses accurately', () => {
  assert.match(v3, /^7 Kasım 2026 — /);
  assert.doesNotMatch(v3, /Sınıf ya da mezun olma durumu/);
  assert.match(v3, /Eğitim durumu \(ortaokul ya da lise öğrencisi, lise mezunu, üniversite öğrencisi ya da üniversite mezunu\) ve öğrenciyse sınıfı/);
  assert.match(v3, /öğrenci olmayan bir eğitim durumu seçildiğinde kayıtlı sınıf, okul ve şube silinir/);
  assert.match(v3, /hazırlanabileceği sınavları/);
  assert.match(v3, /eğitim durumunu, sınıfını, okulunu/);
  assert.match(v3, /Eğitim durumu ya da sınıf, öğrencinin Profilim sayfasında ya da öğrencinin kurumu tarafından değiştirilirse/);
  // The diploma grade: 11th and 12th grade and everyone out of high school.
  assert.match(v3, /11\. ve 12\. sınıf öğrencileri ile liseden mezun olanlar/);
  assert.doesNotMatch(v3, /saat dilimi/i);
});

test('everything else in the version 2 text stays', () => {
  const paragraphs = v2.split('\n\n');
  const changed = paragraphs.findIndex(x => x.includes('Sınıf ya da mezun olma durumu'));
  assert.ok(changed > 0);
  paragraphs.forEach((paragraph, index) => {
    if (index === 0 || index === changed) return;
    assert.ok(v3.includes(paragraph), `dropped: ${paragraph.slice(0, 60)}…`);
  });
  // Inside the changed paragraph only the education sentences differ.
  for (const sentence of paragraphs[changed].split('. ').filter(x => !/[Ss]ınıf|mezun|diploma notu sorusu|sınavını yeniden/.test(x)))
    assert.ok(v3.includes(sentence), `dropped sentence: ${sentence.slice(0, 60)}…`);
});

test('it is published like the earlier legal texts and changes only the text', () => {
  assert.match(sql, /INSERT INTO identity\."PlatformLegalPageRevisions"/);
  assert.match(sql, /"Version"=identity\."PlatformLegalPages"\."Version"\+1/);
  assert.match(sql, /WHERE p\."Slug"='kvkk'/);
  assert.match(sql, /^BEGIN;/m); assert.match(sql, /^COMMIT;/m);
  for (const forbidden of [/RegistrationLegalConsent/i, /Acceptances/i, /Erasure/i, /DELETE FROM/i, /"users"\s+SET/i])
    assert.doesNotMatch(sql, forbidden);
});
